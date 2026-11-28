using Minibox.Catalog.Application.Features.Categories;
using Minibox.Catalog.Domain.Aggregates.Categories;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.UnitTests;

/// <summary>Bất biến của <see cref="Category"/>: bản dịch theo locale, slug và lịch sử slug, chặn vòng lặp cha–con, xóa mềm.</summary>
public sealed class CategoryTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 28, 0, 0, 0, TimeSpan.Zero);

    static CategoryText Vi(string slug = "den-ngu", string name = "Đèn ngủ") => new("vi", name, null, slug);
    static CategoryText En(string slug = "night-lights") => new("en", "Night lights", "Lamps", slug);

    static Category New(Guid? parent = null, params CategoryText[] texts) =>
        Category.Create(parent, 0, texts.Length == 0 ? [Vi()] : texts, Now).Value;

    [Fact(DisplayName = "Create cần bản dịch vi, sinh UUIDv7, cắt khoảng trắng tên")]
    public void create_requires_vi()
    {
        Assert.Equal(CategoryErrors.DefaultTranslationMissing, Category.Create(null, 0, [En()], Now).Error);
        var c = Category.Create(null, 0, [Vi(name: "  Đèn ngủ "), En()], Now).Value;
        Assert.Equal(7, c.Id.Version);
        Assert.Equal("Đèn ngủ", c.Translations.Single(t => t.Locale == "vi").Name);
    }

    [Theory(DisplayName = "Create từ chối bản dịch sai: locale, tên rỗng, slug có hoa/khoảng trắng/gạch chéo, locale trùng")]
    [InlineData("VI", "Đèn", "den")]
    [InlineData("vi", " ", "den")]
    [InlineData("vi", "Đèn", "Den")]
    [InlineData("vi", "Đèn", "den ngu")]
    [InlineData("vi", "Đèn", "den/ngu")]
    [InlineData("vi", "Đèn", "-den")]
    public void create_rejects_bad_text(string locale, string name, string slug) =>
        Assert.Equal(CategoryErrors.TranslationInvalid, Category.Create(null, 0, [new(locale, name, null, slug), Vi()], Now).Error);

    [Fact(DisplayName = "Slug theo ngôn ngữ chấp nhận chữ không Latin; locale trùng bị từ chối")]
    public void slug_unicode_and_duplicate_locale()
    {
        Assert.True(Category.Create(null, 0, [Vi(), new("ja", "照明", null, "照明-ランプ")], Now).IsSuccess);
        Assert.Equal(CategoryErrors.TranslationInvalid, Category.Create(null, 0, [Vi(), Vi("khac")], Now).Error);
    }

    [Fact(DisplayName = "Đổi slug lưu slug cũ vào lịch sử; đổi lại slug cũ thì gỡ khỏi lịch sử; bỏ locale cũng lưu slug của nó")]
    public void slug_change_keeps_history()
    {
        var c = New(null, Vi(), En());
        Assert.True(c.Update(1, [Vi("den-ngu-moi"), En()], Now).IsSuccess);
        Assert.Equal([("vi", "den-ngu")], c.SlugHistory.Select(h => (h.Locale, h.Slug)));

        Assert.True(c.Update(1, [Vi("den-ngu")], Now).IsSuccess);
        Assert.Equal([("vi", "den-ngu-moi"), ("en", "night-lights")], c.SlugHistory.Select(h => (h.Locale, h.Slug)));
        Assert.Equal(["vi"], c.Translations.Select(t => t.Locale));
        Assert.Equal(1, c.SortOrder);
    }

    [Fact(DisplayName = "ac236_1: không di chuyển được danh mục vào chính nó hoặc vào con/cháu của nó")]
    public void ac236_1_move_rejects_cycle()
    {
        var root = New();
        var child = New(root.Id);
        var grandchild = New(child.Id);
        var parents = new Dictionary<Guid, Guid?> { [root.Id] = null, [child.Id] = root.Id, [grandchild.Id] = child.Id };

        Assert.Equal(CategoryErrors.Cycle, root.MoveTo(root.Id, parents, Now).Error);
        Assert.Equal(CategoryErrors.Cycle, root.MoveTo(grandchild.Id, parents, Now).Error);
        Assert.Null(root.ParentId);

        Assert.True(grandchild.MoveTo(root.Id, parents, Now).IsSuccess);
        Assert.Equal(root.Id, grandchild.ParentId);
        Assert.True(child.MoveTo(null, parents, Now).IsSuccess);
    }

    [Fact(DisplayName = "ac236_1: dữ liệu cha–con sẵn có vòng thì MoveTo trả lỗi thay vì lặp vô hạn")]
    public void ac236_1_move_survives_corrupt_tree()
    {
        var c = New();
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7();
        Assert.Equal(CategoryErrors.Cycle, c.MoveTo(a, new Dictionary<Guid, Guid?> { [a] = b, [b] = a, [c.Id] = null }, Now).Error);
    }

    [Fact(DisplayName = "Xóa mềm: còn con đang hoạt động thì 422; đã xóa thì không sửa/di chuyển được")]
    public void archive_rules()
    {
        var c = New();
        Assert.Equal(CategoryErrors.HasActiveChildren, c.Archive(hasActiveChildren: true, Now).Error);
        Assert.True(c.Archive(hasActiveChildren: false, Now).IsSuccess);
        Assert.True(c.IsArchived);
        Assert.Equal(CategoryErrors.Archived, c.Update(0, [Vi()], Now).Error);
        Assert.Equal(CategoryErrors.Archived, c.MoveTo(null, new Dictionary<Guid, Guid?>(), Now).Error);
    }

    [Fact(DisplayName = "Dựng cây: con theo cha, sắp theo thứ tự rồi tên; nút mồ côi bị bỏ")]
    public void tree_builder()
    {
        Guid r1 = Guid.CreateVersion7(), r2 = Guid.CreateVersion7(), c1 = Guid.CreateVersion7(), orphan = Guid.CreateVersion7();
        var rows = new (Guid Id, Guid? Parent, int Order, string Name)[]
        {
            (c1, r1, 0, "con"), (r2, null, 0, "B"), (r1, null, 0, "A"), (orphan, Guid.CreateVersion7(), 0, "x"),
        };
        var tree = CategoryTree.Build(rows, r => r.Id, r => r.Parent, r => (r.Order, r.Name),
            ((Guid Id, Guid? Parent, int Order, string Name) r, Node[] children) => new Node(r.Name, children));
        Assert.Equal(["A", "B"], tree.Select(n => n.Name));
        Assert.Equal("con", Assert.Single(tree[0].Children).Name);
    }

    sealed record Node(string Name, Node[] Children);
}
