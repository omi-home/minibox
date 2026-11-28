using Minibox.Catalog.Domain.Aggregates.ProductTypes;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.UnitTests;

/// <summary>Bất biến của <see cref="ProductType"/>: mã nhóm, bản dịch, bộ thuộc tính (kiểu, đơn vị, giá trị cho phép, bắt buộc, tạo biến thể).</summary>
public sealed class ProductTypeTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

    static LocalizedLabel[] Vi(string label) => [new("vi", label)];

    static AttributeSpec Power(bool variant = false) => new("power-source", AttributeTypes.Select, null, true, variant,
        [new("vi", "Nguồn điện"), new("en", "Power source")],
        [new("usb", Vi("USB")), new("battery", [new("vi", "Pin"), new("en", "Battery")])]);

    static AttributeSpec Wattage => new("wattage", AttributeTypes.Number, "W", false, false, Vi("Công suất"), []);

    static ProductType New(params AttributeSpec[] attributes) =>
        ProductType.Create("lamp", 0, Vi("Đèn"), attributes.Length == 0 ? [Power(), Wattage] : attributes, Now).Value;

    [Fact(DisplayName = "Create giữ thứ tự thuộc tính/giá trị, cờ bắt buộc và tạo biến thể, đơn vị; sinh UUIDv7")]
    public void create_keeps_attribute_set()
    {
        var t = New(Power(variant: true), Wattage);
        Assert.Equal(("lamp", 7), (t.Code, t.Id.Version));
        Assert.Equal(["power-source", "wattage"], t.Attributes.Select(a => a.Code));
        var power = t.Attributes[0];
        Assert.Equal((AttributeTypes.Select, true, true, 0), (power.DataType, power.Required, power.Variant, power.SortOrder));
        Assert.Equal(["usb", "battery"], power.Values.Select(v => v.Code));
        Assert.Equal("Battery", power.Values[1].Translations.Single(x => x.Locale == "en").Label);
        Assert.Equal(("W", false, 1), (t.Attributes[1].Unit, t.Attributes[1].Variant, t.Attributes[1].SortOrder));
    }

    [Theory(DisplayName = "Create từ chối mã nhóm sai dạng")]
    [InlineData("Lamp")]
    [InlineData("den led")]
    [InlineData("-lamp")]
    [InlineData("lamp\n")]
    public void create_rejects_bad_code(string code) =>
        Assert.Equal(ProductTypeErrors.CodeInvalid, ProductType.Create(code, 0, Vi("Đèn"), [], Now).Error);

    [Fact(DisplayName = "Nhóm, thuộc tính và giá trị đều cần nhãn vi")]
    public void labels_need_vi()
    {
        Assert.Equal(ProductTypeErrors.DefaultTranslationMissing, ProductType.Create("lamp", 0, [new("en", "Lamp")], [], Now).Error);
        Assert.Equal(ProductTypeErrors.DefaultTranslationMissing,
            ProductType.Create("lamp", 0, Vi("Đèn"), [Wattage with { Labels = [new("en", "Watt")] }], Now).Error);
        Assert.Equal(ProductTypeErrors.DefaultTranslationMissing,
            ProductType.Create("lamp", 0, Vi("Đèn"), [Power() with { Values = [new("usb", [new("en", "USB")])] }], Now).Error);
    }

    public static TheoryData<AttributeSpec> BadAttributes => new()
    {
        Wattage with { Code = "Watt" },
        Wattage with { DataType = "date" },
        Wattage with { DataType = AttributeTypes.Text },                 // đơn vị chỉ cho number
        Wattage with { Unit = new string('W', 21) },
        Wattage with { Values = [new("w5", Vi("5W"))] },                 // number không có giá trị cho phép
        Power() with { Values = [] },                                     // select cần giá trị
        Power() with { Values = [new("usb", Vi("USB")), new("usb", Vi("USB 2"))] },
        Power() with { Values = [new("USB", Vi("USB"))] },
        Wattage with { Labels = [new("vi", " ")] },
    };

    [Theory(DisplayName = "Create từ chối thuộc tính sai: mã, kiểu, đơn vị, giá trị cho phép, nhãn")]
    [MemberData(nameof(BadAttributes))]
    public void create_rejects_bad_attribute(AttributeSpec attribute) =>
        Assert.Equal(ProductTypeErrors.AttributeInvalid, ProductType.Create("lamp", 0, Vi("Đèn"), [attribute], Now).Error);

    [Fact(DisplayName = "Mã thuộc tính không trùng trong nhóm")]
    public void attribute_codes_unique() =>
        Assert.Equal(ProductTypeErrors.AttributeInvalid, ProductType.Create("lamp", 0, Vi("Đèn"), [Wattage, Wattage], Now).Error);

    [Theory(DisplayName = "Thuộc tính tạo biến thể phải là select (giá trị rời rạc)")]
    [InlineData(AttributeTypes.Number)]
    [InlineData(AttributeTypes.Text)]
    [InlineData(AttributeTypes.Boolean)]
    [InlineData(AttributeTypes.MultiSelect)]
    public void variant_requires_select(string type)
    {
        var spec = new AttributeSpec("size", type, null, false, true, Vi("Cỡ"),
            type == AttributeTypes.MultiSelect ? [new("s", Vi("S"))] : []);
        Assert.Equal(ProductTypeErrors.VariantRequiresSelect, ProductType.Create("pot", 0, Vi("Chậu"), [spec], Now).Error);
    }

    [Fact(DisplayName = "Update thay bộ thuộc tính theo mã: giữ Id thuộc tính/giá trị còn lại, thêm mới, bỏ cái không còn")]
    public void update_syncs_by_code()
    {
        var t = New();
        var powerId = t.Attributes[0].Id;
        var usbId = t.Attributes[0].Values[0].Id;
        var drainage = new AttributeSpec("drainage", AttributeTypes.Boolean, null, true, false, Vi("Thoát nước"), []);

        Assert.True(t.Update(2, [new("vi", "Đèn trang trí")],
            [drainage, Power(variant: true) with { Values = [new("solar", Vi("Năng lượng mặt trời")), new("usb", Vi("Cổng USB"))] }], Now).IsSuccess);

        Assert.Equal(["drainage", "power-source"], t.Attributes.Select(a => a.Code));
        var power = t.Attributes.Single(a => a.Code == "power-source");
        Assert.Equal((powerId, 1, true), (power.Id, power.SortOrder, power.Variant));
        Assert.Equal(["solar", "usb"], power.Values.Select(v => v.Code));
        Assert.Equal((usbId, 1, "Cổng USB"), (power.Values[1].Id, power.Values[1].SortOrder, power.Values[1].Translations.Single().Label));
        Assert.Equal((2, "Đèn trang trí"), (t.SortOrder, t.Translations.Single().Name));
        Assert.Equal(2, t.DomainEvents.Count);
    }

    [Fact(DisplayName = "Xóa mềm idempotent; đã xóa thì không sửa được")]
    public void archive_blocks_update()
    {
        var t = New();
        Assert.True(t.Archive(Now).IsSuccess);
        Assert.True(t.Archive(Now).IsSuccess);
        Assert.Equal(ProductTypeErrors.Archived, t.Update(0, Vi("Đèn"), [], Now).Error);
        Assert.Equal(2, t.DomainEvents.Count);
    }
}
