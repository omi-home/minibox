using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;

namespace Minibox.Catalog.Application.Features.ProductTypes.Queries.ListProductTypes;

/// <summary>Truy vấn danh sách quản trị nhóm sản phẩm kèm bộ thuộc tính (kể cả đã xóa, mọi bản dịch); cần quyền <see cref="CatalogPermissions.AttributeManage"/>.</summary>
public sealed record ListProductTypes : IQuery<ProductTypeAdminRow[]>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.AttributeManage;
}

/// <summary>Đọc qua <see cref="IProductTypeReadService"/>, sắp theo thứ tự rồi mã.</summary>
/// <param name="reader">Port đọc.</param>
public sealed class ListProductTypesHandler(IProductTypeReadService reader) : IQueryHandler<ListProductTypes, ProductTypeAdminRow[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<ProductTypeAdminRow[]>> Handle(ListProductTypes query, CancellationToken cancellationToken) =>
        (await reader.ListAllAsync(cancellationToken)).OrderBy(r => r.SortOrder).ThenBy(r => r.Code, StringComparer.Ordinal).ToArray();
}
