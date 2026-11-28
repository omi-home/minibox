using Minibox.Catalog.Domain.Aggregates.Categories;

namespace Minibox.Catalog.Application.Features.Categories;

/// <summary>Bản dịch của danh mục ở một locale, dùng cho request/response (Api không dùng kiểu Domain).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Slug">Slug theo ngôn ngữ.</param>
public sealed record CategoryTranslationDto(string Locale, string Name, string? Description, string Slug)
{
    /// <summary>Chuyển sang đầu vào của aggregate.</summary>
    /// <param name="items">Bản dịch nhận từ client; <c>null</c> coi như rỗng.</param>
    /// <returns>Bản dịch cho <see cref="Category"/>.</returns>
    public static CategoryText[] ToDomain(IEnumerable<CategoryTranslationDto>? items) =>
        items?.Select(t => new CategoryText(t.Locale, t.Name, t.Description, t.Slug)).ToArray() ?? [];

    /// <summary>Lấy bản dịch hiện hành (đã chuẩn hoá) của danh mục.</summary>
    /// <param name="category">Danh mục.</param>
    /// <returns>Bản dịch dạng đầu vào aggregate, để kiểm slug trùng.</returns>
    public static CategoryText[] Current(Category category) =>
        category.Translations.Select(t => new CategoryText(t.Locale, t.Name, t.Description, t.Slug)).ToArray();
}

/// <summary>Dựng cây từ danh sách phẳng có Id cha.</summary>
public static class CategoryTree
{
    /// <summary>Gom con theo cha, bắt đầu từ các gốc (cha <c>null</c>); nút có cha không nằm trong danh sách bị bỏ cùng nhánh của nó.</summary>
    /// <typeparam name="TRow">Dòng phẳng.</typeparam>
    /// <typeparam name="TNode">Nút kết quả.</typeparam>
    /// <typeparam name="TOrder">Khoá sắp xếp anh em.</typeparam>
    /// <param name="rows">Dòng phẳng.</param>
    /// <param name="id">Lấy Id.</param>
    /// <param name="parentId">Lấy Id cha.</param>
    /// <param name="order">Khoá sắp xếp anh em (tăng dần).</param>
    /// <param name="make">Dựng nút từ dòng và các con đã dựng.</param>
    /// <returns>Các nút gốc đã sắp.</returns>
    public static TNode[] Build<TRow, TNode, TOrder>(IEnumerable<TRow> rows, Func<TRow, Guid> id, Func<TRow, Guid?> parentId,
        Func<TRow, TOrder> order, Func<TRow, TNode[], TNode> make)
    {
        var byParent = rows.ToLookup(parentId);
        var visited = new HashSet<Guid>();
        TNode[] Level(Guid? parent) => byParent[parent].OrderBy(order)
            .Where(r => visited.Add(id(r))) // phòng dữ liệu hỏng có vòng
            .Select(r => make(r, Level(id(r)))).ToArray();
        return Level(null);
    }
}
