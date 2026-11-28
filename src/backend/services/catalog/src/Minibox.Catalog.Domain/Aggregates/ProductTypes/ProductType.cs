using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Domain.Aggregates.ProductTypes;

/// <summary>Kiểu dữ liệu của thuộc tính (giá trị cột <c>attribute.data_type</c>).</summary>
public static class AttributeTypes
{
    /// <summary>Chữ tự do.</summary>
    public const string Text = "text";

    /// <summary>Số, có thể kèm đơn vị.</summary>
    public const string Number = "number";

    /// <summary>Có/không.</summary>
    public const string Boolean = "boolean";

    /// <summary>Chọn một trong các giá trị cho phép; kiểu duy nhất được tạo biến thể.</summary>
    public const string Select = "select";

    /// <summary>Chọn nhiều trong các giá trị cho phép.</summary>
    public const string MultiSelect = "multi_select";

    /// <summary>Mọi kiểu hợp lệ.</summary>
    public static readonly IReadOnlyList<string> All = [Text, Number, Boolean, Select, MultiSelect];

    /// <summary>Kiểm kiểu dùng danh sách giá trị cho phép.</summary>
    /// <param name="type">Kiểu cần kiểm.</param>
    /// <returns><c>true</c> với <see cref="Select"/>/<see cref="MultiSelect"/>.</returns>
    public static bool HasValues(string type) => type is Select or MultiSelect;
}

/// <summary>Nhãn ở một locale (đầu vào của <see cref="ProductType"/>).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Label">Nhãn hiển thị.</param>
public sealed record LocalizedLabel(string Locale, string Label);

/// <summary>Giá trị cho phép của thuộc tính select/multi_select.</summary>
/// <param name="Code">Mã ổn định trong thuộc tính.</param>
/// <param name="Labels">Nhãn theo locale, bắt buộc có <c>vi</c>.</param>
public sealed record AttributeValueSpec(string Code, IReadOnlyList<LocalizedLabel> Labels);

/// <summary>Định nghĩa một thuộc tính của nhóm (đầu vào của <see cref="ProductType.Create"/>/<see cref="ProductType.Update"/>).</summary>
/// <param name="Code">Mã ổn định trong nhóm.</param>
/// <param name="DataType">Kiểu (<see cref="AttributeTypes"/>).</param>
/// <param name="Unit">Đơn vị, chỉ cho <see cref="AttributeTypes.Number"/>; <c>null</c> = không có.</param>
/// <param name="Required">Sản phẩm thuộc nhóm phải có giá trị trước khi gửi duyệt.</param>
/// <param name="Variant">Thuộc tính tạo biến thể (mỗi tổ hợp bán được là một SKU); <c>false</c> = thuộc tính mô tả.</param>
/// <param name="Labels">Nhãn theo locale, bắt buộc có <c>vi</c>.</param>
/// <param name="Values">Giá trị cho phép theo thứ tự hiển thị; chỉ cho select/multi_select.</param>
public sealed record AttributeSpec(string Code, string DataType, string? Unit, bool Required, bool Variant,
    IReadOnlyList<LocalizedLabel> Labels, IReadOnlyList<AttributeValueSpec> Values);

/// <summary>Nhóm sản phẩm (đèn, chậu, thủy sinh…) và bộ thuộc tính của nó (BRD BR-CAT-01/02): bảng <c>product_type</c>, thuộc tính ở <c>attribute</c>,
/// giá trị cho phép ở <c>attribute_value</c>; nhãn theo locale ở các bảng <c>*_translation</c>.</summary>
/// <remarks>Thứ tự thuộc tính/giá trị là thứ tự trong danh sách gửi lên. Thuộc tính và giá trị khớp theo mã khi sửa nên giữ nguyên Id
/// (sản phẩm/biến thể tham chiếu Id). Kết thúc bằng xóa mềm.</remarks>
public sealed class ProductType : AggregateRoot<Guid>
{
    /// <summary>Độ dài tối đa của mã nhóm/thuộc tính/giá trị.</summary>
    public const int CodeMaxLength = 50;

    /// <summary>Độ dài tối đa của tên/nhãn.</summary>
    public const int LabelMaxLength = 200;

    /// <summary>Độ dài tối đa của đơn vị.</summary>
    public const int UnitMaxLength = 20;

    readonly List<ProductTypeTranslation> _translations = [];
    readonly List<ProductAttribute> _attributes = [];

    ProductType(Guid id) : base(id) { }

    /// <summary>Mã nhóm, duy nhất; không đổi sau khi tạo.</summary>
    public string Code { get; private set; } = "";

    /// <summary>Thứ tự hiển thị (nhỏ trước).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Thời điểm xóa mềm; <c>null</c> = đang hoạt động.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Phiên bản cho optimistic concurrency (cột <c>version</c>).</summary>
    public long Version { get; private set; }

    /// <summary>Cờ đã xóa mềm.</summary>
    public bool IsArchived => ArchivedAt is not null;

    /// <summary>Tên nhóm theo locale; luôn có <see cref="LocaleChain.Default"/>.</summary>
    public IReadOnlyList<ProductTypeTranslation> Translations => _translations;

    /// <summary>Bộ thuộc tính theo thứ tự hiển thị.</summary>
    public IReadOnlyList<ProductAttribute> Attributes => _attributes;

    /// <summary>Tạo nhóm sản phẩm kèm bộ thuộc tính; phát <see cref="ProductTypeCreated"/>.</summary>
    /// <param name="code">Mã nhóm.</param>
    /// <param name="sortOrder">Thứ tự hiển thị.</param>
    /// <param name="names">Tên theo locale, bắt buộc có <c>vi</c>.</param>
    /// <param name="attributes">Bộ thuộc tính (có thể rỗng).</param>
    /// <param name="now">Thời điểm tạo (sinh Id UUIDv7).</param>
    /// <returns>Nhóm mới; lỗi <see cref="ProductTypeErrors.CodeInvalid"/> hoặc lỗi kiểm bộ thuộc tính (xem <see cref="Update"/>).</returns>
    public static Result<ProductType> Create(string code, int sortOrder, IReadOnlyCollection<LocalizedLabel> names,
        IReadOnlyCollection<AttributeSpec> attributes, DateTimeOffset now)
    {
        if (!IsCode(code))
            return ProductTypeErrors.CodeInvalid;
        var type = new ProductType(Guid.CreateVersion7(now)) { Code = code };
        var set = type.Set(sortOrder, names, attributes, now);
        if (set.IsFailure)
            return set.Error;
        type.Raise(new ProductTypeCreated(type.Id, code, sortOrder, set.Value.Names, set.Value.Attributes));
        return type;
    }

    /// <summary>Sửa thứ tự, tên và toàn bộ bộ thuộc tính (khớp theo mã: giữ Id, thêm mới, bỏ cái không còn). Phát <see cref="ProductTypeUpdated"/>.</summary>
    /// <param name="sortOrder">Thứ tự mới.</param>
    /// <param name="names">Tên mới theo locale (thay hết bộ cũ), bắt buộc có <c>vi</c>.</param>
    /// <param name="attributes">Bộ thuộc tính mới (thay hết bộ cũ).</param>
    /// <param name="now">Thời điểm sửa (sinh Id cho thuộc tính/giá trị mới).</param>
    /// <returns>Thành công; lỗi <see cref="ProductTypeErrors.Archived"/>, <see cref="ProductTypeErrors.TranslationInvalid"/>,
    /// <see cref="ProductTypeErrors.DefaultTranslationMissing"/>, <see cref="ProductTypeErrors.AttributeInvalid"/> hoặc
    /// <see cref="ProductTypeErrors.VariantRequiresSelect"/>; lỗi thì không đổi gì.</returns>
    /// <remarks>ponytail: cho đổi kiểu/bỏ thuộc tính tự do vì chưa có sản phẩm; task Product chặn khi đã có giá trị/biến thể dùng tới.</remarks>
    public Result Update(int sortOrder, IReadOnlyCollection<LocalizedLabel> names, IReadOnlyCollection<AttributeSpec> attributes, DateTimeOffset now)
    {
        if (IsArchived)
            return ProductTypeErrors.Archived;
        var set = Set(sortOrder, names, attributes, now);
        if (set.IsFailure)
            return set.Error;
        Raise(new ProductTypeUpdated(Id, sortOrder, set.Value.Names, set.Value.Attributes));
        return Result.Success();
    }

    /// <summary>Xóa mềm; idempotent nếu đã xóa. Phát <see cref="ProductTypeArchived"/>.</summary>
    /// <param name="now">Thời điểm xóa.</param>
    /// <returns>Luôn thành công.</returns>
    public Result Archive(DateTimeOffset now)
    {
        if (IsArchived)
            return Result.Success();
        ArchivedAt = now;
        Raise(new ProductTypeArchived(Id, now));
        return Result.Success();
    }

    Result<(LocalizedLabel[] Names, AttributeSpec[] Attributes)> Set(int sortOrder, IReadOnlyCollection<LocalizedLabel> names,
        IReadOnlyCollection<AttributeSpec> attributes, DateTimeOffset now)
    {
        var cleanNames = Labels(names);
        if (cleanNames is null)
            return ProductTypeErrors.TranslationInvalid;
        if (cleanNames.All(n => n.Locale != LocaleChain.Default))
            return ProductTypeErrors.DefaultTranslationMissing;
        var cleanAttributes = Validate(attributes);
        if (cleanAttributes.IsFailure)
            return cleanAttributes.Error;

        SortOrder = sortOrder;
        Sync(_translations, cleanNames, t => t.Locale, n => n.Locale, (n, _) => new ProductTypeTranslation(n.Locale, n.Label), (t, n, _) => t.Name = n.Label);
        Sync(_attributes, cleanAttributes.Value, a => a.Code, s => s.Code, (s, i) =>
        {
            var a = new ProductAttribute(Guid.CreateVersion7(now), s.Code);
            a.Set(s, i, now);
            return a;
        }, (a, s, i) => a.Set(s, i, now));
        return (cleanNames, cleanAttributes.Value);
    }

    static Result<AttributeSpec[]> Validate(IReadOnlyCollection<AttributeSpec>? attributes)
    {
        var clean = new List<AttributeSpec>();
        foreach (var a in attributes ?? [])
        {
            if (a is null || !IsCode(a.Code) || clean.Any(c => c.Code == a.Code) || !AttributeTypes.All.Contains(a.DataType))
                return ProductTypeErrors.AttributeInvalid;
            var unit = string.IsNullOrWhiteSpace(a.Unit) ? null : a.Unit.Trim();
            if (unit is not null && (a.DataType != AttributeTypes.Number || unit.Length > UnitMaxLength))
                return ProductTypeErrors.AttributeInvalid;
            var labels = Labels(a.Labels);
            if (labels is null)
                return ProductTypeErrors.AttributeInvalid;
            var values = new List<AttributeValueSpec>();
            foreach (var v in a.Values ?? [])
            {
                var valueLabels = v is null || !IsCode(v.Code) || values.Any(c => c.Code == v.Code) ? null : Labels(v.Labels);
                if (valueLabels is null)
                    return ProductTypeErrors.AttributeInvalid;
                values.Add(new AttributeValueSpec(v!.Code, valueLabels));
            }
            if (AttributeTypes.HasValues(a.DataType) != (values.Count > 0))
                return ProductTypeErrors.AttributeInvalid;
            if (a.Variant && a.DataType != AttributeTypes.Select)
                return ProductTypeErrors.VariantRequiresSelect;
            if (labels.Concat(values.SelectMany(v => v.Labels)).Count(l => l.Locale == LocaleChain.Default) != 1 + values.Count)
                return ProductTypeErrors.DefaultTranslationMissing;
            clean.Add(a with { Unit = unit, Labels = labels, Values = values });
        }
        return clean.ToArray();
    }

    // Nhãn đã chuẩn hoá; null nếu sai dạng (locale, độ dài, locale lặp).
    static LocalizedLabel[]? Labels(IReadOnlyCollection<LocalizedLabel>? labels)
    {
        var clean = new List<LocalizedLabel>();
        foreach (var l in labels ?? [])
        {
            if (l is null || l.Locale is null || !TextRules.Locale().IsMatch(l.Locale) || string.IsNullOrWhiteSpace(l.Label)
                || l.Label.Trim().Length > LabelMaxLength || clean.Any(c => c.Locale == l.Locale))
                return null;
            clean.Add(l with { Label = l.Label.Trim() });
        }
        return clean.ToArray();
    }

    static bool IsCode(string? code) => code is not null && code.Length <= CodeMaxLength && TextRules.Code().IsMatch(code);

    // Đồng bộ danh sách con theo khoá, giữ instance (và Id) của khoá còn lại; thứ tự theo specs.
    internal static void Sync<TItem, TSpec>(List<TItem> items, IReadOnlyList<TSpec> specs, Func<TItem, string> itemKey, Func<TSpec, string> specKey,
        Func<TSpec, int, TItem> create, Action<TItem, TSpec, int> set)
    {
        var current = items.ToDictionary(itemKey);
        items.Clear();
        for (var i = 0; i < specs.Count; i++)
        {
            if (current.Remove(specKey(specs[i]), out var item))
                set(item, specs[i], i);
            else
                item = create(specs[i], i);
            items.Add(item);
        }
    }
}

/// <summary>Tên nhóm ở một locale (dòng <c>product_type_translation</c>, khoá <c>(product_type_id, locale)</c>).</summary>
public sealed class ProductTypeTranslation
{
    internal ProductTypeTranslation(string locale, string name) => (Locale, Name) = (locale, name);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; }

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; internal set; }
}

/// <summary>Thuộc tính của nhóm (dòng <c>attribute</c>, mã duy nhất trong nhóm).</summary>
public sealed class ProductAttribute
{
    readonly List<AttributeTranslation> _translations = [];
    readonly List<AttributeValue> _values = [];

    internal ProductAttribute(Guid id, string code) => (Id, Code) = (id, code);

    /// <summary>Id (UUIDv7), giữ nguyên khi sửa nhóm.</summary>
    public Guid Id { get; private set; }

    /// <summary>Mã ổn định trong nhóm.</summary>
    public string Code { get; private set; }

    /// <summary>Kiểu (<see cref="AttributeTypes"/>).</summary>
    public string DataType { get; private set; } = "";

    /// <summary>Đơn vị (chỉ kiểu number).</summary>
    public string? Unit { get; private set; }

    /// <summary>Bắt buộc có giá trị trước khi gửi duyệt sản phẩm.</summary>
    public bool Required { get; private set; }

    /// <summary>Tạo biến thể (chỉ kiểu select).</summary>
    public bool Variant { get; private set; }

    /// <summary>Thứ tự trong nhóm.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Nhãn theo locale.</summary>
    public IReadOnlyList<AttributeTranslation> Translations => _translations;

    /// <summary>Giá trị cho phép theo thứ tự (select/multi_select).</summary>
    public IReadOnlyList<AttributeValue> Values => _values;

    internal void Set(AttributeSpec s, int sortOrder, DateTimeOffset now)
    {
        (DataType, Unit, Required, Variant, SortOrder) = (s.DataType, s.Unit, s.Required, s.Variant, sortOrder);
        ProductType.Sync(_translations, s.Labels, t => t.Locale, l => l.Locale, (l, _) => new AttributeTranslation(l.Locale, l.Label), (t, l, _) => t.Label = l.Label);
        ProductType.Sync(_values, s.Values, v => v.Code, v => v.Code, (v, i) =>
        {
            var value = new AttributeValue(Guid.CreateVersion7(now), v.Code);
            value.Set(v, i);
            return value;
        }, (value, v, i) => value.Set(v, i));
    }
}

/// <summary>Nhãn thuộc tính ở một locale (dòng <c>attribute_translation</c>).</summary>
public sealed class AttributeTranslation
{
    internal AttributeTranslation(string locale, string label) => (Locale, Label) = (locale, label);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; }

    /// <summary>Nhãn hiển thị.</summary>
    public string Label { get; internal set; }
}

/// <summary>Giá trị cho phép của thuộc tính (dòng <c>attribute_value</c>, mã duy nhất trong thuộc tính).</summary>
public sealed class AttributeValue
{
    readonly List<AttributeValueTranslation> _translations = [];

    internal AttributeValue(Guid id, string code) => (Id, Code) = (id, code);

    /// <summary>Id (UUIDv7), giữ nguyên khi sửa nhóm.</summary>
    public Guid Id { get; private set; }

    /// <summary>Mã ổn định trong thuộc tính.</summary>
    public string Code { get; private set; }

    /// <summary>Thứ tự trong thuộc tính.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Nhãn theo locale.</summary>
    public IReadOnlyList<AttributeValueTranslation> Translations => _translations;

    internal void Set(AttributeValueSpec s, int sortOrder)
    {
        SortOrder = sortOrder;
        ProductType.Sync(_translations, s.Labels, t => t.Locale, l => l.Locale, (l, _) => new AttributeValueTranslation(l.Locale, l.Label), (t, l, _) => t.Label = l.Label);
    }
}

/// <summary>Nhãn giá trị cho phép ở một locale (dòng <c>attribute_value_translation</c>).</summary>
public sealed class AttributeValueTranslation
{
    internal AttributeValueTranslation(string locale, string label) => (Locale, Label) = (locale, label);

    /// <summary>Mã locale.</summary>
    public string Locale { get; private set; }

    /// <summary>Nhãn hiển thị.</summary>
    public string Label { get; internal set; }
}

/// <summary>Nhóm sản phẩm vừa được tạo (ghi audit).</summary>
/// <param name="ProductTypeId">Id.</param>
/// <param name="Code">Mã nhóm.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Names">Tên theo locale.</param>
/// <param name="Attributes">Bộ thuộc tính.</param>
public sealed record ProductTypeCreated(Guid ProductTypeId, string Code, int SortOrder, IReadOnlyList<LocalizedLabel> Names,
    IReadOnlyList<AttributeSpec> Attributes) : IDomainEvent;

/// <summary>Nhóm sản phẩm vừa được sửa tên/bộ thuộc tính (ghi audit).</summary>
/// <param name="ProductTypeId">Id.</param>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Names">Tên mới.</param>
/// <param name="Attributes">Bộ thuộc tính mới.</param>
public sealed record ProductTypeUpdated(Guid ProductTypeId, int SortOrder, IReadOnlyList<LocalizedLabel> Names,
    IReadOnlyList<AttributeSpec> Attributes) : IDomainEvent;

/// <summary>Nhóm sản phẩm vừa bị xóa mềm (ghi audit).</summary>
/// <param name="ProductTypeId">Id.</param>
/// <param name="ArchivedAt">Thời điểm.</param>
public sealed record ProductTypeArchived(Guid ProductTypeId, DateTimeOffset ArchivedAt) : IDomainEvent;
