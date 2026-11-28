using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Domain.Aggregates.Categories;

/// <summary>Nội dung dịch của danh mục ở một locale (đầu vào của <see cref="Category.Create"/>/<see cref="Category.Update"/>).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả; <c>null</c> = không có.</param>
/// <param name="Slug">Đoạn URL theo ngôn ngữ (chữ thường, không khoảng trắng/<c>/?#%</c>, nối bằng <c>-</c>).</param>
public sealed record CategoryText(string Locale, string Name, string? Description, string Slug);

/// <summary>Danh mục sản phẩm nhiều cấp (bảng <c>category</c>, BRD BR-CAT-01): cây cha–con không vòng, tên/mô tả/slug theo locale
/// ở <c>category_translation</c>, slug cũ lưu ở <c>category_slug_history</c> để redirect 301 (spec §6.6).</summary>
/// <remarks>Kết thúc bằng xóa mềm (<see cref="ArchivedAt"/>), không xóa dòng; slug của danh mục đã xóa vẫn giữ chỗ.</remarks>
public sealed partial class Category : AggregateRoot<Guid>
{
    /// <summary>Độ dài tối đa của tên (khớp CHECK <c>category_translation.name</c>).</summary>
    public const int NameMaxLength = 200;

    /// <summary>Độ dài tối đa của mô tả.</summary>
    public const int DescriptionMaxLength = 5000;

    /// <summary>Độ dài tối đa của slug.</summary>
    public const int SlugMaxLength = 200;

    readonly List<CategoryTranslation> _translations = [];
    readonly List<CategorySlugRedirect> _slugHistory = [];

    Category(Guid id) : base(id) { }

    /// <summary>Danh mục cha; <c>null</c> = gốc.</summary>
    public Guid? ParentId { get; private set; }

    /// <summary>Thứ tự trong cùng cha (nhỏ trước).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Thời điểm xóa mềm; <c>null</c> = đang hoạt động.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Phiên bản cho optimistic concurrency (cột <c>version</c>).</summary>
    public long Version { get; private set; }

    /// <summary>Cờ đã xóa mềm.</summary>
    public bool IsArchived => ArchivedAt is not null;

    /// <summary>Bản dịch hiện hành, mỗi locale một dòng; luôn có <see cref="LocaleChain.Default"/>.</summary>
    public IReadOnlyList<CategoryTranslation> Translations => _translations;

    /// <summary>Slug đã thay (locale, slug cũ) để redirect về slug hiện hành.</summary>
    public IReadOnlyList<CategorySlugRedirect> SlugHistory => _slugHistory;

    /// <summary>Tạo danh mục; phát <see cref="CategoryCreated"/>. Cha phải tồn tại và chưa xóa (Application kiểm).</summary>
    /// <param name="parentId">Danh mục cha; <c>null</c> = gốc.</param>
    /// <param name="sortOrder">Thứ tự trong cha.</param>
    /// <param name="texts">Bản dịch, bắt buộc có <c>vi</c>.</param>
    /// <param name="now">Thời điểm tạo (sinh Id UUIDv7).</param>
    /// <returns>Danh mục mới; lỗi <see cref="CategoryErrors.TranslationInvalid"/> hoặc <see cref="CategoryErrors.DefaultTranslationMissing"/>.</returns>
    public static Result<Category> Create(Guid? parentId, int sortOrder, IReadOnlyCollection<CategoryText> texts, DateTimeOffset now)
    {
        var valid = Validate(texts);
        if (valid.IsFailure)
            return valid.Error;
        var category = new Category(Guid.CreateVersion7(now)) { ParentId = parentId, SortOrder = sortOrder };
        foreach (var t in valid.Value)
            category._translations.Add(new CategoryTranslation(t.Locale, t.Name, t.Description, t.Slug));
        category.Raise(new CategoryCreated(category.Id, parentId, sortOrder, valid.Value));
        return category;
    }

    /// <summary>Sửa thứ tự và toàn bộ bản dịch; slug thay đổi hoặc locale bị bỏ thì slug cũ vào <see cref="SlugHistory"/>. Phát <see cref="CategoryUpdated"/>.</summary>
    /// <param name="sortOrder">Thứ tự mới.</param>
    /// <param name="texts">Bộ bản dịch mới (thay hết bộ cũ), bắt buộc có <c>vi</c>.</param>
    /// <param name="now">Thời điểm sửa.</param>
    /// <returns>Thành công; lỗi <see cref="CategoryErrors.Archived"/>, <see cref="CategoryErrors.TranslationInvalid"/> hoặc
    /// <see cref="CategoryErrors.DefaultTranslationMissing"/>.</returns>
    public Result Update(int sortOrder, IReadOnlyCollection<CategoryText> texts, DateTimeOffset now)
    {
        if (IsArchived)
            return CategoryErrors.Archived;
        var valid = Validate(texts);
        if (valid.IsFailure)
            return valid.Error;
        var next = valid.Value.ToDictionary(t => t.Locale);
        foreach (var current in _translations.ToList())
        {
            if (!next.TryGetValue(current.Locale, out var t))
            {
                Retire(current.Locale, current.Slug, now);
                _translations.Remove(current);
                continue;
            }
            if (t.Slug != current.Slug)
                Retire(current.Locale, current.Slug, now);
            current.Set(t.Name, t.Description, t.Slug);
            next.Remove(t.Locale);
        }
        foreach (var t in next.Values)
            _translations.Add(new CategoryTranslation(t.Locale, t.Name, t.Description, t.Slug));
        // Slug hiện hành lấy lại từ lịch sử thì không còn là redirect.
        _slugHistory.RemoveAll(h => _translations.Any(t => t.Locale == h.Locale && t.Slug == h.Slug));
        SortOrder = sortOrder;
        Raise(new CategoryUpdated(Id, sortOrder, valid.Value));
        return Result.Success();
    }

    /// <summary>Chuyển sang cha khác; chặn vòng lặp cha–con (BR-CAT-01). Phát <see cref="CategoryMoved"/>.</summary>
    /// <param name="parentId">Cha mới; <c>null</c> = lên gốc. Cha phải tồn tại và chưa xóa (Application kiểm).</param>
    /// <param name="parents">Id → cha hiện tại của mọi danh mục, đọc trong cùng khoá cây với lệnh.</param>
    /// <param name="now">Thời điểm chuyển.</param>
    /// <returns>Thành công; lỗi <see cref="CategoryErrors.Archived"/> hoặc <see cref="CategoryErrors.Cycle"/> nếu cha mới là chính nó hoặc con/cháu của nó.</returns>
    public Result MoveTo(Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents, DateTimeOffset now)
    {
        if (IsArchived)
            return CategoryErrors.Archived;
        // Đi ngược từ cha mới lên gốc; gặp chính mình là vòng. Giới hạn bước để dữ liệu hỏng (đã có vòng) không lặp vô hạn.
        var steps = 0;
        for (var p = parentId; p is not null; p = parents.GetValueOrDefault(p.Value))
            if (p == Id || ++steps > parents.Count + 1)
                return CategoryErrors.Cycle;
        ParentId = parentId;
        Raise(new CategoryMoved(Id, parentId, now));
        return Result.Success();
    }

    /// <summary>Xóa mềm; idempotent nếu đã xóa. Phát <see cref="CategoryArchived"/>.</summary>
    /// <param name="hasActiveChildren">Còn danh mục con chưa xóa (đọc trong khoá cây).</param>
    /// <param name="now">Thời điểm xóa.</param>
    /// <returns>Thành công; lỗi <see cref="CategoryErrors.HasActiveChildren"/>.</returns>
    public Result Archive(bool hasActiveChildren, DateTimeOffset now)
    {
        if (IsArchived)
            return Result.Success();
        if (hasActiveChildren)
            return CategoryErrors.HasActiveChildren;
        ArchivedAt = now;
        Raise(new CategoryArchived(Id, now));
        return Result.Success();
    }

    void Retire(string locale, string slug, DateTimeOffset now)
    {
        if (!_slugHistory.Any(h => h.Locale == locale && h.Slug == slug))
            _slugHistory.Add(new CategorySlugRedirect(locale, slug, now));
    }

    static Result<CategoryText[]> Validate(IReadOnlyCollection<CategoryText>? texts)
    {
        if (texts is null || texts.Count == 0)
            return CategoryErrors.DefaultTranslationMissing;
        var clean = new List<CategoryText>(texts.Count);
        foreach (var t in texts)
        {
            if (t is null || t.Locale is null || !LocalePattern().IsMatch(t.Locale) || t.Slug is null || !SlugPattern().IsMatch(t.Slug)
                || t.Slug.Length > SlugMaxLength || string.IsNullOrWhiteSpace(t.Name) || t.Name.Trim().Length > NameMaxLength
                || t.Description?.Length > DescriptionMaxLength || clean.Any(c => c.Locale == t.Locale))
                return CategoryErrors.TranslationInvalid;
            clean.Add(t with { Name = t.Name.Trim(), Description = string.IsNullOrWhiteSpace(t.Description) ? null : t.Description.Trim() });
        }
        return clean.Any(t => t.Locale == LocaleChain.Default) ? clean.ToArray() : CategoryErrors.DefaultTranslationMissing;
    }

    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex LocalePattern();

    // Chữ thường/chữ không phân hoa thường (CJK…)/số, nối bằng một dấu '-'.
    [GeneratedRegex(@"^[\p{Ll}\p{Lo}\p{Lm}\p{Mn}\p{Nd}]+(-[\p{Ll}\p{Lo}\p{Lm}\p{Mn}\p{Nd}]+)*$")]
    private static partial Regex SlugPattern();
}

/// <summary>Bản dịch của danh mục ở một locale (dòng <c>category_translation</c>, khoá <c>(category_id, locale)</c>).</summary>
public sealed class CategoryTranslation
{
    internal CategoryTranslation(string locale, string name, string? description, string slug) =>
        (Locale, Name, Description, Slug) = (locale, name, description, slug);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; }

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; private set; }

    /// <summary>Mô tả.</summary>
    public string? Description { get; private set; }

    /// <summary>Slug hiện hành, duy nhất theo locale.</summary>
    public string Slug { get; private set; }

    internal void Set(string name, string? description, string slug) => (Name, Description, Slug) = (name, description, slug);
}

/// <summary>Slug đã thay của danh mục (dòng <c>category_slug_history</c>): URL cũ redirect 301 về slug hiện hành.</summary>
public sealed class CategorySlugRedirect
{
    internal CategorySlugRedirect(string locale, string slug, DateTimeOffset replacedAt) => (Locale, Slug, ReplacedAt) = (locale, slug, replacedAt);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; }

    /// <summary>Slug cũ.</summary>
    public string Slug { get; private set; }

    /// <summary>Thời điểm bị thay.</summary>
    public DateTimeOffset ReplacedAt { get; private set; }
}

/// <summary>Danh mục vừa được tạo (ghi audit).</summary>
/// <param name="CategoryId">Id.</param>
/// <param name="ParentId">Cha.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Translations">Bản dịch.</param>
public sealed record CategoryCreated(Guid CategoryId, Guid? ParentId, int SortOrder, IReadOnlyList<CategoryText> Translations) : IDomainEvent;

/// <summary>Danh mục vừa được sửa thứ tự/bản dịch (ghi audit).</summary>
/// <param name="CategoryId">Id.</param>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Translations">Bộ bản dịch mới.</param>
public sealed record CategoryUpdated(Guid CategoryId, int SortOrder, IReadOnlyList<CategoryText> Translations) : IDomainEvent;

/// <summary>Danh mục vừa chuyển cha (ghi audit).</summary>
/// <param name="CategoryId">Id.</param>
/// <param name="ParentId">Cha mới.</param>
/// <param name="MovedAt">Thời điểm.</param>
public sealed record CategoryMoved(Guid CategoryId, Guid? ParentId, DateTimeOffset MovedAt) : IDomainEvent;

/// <summary>Danh mục vừa bị xóa mềm (ghi audit).</summary>
/// <param name="CategoryId">Id.</param>
/// <param name="ArchivedAt">Thời điểm.</param>
public sealed record CategoryArchived(Guid CategoryId, DateTimeOffset ArchivedAt) : IDomainEvent;
