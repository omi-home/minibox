using System.Globalization;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Domain.Aggregates.Products;

/// <summary>Trạng thái của <see cref="Product"/> (giá trị cột <c>product.status</c>).</summary>
public static class ProductStatuses
{
    /// <summary>Nháp: đang soạn, chưa gửi duyệt.</summary>
    public const string Draft = "draft";
}

/// <summary>Nội dung dịch của sản phẩm ở một locale (đầu vào của <see cref="Product.Create"/>/<see cref="Product.Update"/>).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả; <c>null</c> = không có.</param>
/// <param name="Slug">Đoạn URL theo ngôn ngữ.</param>
/// <param name="SeoTitle">Tiêu đề SEO; <c>null</c> = dùng tên.</param>
/// <param name="SeoDescription">Mô tả SEO; <c>null</c> = không có.</param>
public sealed record ProductText(string Locale, string Name, string? Description, string Slug, string? SeoTitle, string? SeoDescription);

/// <summary>Giá trị của một thuộc tính mô tả (đầu vào của <see cref="Product"/>).</summary>
/// <param name="Code">Mã thuộc tính trong nhóm của sản phẩm.</param>
/// <param name="Values">Giá trị: một phần tử, trừ <c>multi_select</c> (một hoặc nhiều mã cho phép).</param>
public sealed record AttributeValueInput(string Code, IReadOnlyList<string> Values);

/// <summary>Sản phẩm (bảng <c>product</c>, BRD BR-CAT-01/02): thuộc một nhóm (<see cref="ProductType"/>) và một danh mục, nhiều bộ sưu tập/thẻ
/// (<c>product_taxon</c>); tên/mô tả/slug/SEO theo locale ở <c>product_translation</c>, slug cũ ở <c>product_slug_history</c>;
/// giá trị thuộc tính mô tả ở <c>product_attribute_value</c>.</summary>
/// <remarks>Thuộc tính tạo biến thể không đặt ở sản phẩm mà ở biến thể. Giá trị chuẩn hoá: number dạng invariant bỏ số 0 thừa,
/// boolean <c>true</c>/<c>false</c>, select/multi_select là mã giá trị cho phép (mỗi mã một dòng).</remarks>
public sealed class Product : AggregateRoot<Guid>
{
    /// <summary>Độ dài tối đa của tên.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Độ dài tối đa của mô tả.</summary>
    public const int DescriptionMaxLength = 20000;

    /// <summary>Độ dài tối đa của slug.</summary>
    public const int SlugMaxLength = 200;

    /// <summary>Độ dài tối đa của tiêu đề SEO.</summary>
    public const int SeoTitleMaxLength = 200;

    /// <summary>Độ dài tối đa của mô tả SEO.</summary>
    public const int SeoDescriptionMaxLength = 500;

    /// <summary>Độ dài tối đa của giá trị thuộc tính kiểu text.</summary>
    public const int TextValueMaxLength = 2000;

    readonly List<ProductTranslation> _translations = [];
    readonly List<ProductSlugRedirect> _slugHistory = [];
    readonly List<ProductAttributeValue> _attributeValues = [];
    readonly List<ProductTaxonLink> _taxa = [];

    Product(Guid id) : base(id) { }

    /// <summary>Nhóm sản phẩm (quyết định bộ thuộc tính).</summary>
    public Guid ProductTypeId { get; private set; }

    /// <summary>Danh mục.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Trạng thái (<see cref="ProductStatuses"/>).</summary>
    public string Status { get; private set; } = ProductStatuses.Draft;

    /// <summary>Phiên bản cho optimistic concurrency (cột <c>version</c>).</summary>
    public long Version { get; private set; }

    /// <summary>Bản dịch hiện hành; luôn có <see cref="LocaleChain.Default"/>.</summary>
    public IReadOnlyList<ProductTranslation> Translations => _translations;

    /// <summary>Slug đã thay để redirect 301 về slug hiện hành.</summary>
    public IReadOnlyList<ProductSlugRedirect> SlugHistory => _slugHistory;

    /// <summary>Giá trị thuộc tính mô tả (multi_select nhiều dòng cùng thuộc tính).</summary>
    public IReadOnlyList<ProductAttributeValue> AttributeValues => _attributeValues;

    /// <summary>Liên kết bộ sưu tập/thẻ (dòng <c>product_taxon</c>).</summary>
    public IReadOnlyList<ProductTaxonLink> Taxa => _taxa;

    /// <summary>Bộ sưu tập và thẻ đang gắn.</summary>
    public IReadOnlyList<Guid> TaxonIds => _taxa.Select(t => t.TaxonId).ToArray();

    /// <summary>Tạo sản phẩm nháp; phát <see cref="ProductCreated"/>.</summary>
    /// <param name="type">Nhóm đang hoạt động (người gọi đã kiểm).</param>
    /// <param name="categoryId">Danh mục đang hoạt động (người gọi đã kiểm).</param>
    /// <param name="texts">Bản dịch, bắt buộc có <c>vi</c>.</param>
    /// <param name="values">Giá trị thuộc tính mô tả theo bộ thuộc tính của <paramref name="type"/>.</param>
    /// <param name="now">Thời điểm tạo (sinh Id UUIDv7).</param>
    /// <returns>Sản phẩm mới; lỗi <see cref="ProductErrors.TranslationInvalid"/>, <see cref="ProductErrors.DefaultTranslationMissing"/>
    /// hoặc <see cref="ProductErrors.AttributeValueInvalid"/>.</returns>
    public static Result<Product> Create(ProductType type, Guid categoryId, IReadOnlyCollection<ProductText> texts,
        IReadOnlyCollection<AttributeValueInput> values, DateTimeOffset now)
    {
        var product = new Product(Guid.CreateVersion7(now));
        var set = product.Set(type, categoryId, texts, values, now);
        if (set.IsFailure)
            return set.Error;
        product.Raise(new ProductCreated(product.Id, type.Id, categoryId, set.Value.Texts, set.Value.Values));
        return product;
    }

    /// <summary>Sửa nháp: nhóm, danh mục, toàn bộ bản dịch và giá trị thuộc tính; slug đổi hoặc locale bị bỏ thì slug cũ vào <see cref="SlugHistory"/>.
    /// Phát <see cref="ProductUpdated"/>.</summary>
    /// <param name="type">Nhóm đang hoạt động (có thể khác nhóm cũ; giá trị kiểm theo nhóm này).</param>
    /// <param name="categoryId">Danh mục đang hoạt động.</param>
    /// <param name="texts">Bộ bản dịch mới (thay hết bộ cũ).</param>
    /// <param name="values">Bộ giá trị thuộc tính mới (thay hết bộ cũ).</param>
    /// <param name="now">Thời điểm sửa.</param>
    /// <returns>Thành công; lỗi như <see cref="Create"/>; lỗi thì không đổi gì.</returns>
    public Result Update(ProductType type, Guid categoryId, IReadOnlyCollection<ProductText> texts, IReadOnlyCollection<AttributeValueInput> values, DateTimeOffset now)
    {
        var set = Set(type, categoryId, texts, values, now);
        if (set.IsFailure)
            return set.Error;
        Raise(new ProductUpdated(Id, type.Id, categoryId, set.Value.Texts, set.Value.Values));
        return Result.Success();
    }

    /// <summary>Thay trọn bộ sưu tập/thẻ đang gắn (bỏ trùng); phát <see cref="ProductTaxaChanged"/>.</summary>
    /// <param name="taxonIds">Taxon đang hoạt động (người gọi đã kiểm).</param>
    /// <param name="now">Thời điểm.</param>
    public void SetTaxa(IReadOnlyCollection<Guid> taxonIds, DateTimeOffset now)
    {
        var ids = taxonIds.Distinct().ToArray();
        ProductType.Sync(_taxa, ids, t => t.TaxonId.ToString(), id => id.ToString(), (id, _) => new ProductTaxonLink(id), (_, _, _) => { });
        Raise(new ProductTaxaChanged(Id, ids, now));
    }

    /// <summary>Liệt kê thuộc tính mô tả bắt buộc của nhóm mà sản phẩm chưa có giá trị (kiểm khi gửi duyệt).</summary>
    /// <param name="type">Nhóm hiện tại của sản phẩm.</param>
    /// <returns>Mã thuộc tính theo thứ tự trong nhóm; rỗng = đủ.</returns>
    /// <exception cref="ArgumentException">Khi <paramref name="type"/> không phải nhóm của sản phẩm.</exception>
    public IReadOnlyList<string> MissingRequiredAttributes(ProductType type)
    {
        if (type.Id != ProductTypeId)
            throw new ArgumentException("Nhóm không khớp sản phẩm.", nameof(type));
        return type.Attributes.Where(a => a.Required && !a.Variant && _attributeValues.All(v => v.AttributeId != a.Id)).Select(a => a.Code).ToArray();
    }

    Result<(ProductText[] Texts, AttributeValueInput[] Values)> Set(ProductType type, Guid categoryId, IReadOnlyCollection<ProductText> texts,
        IReadOnlyCollection<AttributeValueInput> values, DateTimeOffset now)
    {
        var cleanTexts = ValidateTexts(texts);
        if (cleanTexts.IsFailure)
            return cleanTexts.Error;
        var rows = ValidateValues(type, values);
        if (rows.IsFailure)
            return rows.Error;

        (ProductTypeId, CategoryId) = (type.Id, categoryId);
        var next = cleanTexts.Value.ToDictionary(t => t.Locale);
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
            current.Set(t);
        }
        foreach (var t in next.Values)
            _translations.Add(new ProductTranslation(t));
        // Slug hiện hành lấy lại từ lịch sử thì không còn là redirect.
        _slugHistory.RemoveAll(h => _translations.Any(t => t.Locale == h.Locale && t.Slug == h.Slug));
        ProductType.Sync(_attributeValues, rows.Value.Rows, v => $"{v.AttributeId}|{v.Value}", r => $"{r.AttributeId}|{r.Value}",
            (r, _) => r, (_, _, _) => { });
        return (cleanTexts.Value, rows.Value.Inputs);
    }

    void Retire(string locale, string slug, DateTimeOffset now)
    {
        if (!_slugHistory.Any(h => h.Locale == locale && h.Slug == slug))
            _slugHistory.Add(new ProductSlugRedirect(locale, slug, now));
    }

    static Result<ProductText[]> ValidateTexts(IReadOnlyCollection<ProductText>? texts)
    {
        var clean = new List<ProductText>();
        foreach (var t in texts ?? [])
        {
            if (t is null || t.Locale is null || !TextRules.Locale().IsMatch(t.Locale) || t.Slug is null || !TextRules.Slug().IsMatch(t.Slug)
                || t.Slug.Length > SlugMaxLength || string.IsNullOrWhiteSpace(t.Name) || t.Name.Trim().Length > NameMaxLength
                || t.Description?.Length > DescriptionMaxLength || t.SeoTitle?.Trim().Length > SeoTitleMaxLength
                || t.SeoDescription?.Trim().Length > SeoDescriptionMaxLength || clean.Any(c => c.Locale == t.Locale))
                return ProductErrors.TranslationInvalid;
            clean.Add(t with { Name = t.Name.Trim(), Description = Blank(t.Description), SeoTitle = Blank(t.SeoTitle), SeoDescription = Blank(t.SeoDescription) });
        }
        return clean.Any(t => t.Locale == LocaleChain.Default) ? clean.ToArray() : ProductErrors.DefaultTranslationMissing;
    }

    static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    static Result<(ProductAttributeValue[] Rows, AttributeValueInput[] Inputs)> ValidateValues(ProductType type, IReadOnlyCollection<AttributeValueInput>? values)
    {
        var rows = new List<ProductAttributeValue>();
        var inputs = new List<AttributeValueInput>();
        foreach (var input in values ?? [])
        {
            var attribute = input is null ? null : type.Attributes.FirstOrDefault(a => a.Code == input.Code);
            if (attribute is null || attribute.Variant || inputs.Any(i => i.Code == input!.Code))
                return ProductErrors.AttributeValueInvalid;
            var normalized = Normalize(attribute, input!.Values ?? []);
            if (normalized is null)
                return ProductErrors.AttributeValueInvalid;
            rows.AddRange(normalized.Select(v => new ProductAttributeValue(attribute.Id, v)));
            inputs.Add(new AttributeValueInput(attribute.Code, normalized));
        }
        return (rows.ToArray(), inputs.ToArray());
    }

    // Giá trị chuẩn hoá theo kiểu; null nếu sai.
    static string[]? Normalize(ProductAttribute attribute, IReadOnlyList<string> values)
    {
        if (values.Count == 0 || values.Any(v => v is null) || attribute.DataType != AttributeTypes.MultiSelect && values.Count != 1)
            return null;
        var v = values[0];
        return attribute.DataType switch
        {
            AttributeTypes.Text => v.Trim() is { Length: > 0 and <= TextValueMaxLength } text ? [text] : null,
            AttributeTypes.Number => decimal.TryParse(v, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)
                ? [d.ToString("G29", CultureInfo.InvariantCulture)] : null,
            AttributeTypes.Boolean => v is "true" or "false" ? [v] : null,
            _ => values.Distinct().ToArray() is var codes && codes.All(c => attribute.Values.Any(a => a.Code == c)) ? codes : null,
        };
    }
}

/// <summary>Bản dịch của sản phẩm ở một locale (dòng <c>product_translation</c>, khoá <c>(product_id, locale)</c>).</summary>
public sealed class ProductTranslation
{
    ProductTranslation() { }

    internal ProductTranslation(ProductText t) => Set(t);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; } = "";

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; private set; } = "";

    /// <summary>Mô tả.</summary>
    public string? Description { get; private set; }

    /// <summary>Slug hiện hành, duy nhất theo locale.</summary>
    public string Slug { get; private set; } = "";

    /// <summary>Tiêu đề SEO.</summary>
    public string? SeoTitle { get; private set; }

    /// <summary>Mô tả SEO.</summary>
    public string? SeoDescription { get; private set; }

    internal void Set(ProductText t) => (Locale, Name, Description, Slug, SeoTitle, SeoDescription) = (t.Locale, t.Name, t.Description, t.Slug, t.SeoTitle, t.SeoDescription);
}

/// <summary>Slug đã thay của sản phẩm (dòng <c>product_slug_history</c>): URL cũ redirect 301 về slug hiện hành.</summary>
public sealed class ProductSlugRedirect
{
    internal ProductSlugRedirect(string locale, string slug, DateTimeOffset replacedAt) => (Locale, Slug, ReplacedAt) = (locale, slug, replacedAt);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; }

    /// <summary>Slug cũ.</summary>
    public string Slug { get; private set; }

    /// <summary>Thời điểm bị thay.</summary>
    public DateTimeOffset ReplacedAt { get; private set; }
}

/// <summary>Một giá trị thuộc tính của sản phẩm (dòng <c>product_attribute_value</c>, khoá <c>(product_id, attribute_id, value)</c>).</summary>
public sealed class ProductAttributeValue
{
    internal ProductAttributeValue(Guid attributeId, string value) => (AttributeId, Value) = (attributeId, value);

    /// <summary>Thuộc tính của nhóm.</summary>
    public Guid AttributeId { get; private set; }

    /// <summary>Giá trị đã chuẩn hoá.</summary>
    public string Value { get; private set; }
}

/// <summary>Liên kết sản phẩm–bộ sưu tập/thẻ (dòng <c>product_taxon</c>).</summary>
public sealed class ProductTaxonLink
{
    internal ProductTaxonLink(Guid taxonId) => TaxonId = taxonId;

    /// <summary>Taxon.</summary>
    public Guid TaxonId { get; private set; }
}

/// <summary>Sản phẩm nháp vừa được tạo (ghi audit).</summary>
/// <param name="ProductId">Id.</param>
/// <param name="ProductTypeId">Nhóm.</param>
/// <param name="CategoryId">Danh mục.</param>
/// <param name="Translations">Bản dịch.</param>
/// <param name="Attributes">Giá trị thuộc tính đã chuẩn hoá.</param>
public sealed record ProductCreated(Guid ProductId, Guid ProductTypeId, Guid CategoryId, IReadOnlyList<ProductText> Translations,
    IReadOnlyList<AttributeValueInput> Attributes) : IDomainEvent;

/// <summary>Sản phẩm nháp vừa được sửa (ghi audit).</summary>
/// <param name="ProductId">Id.</param>
/// <param name="ProductTypeId">Nhóm.</param>
/// <param name="CategoryId">Danh mục.</param>
/// <param name="Translations">Bộ bản dịch mới.</param>
/// <param name="Attributes">Bộ giá trị thuộc tính mới.</param>
public sealed record ProductUpdated(Guid ProductId, Guid ProductTypeId, Guid CategoryId, IReadOnlyList<ProductText> Translations,
    IReadOnlyList<AttributeValueInput> Attributes) : IDomainEvent;

/// <summary>Bộ sưu tập/thẻ của sản phẩm vừa được thay (ghi audit).</summary>
/// <param name="ProductId">Id.</param>
/// <param name="TaxonIds">Trọn danh sách mới.</param>
/// <param name="ChangedAt">Thời điểm.</param>
public sealed record ProductTaxaChanged(Guid ProductId, IReadOnlyList<Guid> TaxonIds, DateTimeOffset ChangedAt) : IDomainEvent;
