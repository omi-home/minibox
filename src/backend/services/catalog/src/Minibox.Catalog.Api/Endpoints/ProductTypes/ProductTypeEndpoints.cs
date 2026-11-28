using Mediator;
using Microsoft.AspNetCore.Mvc;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Web;
using Minibox.Catalog.Application;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.ProductTypes;
using Minibox.Catalog.Application.Features.ProductTypes.Commands.ArchiveProductType;
using Minibox.Catalog.Application.Features.ProductTypes.Commands.CreateProductType;
using Minibox.Catalog.Application.Features.ProductTypes.Commands.UpdateProductType;
using Minibox.Catalog.Application.Features.ProductTypes.Queries.ListProductTypes;

namespace Minibox.Catalog.Api.Endpoints.ProductTypes;

/// <summary>Thân request tạo nhóm sản phẩm.</summary>
/// <param name="Code">Mã nhóm (duy nhất, không đổi).</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="Names">Tên theo locale, bắt buộc có <c>vi</c>.</param>
/// <param name="Attributes">Bộ thuộc tính theo thứ tự.</param>
public sealed record CreateProductTypeRequest(string Code, int SortOrder, LabelDto[] Names, AttributeDto[] Attributes);

/// <summary>Thân request sửa nhóm sản phẩm.</summary>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Names">Tên mới (thay hết bộ cũ).</param>
/// <param name="Attributes">Bộ thuộc tính mới (khớp theo mã).</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateProductTypeRequest(int SortOrder, LabelDto[] Names, AttributeDto[] Attributes, long Version);

/// <summary>Endpoint nhóm sản phẩm và bộ thuộc tính (Minimal API mỏng): request → command/query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class ProductTypeEndpoints
{
    /// <summary>Map <c>GET|POST /admin/product-types</c>, <c>PUT|DELETE /admin/product-types/{id}</c> (quyền <see cref="CatalogPermissions.AttributeManage"/>).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapProductTypeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/product-types").WithTags("ProductTypes")
            .WithMetadata(new RequirePermissionAttribute(CatalogPermissions.AttributeManage));
        admin.MapGet("", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListProductTypes(), ct)).ToHttpResult())
            .Produces<ProductTypeAdminRow[]>();
        admin.MapPost("", async (CreateProductTypeRequest body, [FromHeader(Name = IdempotencyKey.HeaderName)] string idempotencyKey,
                ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateProductType(body.Code, body.SortOrder, body.Names, body.Attributes, idempotencyKey), ct)).ToHttpResult())
            .Produces<Guid>();
        admin.MapPut("{id:guid}", async (Guid id, UpdateProductTypeRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateProductType(id, body.SortOrder, body.Names, body.Attributes, body.Version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        admin.MapDelete("{id:guid}", async (Guid id, long version, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ArchiveProductType(id, version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
}
