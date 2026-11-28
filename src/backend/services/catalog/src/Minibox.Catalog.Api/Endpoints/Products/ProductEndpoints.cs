using Mediator;
using Microsoft.AspNetCore.Mvc;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Web;
using Minibox.Catalog.Application;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.Products;
using Minibox.Catalog.Application.Features.Products.Commands.CreateProduct;
using Minibox.Catalog.Application.Features.Products.Commands.UpdateProduct;
using Minibox.Catalog.Application.Features.Products.Queries.GetProduct;

namespace Minibox.Catalog.Api.Endpoints.Products;

/// <summary>Thân request tạo sản phẩm nháp.</summary>
/// <param name="ProductTypeId">Nhóm.</param>
/// <param name="CategoryId">Danh mục.</param>
/// <param name="Translations">Bản dịch, bắt buộc có <c>vi</c>.</param>
/// <param name="Attributes">Giá trị thuộc tính mô tả.</param>
public sealed record CreateProductRequest(Guid ProductTypeId, Guid CategoryId, ProductTranslationDto[] Translations, ProductAttributeDto[] Attributes);

/// <summary>Thân request sửa sản phẩm nháp.</summary>
/// <param name="ProductTypeId">Nhóm.</param>
/// <param name="CategoryId">Danh mục.</param>
/// <param name="Translations">Bộ bản dịch mới (thay hết bộ cũ).</param>
/// <param name="Attributes">Bộ giá trị thuộc tính mới (thay hết bộ cũ).</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateProductRequest(Guid ProductTypeId, Guid CategoryId, ProductTranslationDto[] Translations, ProductAttributeDto[] Attributes, long Version);

/// <summary>Endpoint sản phẩm (Minimal API mỏng): request → command/query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class ProductEndpoints
{
    /// <summary>Map <c>POST /admin/products</c>, <c>GET|PUT /admin/products/{id}</c> (quyền <see cref="CatalogPermissions.ProductManage"/>).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/products").WithTags("Products")
            .WithMetadata(new RequirePermissionAttribute(CatalogPermissions.ProductManage));
        admin.MapPost("", async (CreateProductRequest body, [FromHeader(Name = IdempotencyKey.HeaderName)] string idempotencyKey,
                ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateProduct(body.ProductTypeId, body.CategoryId, body.Translations, body.Attributes, idempotencyKey), ct)).ToHttpResult())
            .Produces<Guid>();
        admin.MapGet("{id:guid}", async (Guid id, ISender sender, CancellationToken ct) => (await sender.Send(new GetProduct(id), ct)).ToHttpResult())
            .Produces<ProductAdminDetail>();
        admin.MapPut("{id:guid}", async (Guid id, UpdateProductRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateProduct(id, body.ProductTypeId, body.CategoryId, body.Translations, body.Attributes, body.Version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
}
