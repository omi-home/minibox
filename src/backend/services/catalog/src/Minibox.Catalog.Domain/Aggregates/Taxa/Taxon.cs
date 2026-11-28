using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Domain.Aggregates.Taxa;

/// <summary>Loại của <see cref="Taxon"/> (giá trị cột <c>taxon.kind</c>).</summary>
public static class TaxonKinds
{
    /// <summary>Bộ sưu tập (trang tuyển chọn có URL riêng).</summary>
    public const string Collection = "collection";

    /// <summary>Thẻ.</summary>
    public const string Tag = "tag";

    /// <summary>Kiểm giá trị là một loại hợp lệ.</summary>
    /// <param name="kind">Giá trị cần kiểm.</param>
    /// <returns><c>true</c> nếu là <see cref="Collection"/> hoặc <see cref="Tag"/>.</returns>
    public static bool IsValid(string? kind) => kind is Collection or Tag;
}

/// <summary>Nội dung dịch của bộ sưu tập/thẻ ở một locale (đầu vào của <see cref="Taxon.Create"/>/<see cref="Taxon.Update"/>).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả; <c>null</c> = không có.</param>
/// <param name="Slug">Đoạn URL theo ngôn ngữ (chữ thường, không khoảng trắng/<c>/?#%</c>, nối bằng <c>-</c>).</param>
public sealed record TaxonText(string Locale, string Name, string? Description, string Slug);

/// <summary>Bộ sưu tập hoặc thẻ (bảng <c>taxon</c>, BRD BR-CAT-01): độc lập với cây danh mục, một sản phẩm thuộc nhiều bộ sưu tập/thẻ
/// (<c>product_taxon</c>); tên/mô tả/slug theo locale ở <c>taxon_translation</c>, slug cũ ở <c>taxon_slug_history</c> để redirect 301 (spec §6.6).</summary>
/// <remarks>Hai loại chung một aggregate vì cùng hình dạng; slug duy nhất theo locale trên cả hai loại. Kết thúc bằng xóa mềm.</remarks>
public sealed class Taxon : AggregateRoot<Guid>
{
    /// <summary>Độ dài tối đa của tên (khớp CHECK <c>taxon_translation.name</c>).</summary>
    public const int NameMaxLength = 200;

    /// <summary>Độ dài tối đa của mô tả.</summary>
    public const int DescriptionMaxLength = 5000;

    /// <summary>Độ dài tối đa của slug.</summary>
    public const int SlugMaxLength = 200;

    readonly List<TaxonTranslation> _translations = [];
    readonly List<TaxonSlugRedirect> _slugHistory = [];

    Taxon(Guid id) : base(id) { }

    /// <summary>Loại: <see cref="TaxonKinds.Collection"/> hoặc <see cref="TaxonKinds.Tag"/>; không đổi sau khi tạo.</summary>
    public string Kind { get; private set; } = "";

    /// <summary>Thứ tự hiển thị trong cùng loại (nhỏ trước).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Thời điểm xóa mềm; <c>null</c> = đang hoạt động.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Phiên bản cho optimistic concurrency (cột <c>version</c>).</summary>
    public long Version { get; private set; }

    /// <summary>Cờ đã xóa mềm.</summary>
    public bool IsArchived => ArchivedAt is not null;

    /// <summary>Bản dịch hiện hành, mỗi locale một dòng; luôn có <see cref="LocaleChain.Default"/>.</summary>
    public IReadOnlyList<TaxonTranslation> Translations => _translations;

    /// <summary>Slug đã thay (locale, slug cũ) để redirect về slug hiện hành.</summary>
    public IReadOnlyList<TaxonSlugRedirect> SlugHistory => _slugHistory;

    /// <summary>Tạo bộ sưu tập/thẻ; phát <see cref="TaxonCreated"/>.</summary>
    /// <param name="kind">Loại (<see cref="TaxonKinds"/>).</param>
    /// <param name="sortOrder">Thứ tự hiển thị.</param>
    /// <param name="texts">Bản dịch, bắt buộc có <c>vi</c>.</param>
    /// <param name="now">Thời điểm tạo (sinh Id UUIDv7).</param>
    /// <returns>Taxon mới; lỗi <see cref="TaxonErrors.KindInvalid"/>, <see cref="TaxonErrors.TranslationInvalid"/> hoặc
    /// <see cref="TaxonErrors.DefaultTranslationMissing"/>.</returns>
    public static Result<Taxon> Create(string kind, int sortOrder, IReadOnlyCollection<TaxonText> texts, DateTimeOffset now)
    {
        if (!TaxonKinds.IsValid(kind))
            return TaxonErrors.KindInvalid;
        var valid = Validate(texts);
        if (valid.IsFailure)
            return valid.Error;
        var taxon = new Taxon(Guid.CreateVersion7(now)) { Kind = kind, SortOrder = sortOrder };
        foreach (var t in valid.Value)
            taxon._translations.Add(new TaxonTranslation(t.Locale, t.Name, t.Description, t.Slug));
        taxon.Raise(new TaxonCreated(taxon.Id, kind, sortOrder, valid.Value));
        return taxon;
    }

    /// <summary>Sửa thứ tự và toàn bộ bản dịch; slug thay đổi hoặc locale bị bỏ thì slug cũ vào <see cref="SlugHistory"/>. Phát <see cref="TaxonUpdated"/>.</summary>
    /// <param name="sortOrder">Thứ tự mới.</param>
    /// <param name="texts">Bộ bản dịch mới (thay hết bộ cũ), bắt buộc có <c>vi</c>.</param>
    /// <param name="now">Thời điểm sửa.</param>
    /// <returns>Thành công; lỗi <see cref="TaxonErrors.Archived"/>, <see cref="TaxonErrors.TranslationInvalid"/> hoặc
    /// <see cref="TaxonErrors.DefaultTranslationMissing"/>.</returns>
    public Result Update(int sortOrder, IReadOnlyCollection<TaxonText> texts, DateTimeOffset now)
    {
        if (IsArchived)
            return TaxonErrors.Archived;
        var valid = Validate(texts);
        if (valid.IsFailure)
            return valid.Error;
        var next = valid.Value.ToDictionary(t => t.Locale);
        foreach (var current in _translations.ToList())
        {
            if (!next.Remove(current.Locale, out var t))
            {
                Retire(current.Locale, current.Slug, now);
                _translations.Remove(current);
                continue;
            }
            if (t.Slug != current.Slug)
                Retire(current.Locale, current.Slug, now);
            current.Set(t.Name, t.Description, t.Slug);
        }
        foreach (var t in next.Values)
            _translations.Add(new TaxonTranslation(t.Locale, t.Name, t.Description, t.Slug));
        // Slug hiện hành lấy lại từ lịch sử thì không còn là redirect.
        _slugHistory.RemoveAll(h => _translations.Any(t => t.Locale == h.Locale && t.Slug == h.Slug));
        SortOrder = sortOrder;
        Raise(new TaxonUpdated(Id, sortOrder, valid.Value));
        return Result.Success();
    }

    /// <summary>Xóa mềm; idempotent nếu đã xóa. Phát <see cref="TaxonArchived"/>. Sản phẩm vẫn giữ liên kết, phía đọc công khai lọc bỏ.</summary>
    /// <param name="now">Thời điểm xóa.</param>
    /// <returns>Luôn thành công.</returns>
    public Result Archive(DateTimeOffset now)
    {
        if (IsArchived)
            return Result.Success();
        ArchivedAt = now;
        Raise(new TaxonArchived(Id, now));
        return Result.Success();
    }

    void Retire(string locale, string slug, DateTimeOffset now)
    {
        if (!_slugHistory.Any(h => h.Locale == locale && h.Slug == slug))
            _slugHistory.Add(new TaxonSlugRedirect(locale, slug, now));
    }

    static Result<TaxonText[]> Validate(IReadOnlyCollection<TaxonText>? texts)
    {
        if (texts is null || texts.Count == 0)
            return TaxonErrors.DefaultTranslationMissing;
        var clean = new List<TaxonText>(texts.Count);
        foreach (var t in texts)
        {
            if (t is null || t.Locale is null || !TextRules.Locale().IsMatch(t.Locale) || t.Slug is null || !TextRules.Slug().IsMatch(t.Slug)
                || t.Slug.Length > SlugMaxLength || string.IsNullOrWhiteSpace(t.Name) || t.Name.Trim().Length > NameMaxLength
                || t.Description?.Length > DescriptionMaxLength || clean.Any(c => c.Locale == t.Locale))
                return TaxonErrors.TranslationInvalid;
            clean.Add(t with { Name = t.Name.Trim(), Description = string.IsNullOrWhiteSpace(t.Description) ? null : t.Description.Trim() });
        }
        return clean.Any(t => t.Locale == LocaleChain.Default) ? clean.ToArray() : TaxonErrors.DefaultTranslationMissing;
    }
}

/// <summary>Bản dịch của taxon ở một locale (dòng <c>taxon_translation</c>, khoá <c>(taxon_id, locale)</c>).</summary>
public sealed class TaxonTranslation
{
    internal TaxonTranslation(string locale, string name, string? description, string slug) =>
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

/// <summary>Slug đã thay của taxon (dòng <c>taxon_slug_history</c>): URL cũ redirect 301 về slug hiện hành.</summary>
public sealed class TaxonSlugRedirect
{
    internal TaxonSlugRedirect(string locale, string slug, DateTimeOffset replacedAt) => (Locale, Slug, ReplacedAt) = (locale, slug, replacedAt);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; }

    /// <summary>Slug cũ.</summary>
    public string Slug { get; private set; }

    /// <summary>Thời điểm bị thay.</summary>
    public DateTimeOffset ReplacedAt { get; private set; }
}

/// <summary>Bộ sưu tập/thẻ vừa được tạo (ghi audit).</summary>
/// <param name="TaxonId">Id.</param>
/// <param name="Kind">Loại.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Translations">Bản dịch.</param>
public sealed record TaxonCreated(Guid TaxonId, string Kind, int SortOrder, IReadOnlyList<TaxonText> Translations) : IDomainEvent;

/// <summary>Bộ sưu tập/thẻ vừa được sửa thứ tự/bản dịch (ghi audit).</summary>
/// <param name="TaxonId">Id.</param>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Translations">Bộ bản dịch mới.</param>
public sealed record TaxonUpdated(Guid TaxonId, int SortOrder, IReadOnlyList<TaxonText> Translations) : IDomainEvent;

/// <summary>Bộ sưu tập/thẻ vừa bị xóa mềm (ghi audit).</summary>
/// <param name="TaxonId">Id.</param>
/// <param name="ArchivedAt">Thời điểm.</param>
public sealed record TaxonArchived(Guid TaxonId, DateTimeOffset ArchivedAt) : IDomainEvent;
