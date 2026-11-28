using Mediator;
using Microsoft.AspNetCore.Mvc;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Web;
using Minibox.Catalog.Application;
using Minibox.Catalog.Application.Features.Categories;
using Minibox.Catalog.Application.Features.Categories.Commands.ArchiveCategory;
using Minibox.Catalog.Application.Features.Categories.Commands.CreateCategory;
using Minibox.Catalog.Application.Features.Categories.Commands.MoveCategory;
using Minibox.Catalog.Application.Features.Categories.Commands.UpdateCategory;
using Minibox.Catalog.Application.Features.Categories.Queries.GetCategoryTree;
using Minibox.Catalog.Application.Features.Categories.Queries.ListCategoryTree;

namespace Minibox.Catalog.Api.Endpoints.Categories;

/// <summary>Thân request tạo danh mục.</summary>
/// <param name="ParentId">Cha; <c>null</c> = gốc.</param>
/// <param name="SortOrder">Thứ tự trong cha.</param>
/// <param name="Translations">Bản dịch, bắt buộc có <c>vi</c>.</param>
public sealed record CreateCategoryRequest(Guid? ParentId, int SortOrder, CategoryTranslationDto[] Translations);

/// <summary>Thân request sửa danh mục.</summary>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Translations">Bộ bản dịch mới (thay hết bộ cũ).</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateCategoryRequest(int SortOrder, CategoryTranslationDto[] Translations, long Version);

/// <summary>Thân request di chuyển danh mục.</summary>
/// <param name="ParentId">Cha mới; <c>null</c> = lên gốc.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record MoveCategoryRequest(Guid? ParentId, long Version);

/// <summary>Endpoint danh mục (Minimal API mỏng): request → command/query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class CategoryEndpoints
{
    /// <summary>Map <c>GET /categories?locale=</c> (công khai) và <c>GET|POST /admin/categories</c>, <c>PUT|DELETE /admin/categories/{id}</c>,
    /// <c>PUT /admin/categories/{id}/parent</c> (quyền <see cref="CatalogPermissions.CategoryManage"/>).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/categories").WithTags("Categories")
            .MapGet("", async (string? locale, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetCategoryTree(locale ?? "vi"), ct)).ToHttpResult())
            .Produces<CategoryTreeNode[]>();

        var admin = endpoints.MapGroup("/admin/categories").WithTags("Categories")
            .WithMetadata(new RequirePermissionAttribute(CatalogPermissions.CategoryManage));
        admin.MapGet("", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListCategoryTree(), ct)).ToHttpResult())
            .Produces<CategoryAdminNode[]>();
        admin.MapPost("", async (CreateCategoryRequest body, [FromHeader(Name = IdempotencyKey.HeaderName)] string idempotencyKey,
                ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateCategory(body.ParentId, body.SortOrder, body.Translations, idempotencyKey), ct)).ToHttpResult())
            .Produces<Guid>();
        admin.MapPut("{id:guid}", async (Guid id, UpdateCategoryRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateCategory(id, body.SortOrder, body.Translations, body.Version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        admin.MapPut("{id:guid}/parent", async (Guid id, MoveCategoryRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new MoveCategory(id, body.ParentId, body.Version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        admin.MapDelete("{id:guid}", async (Guid id, long version, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ArchiveCategory(id, version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
}
