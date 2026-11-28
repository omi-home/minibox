using Minibox.Catalog.Domain.Aggregates.Taxa;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.UnitTests;

/// <summary>Bất biến của <see cref="Taxon"/> (bộ sưu tập/thẻ): loại, bản dịch theo locale, lịch sử slug, xóa mềm.</summary>
public sealed class TaxonTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

    static TaxonText Vi(string slug = "qua-tang", string name = "Quà tặng") => new("vi", name, null, slug);
    static TaxonText En(string slug = "gifts") => new("en", "Gifts", "Gift ideas", slug);

    static Taxon New(string kind = TaxonKinds.Collection, params TaxonText[] texts) =>
        Taxon.Create(kind, 0, texts.Length == 0 ? [Vi()] : texts, Now).Value;

    [Fact(DisplayName = "Create nhận collection/tag, từ chối loại khác; cần bản dịch vi; sinh UUIDv7")]
    public void create_checks_kind_and_vi()
    {
        Assert.Equal(TaxonErrors.KindInvalid, Taxon.Create("category", 0, [Vi()], Now).Error);
        Assert.Equal(TaxonErrors.DefaultTranslationMissing, Taxon.Create(TaxonKinds.Tag, 0, [En()], Now).Error);
        var t = New(TaxonKinds.Tag, Vi(name: "  Quà tặng "), En());
        Assert.Equal((TaxonKinds.Tag, 7, "Quà tặng"), (t.Kind, t.Id.Version, t.Translations.Single(x => x.Locale == "vi").Name));
    }

    [Theory(DisplayName = "Create từ chối bản dịch sai: locale, tên rỗng, slug hoa/khoảng trắng/gạch chéo, locale trùng")]
    [InlineData("VI", "Quà", "qua")]
    [InlineData("vi", " ", "qua")]
    [InlineData("vi", "Quà", "Qua")]
    [InlineData("vi", "Quà", "qua tang")]
    [InlineData("vi", "Quà", "qua/tang")]
    [InlineData("vi", "Quà", "qua-tang")] // trùng locale vi
    public void create_rejects_bad_text(string locale, string name, string slug) =>
        Assert.Equal(TaxonErrors.TranslationInvalid, Taxon.Create(TaxonKinds.Collection, 0, [new(locale, name, null, slug), Vi()], Now).Error);

    [Fact(DisplayName = "Đổi slug lưu slug cũ vào lịch sử; lấy lại slug cũ thì gỡ khỏi lịch sử; bỏ locale cũng lưu slug của nó")]
    public void slug_change_keeps_history()
    {
        var t = New(TaxonKinds.Collection, Vi(), En());
        Assert.True(t.Update(1, [Vi("qua-tang-moi")], Now).IsSuccess);
        Assert.Equal([("vi", "qua-tang"), ("en", "gifts")], t.SlugHistory.Select(h => (h.Locale, h.Slug)));
        Assert.Equal(1, t.SortOrder);

        Assert.True(t.Update(1, [Vi(), En()], Now).IsSuccess);
        Assert.Equal([("vi", "qua-tang-moi")], t.SlugHistory.Select(h => (h.Locale, h.Slug)));
    }

    [Fact(DisplayName = "Xóa mềm idempotent; đã xóa thì không sửa được")]
    public void archive_blocks_update()
    {
        var t = New();
        Assert.True(t.Archive(Now).IsSuccess);
        Assert.True(t.Archive(Now).IsSuccess);
        Assert.Equal(Now, t.ArchivedAt);
        Assert.Equal(TaxonErrors.Archived, t.Update(0, [Vi()], Now).Error);
        Assert.Equal(2, t.DomainEvents.Count); // Created + Archived một lần
    }
}
