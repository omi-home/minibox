using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;

namespace Minibox.Catalog.Application.Features.Categories.Queries.ListCategoryTree;

/// <summary>Truy vấn cây danh mục quản trị (kể cả đã xóa, mọi bản dịch); cần quyền <see cref="CatalogPermissions.CategoryManage"/>.</summary>
public sealed record ListCategoryTree : IQuery<CategoryAdminNode[]>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CategoryManage;
}

/// <summary>Nút cây quản trị.</summary>
/// <param name="Id">Id.</param>
/// <param name="ParentId">Cha.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Archived">Đã xóa mềm.</param>
/// <param name="Version">Phiên bản gửi lại khi sửa/di chuyển/xóa.</param>
/// <param name="Translations">Mọi bản dịch.</param>
/// <param name="Children">Danh mục con.</param>
public sealed record CategoryAdminNode(Guid Id, Guid? ParentId, int SortOrder, bool Archived, long Version,
    CategoryTranslationDto[] Translations, CategoryAdminNode[] Children);

/// <summary>Đọc qua <see cref="ICategoryReadService"/> rồi dựng cây.</summary>
/// <param name="reader">Port đọc.</param>
public sealed class ListCategoryTreeHandler(ICategoryReadService reader) : IQueryHandler<ListCategoryTree, CategoryAdminNode[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<CategoryAdminNode[]>> Handle(ListCategoryTree query, CancellationToken cancellationToken) =>
        CategoryTree.Build(await reader.ListAllAsync(cancellationToken), r => r.Id, r => r.ParentId,
            r => (r.SortOrder, r.Translations.FirstOrDefault(t => t.Locale == LocaleChain.Default)?.Name ?? ""),
            (CategoryAdminRow r, CategoryAdminNode[] children) => new CategoryAdminNode(r.Id, r.ParentId, r.SortOrder, r.Archived, r.Version, r.Translations, children));
}
