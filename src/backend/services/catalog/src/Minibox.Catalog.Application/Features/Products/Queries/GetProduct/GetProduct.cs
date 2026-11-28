using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Products.Queries.GetProduct;

/// <summary>Truy vấn chi tiết sản phẩm cho trang quản trị; cần quyền <see cref="CatalogPermissions.ProductManage"/>.</summary>
/// <param name="Id">Sản phẩm.</param>
public sealed record GetProduct(Guid Id) : IQuery<ProductAdminDetail>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.ProductManage;
}

/// <summary>Đọc qua <see cref="IProductReadService"/>.</summary>
/// <param name="reader">Port đọc.</param>
public sealed class GetProductHandler(IProductReadService reader) : IQueryHandler<GetProduct, ProductAdminDetail>
{
    /// <inheritdoc/>
    /// <returns>Chi tiết; lỗi <see cref="ProductErrors.NotFound"/>.</returns>
    public async ValueTask<Result<ProductAdminDetail>> Handle(GetProduct query, CancellationToken cancellationToken) =>
        await reader.GetAsync(query.Id, cancellationToken) is { } detail ? detail : ProductErrors.NotFound;
}
