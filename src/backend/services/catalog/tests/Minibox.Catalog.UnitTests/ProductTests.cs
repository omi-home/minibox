using Minibox.Catalog.Domain.Aggregates.ProductTypes;
using Minibox.Catalog.Domain.Aggregates.Products;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.UnitTests;

/// <summary>Bất biến của <see cref="Product"/>: bản dịch/slug/SEO, giá trị thuộc tính theo bộ thuộc tính của nhóm, thuộc tính bắt buộc, liên kết taxon.</summary>
public sealed class ProductTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);
    static readonly Guid Category = Guid.CreateVersion7();

    static LocalizedLabel[] Vi(string label) => [new("vi", label)];

    static ProductType Lamp() => ProductType.Create("lamp", 0, Vi("Đèn"),
    [
        new("power-source", AttributeTypes.Select, null, true, false, Vi("Nguồn"), [new("usb", Vi("USB")), new("battery", Vi("Pin"))]),
        new("wattage", AttributeTypes.Number, "W", true, false, Vi("Công suất"), []),
        new("dimmable", AttributeTypes.Boolean, null, false, false, Vi("Chỉnh sáng"), []),
        new("material", AttributeTypes.Text, null, false, false, Vi("Chất liệu"), []),
        new("rooms", AttributeTypes.MultiSelect, null, false, false, Vi("Phòng"), [new("bedroom", Vi("Ngủ")), new("office", Vi("Làm việc"))]),
        new("color", AttributeTypes.Select, null, true, true, Vi("Màu"), [new("white", Vi("Trắng"))]),
    ], Now).Value;

    static ProductText T(string slug = "den-ngu", string name = "Đèn ngủ") => new("vi", name, null, slug, null, null);

    static AttributeValueInput V(string code, params string[] values) => new(code, values);

    static Product New(ProductType type, params AttributeValueInput[] values) =>
        Product.Create(type, Category, [T()], values, Now).Value;

    [Fact(DisplayName = "Create ở trạng thái nháp, gắn nhóm + danh mục; chuẩn hoá giá trị thuộc tính theo kiểu")]
    public void create_normalizes_values()
    {
        var type = Lamp();
        var p = New(type, V("power-source", "usb"), V("wattage", "5.50"), V("dimmable", "true"), V("material", "  Gốm "), V("rooms", "office", "bedroom"));
        Assert.Equal((ProductStatuses.Draft, type.Id, Category, 7), (p.Status, p.ProductTypeId, p.CategoryId, p.Id.Version));
        var byCode = type.Attributes.ToDictionary(a => a.Id, a => a.Code);
        Assert.Equal(["dimmable=true", "material=Gốm", "power-source=usb", "rooms=bedroom", "rooms=office", "wattage=5.5"],
            p.AttributeValues.Select(v => $"{byCode[v.AttributeId]}={v.Value}").Order(StringComparer.Ordinal));
    }

    public static TheoryData<AttributeValueInput> BadValues => new()
    {
        V("unknown", "x"),
        V("power-source", "solar"),            // không trong giá trị cho phép
        V("power-source", "usb", "battery"),   // select chỉ một
        V("wattage", "năm"),
        V("dimmable", "yes"),
        V("material", " "),
        V("material", new string('x', 2001)),
        V("rooms"),
        V("color", "white"),                   // thuộc tính tạo biến thể đặt ở biến thể, không ở sản phẩm
    };

    [Theory(DisplayName = "Create từ chối giá trị thuộc tính sai kiểu, ngoài bộ thuộc tính, hoặc của thuộc tính tạo biến thể")]
    [MemberData(nameof(BadValues))]
    public void create_rejects_bad_value(AttributeValueInput value) =>
        Assert.Equal(ProductErrors.AttributeValueInvalid, Product.Create(Lamp(), Category, [T()], [value], Now).Error);

    [Fact(DisplayName = "Một thuộc tính chỉ khai báo một lần")]
    public void attribute_once() =>
        Assert.Equal(ProductErrors.AttributeValueInvalid, Product.Create(Lamp(), Category, [T()], [V("wattage", "5"), V("wattage", "6")], Now).Error);

    [Fact(DisplayName = "Thiếu thuộc tính bắt buộc (mô tả) của nhóm được liệt kê; thuộc tính tạo biến thể không tính ở cấp sản phẩm")]
    public void missing_required()
    {
        var type = Lamp();
        Assert.Equal(["power-source", "wattage"], New(type).MissingRequiredAttributes(type));
        Assert.Empty(New(type, V("power-source", "usb"), V("wattage", "5")).MissingRequiredAttributes(type));
    }

    [Theory(DisplayName = "Create từ chối bản dịch sai: thiếu vi, slug sai dạng, SEO quá dài")]
    [InlineData("vi", "den ngu", null)]
    [InlineData("en", "lamp", null)]
    [InlineData("vi", "den-ngu", 201)]
    public void create_rejects_bad_text(string locale, string slug, int? seoTitleLength)
    {
        var text = new ProductText(locale, "Đèn", null, slug, seoTitleLength is { } n ? new string('x', n) : null, null);
        Assert.True(Product.Create(Lamp(), Category, [text], [], Now).IsFailure);
    }

    [Fact(DisplayName = "Update đổi slug lưu lịch sử, thay giá trị thuộc tính; đổi nhóm thì kiểm lại theo nhóm mới")]
    public void update_replaces()
    {
        var type = Lamp();
        var p = New(type, V("wattage", "5"));
        Assert.True(p.Update(type, Category, [T("den-ngu-moi")], [V("wattage", "7")], Now).IsSuccess);
        Assert.Equal([("vi", "den-ngu")], p.SlugHistory.Select(h => (h.Locale, h.Slug)));
        Assert.Equal("7", p.AttributeValues.Single().Value);

        var pot = ProductType.Create("pot", 0, Vi("Chậu"), [new("drainage", AttributeTypes.Boolean, null, true, false, Vi("Thoát nước"), [])], Now).Value;
        Assert.Equal(ProductErrors.AttributeValueInvalid, p.Update(pot, Category, [T()], [V("wattage", "7")], Now).Error);
        Assert.True(p.Update(pot, Category, [T()], [V("drainage", "false")], Now).IsSuccess);
        Assert.Equal(pot.Id, p.ProductTypeId);
    }

    [Fact(DisplayName = "Đặt taxon thay trọn danh sách, bỏ trùng, phát sự kiện")]
    public void set_taxa()
    {
        var p = New(Lamp());
        var (a, b) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        p.SetTaxa([a, b, b], Now);
        Assert.Equal([a, b], p.TaxonIds);
        p.SetTaxa([b], Now);
        Assert.Equal([b], p.TaxonIds);
        Assert.Equal(3, p.DomainEvents.Count);
    }

    [Fact(DisplayName = "Nhóm sản phẩm: không bỏ/đổi kiểu/chuyển sang tạo biến thể thuộc tính đang có giá trị, không bỏ giá trị cho phép đang dùng")]
    public void product_type_blocks_used_attribute()
    {
        var type = Lamp();
        var power = type.Attributes.Single(a => a.Code == "power-source");
        var inUse = new[] { new AttributeUsage(power.Id, "usb") };
        var specs = type.Attributes.Select(a => new AttributeSpec(a.Code, a.DataType, a.Unit, a.Required, a.Variant,
            a.Translations.Select(t => new LocalizedLabel(t.Locale, t.Label)).ToArray(),
            a.Values.Select(v => new AttributeValueSpec(v.Code, v.Translations.Select(t => new LocalizedLabel(t.Locale, t.Label)).ToArray())).ToArray())).ToArray();
        AttributeSpec[] With(Func<AttributeSpec, AttributeSpec?> change) => specs.Select(s => s.Code == "power-source" ? change(s) : s).OfType<AttributeSpec>().ToArray();

        Assert.Equal(ProductTypeErrors.AttributeInUse, type.Update(0, Vi("Đèn"), With(_ => null), Now, inUse).Error);
        Assert.Equal(ProductTypeErrors.AttributeInUse, type.Update(0, Vi("Đèn"), With(s => s with { DataType = AttributeTypes.MultiSelect }), Now, inUse).Error);
        Assert.Equal(ProductTypeErrors.AttributeInUse, type.Update(0, Vi("Đèn"), With(s => s with { Variant = true }), Now, inUse).Error);
        Assert.Equal(ProductTypeErrors.AttributeInUse, type.Update(0, Vi("Đèn"), With(s => s with { Values = [s.Values[1]] }), Now, inUse).Error);
        Assert.True(type.Update(0, Vi("Đèn"), With(s => s with { Values = [s.Values[0]], Required = false }), Now, inUse).IsSuccess);
    }
}
