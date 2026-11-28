using Mediator;
using Microsoft.AspNetCore.Mvc;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Web;
using Minibox.Catalog.Application;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.Taxa;
using Minibox.Catalog.Application.Features.Taxa.Commands.ArchiveTaxon;
using Minibox.Catalog.Application.Features.Taxa.Commands.CreateTaxon;
using Minibox.Catalog.Application.Features.Taxa.Commands.SetProductTaxa;
using Minibox.Catalog.Application.Features.Taxa.Commands.UpdateTaxon;
using Minibox.Catalog.Application.Features.Taxa.Queries.GetTaxa;
using Minibox.Catalog.Application.Features.Taxa.Queries.ListTaxa;

namespace Minibox.Catalog.Api.Endpoints.Taxa;

/// <summary>Thân request tạo bộ sưu tập/thẻ.</summary>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="Translations">Bản dịch, bắt buộc có <c>vi</c>.</param>
public sealed record CreateTaxonRequest(int SortOrder, TaxonTranslationDto[] Translations);

/// <summary>Thân request sửa bộ sưu tập/thẻ.</summary>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Translations">Bộ bản dịch mới (thay hết bộ cũ).</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateTaxonRequest(int SortOrder, TaxonTranslationDto[] Translations, long Version);

/// <summary>Thân request đặt bộ sưu tập/thẻ của sản phẩm.</summary>
/// <param name="Ids">Trọn danh sách mới; rỗng = gỡ hết.</param>
public sealed record SetProductTaxaRequest(Guid[] Ids);

/// <summary>Endpoint bộ sưu tập và thẻ (Minimal API mỏng): request → command/query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class TaxonEndpoints
{
    /// <summary>Map cho <c>collections</c> và <c>tags</c>: <c>GET /{loại}?locale=</c> (công khai), <c>GET|POST /admin/{loại}</c>,
    /// <c>PUT|DELETE /admin/{loại}/{id}</c>, <c>PUT /admin/products/{productId}/{loại}</c> (quyền <see cref="CatalogPermissions.CollectionManage"/>).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapTaxonEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Giá trị loại khớp TaxonKinds ở Domain (Api không tham chiếu Domain); validator của lệnh chặn giá trị lạ.
        Map(endpoints, "collections", "collection", "Collections");
        Map(endpoints, "tags", "tag", "Tags");
        return endpoints;
    }

    static void Map(IEndpointRouteBuilder endpoints, string path, string kind, string tag)
    {
        endpoints.MapGroup($"/{path}").WithTags(tag)
            .MapGet("", async (string? locale, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetTaxa(kind, locale ?? "vi"), ct)).ToHttpResult())
            .Produces<TaxonView[]>();

        var admin = endpoints.MapGroup($"/admin/{path}").WithTags(tag)
            .WithMetadata(new RequirePermissionAttribute(CatalogPermissions.CollectionManage));
        admin.MapGet("", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListTaxa(kind), ct)).ToHttpResult())
            .Produces<TaxonAdminRow[]>();
        admin.MapPost("", async (CreateTaxonRequest body, [FromHeader(Name = IdempotencyKey.HeaderName)] string idempotencyKey,
                ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateTaxon(kind, body.SortOrder, body.Translations, idempotencyKey), ct)).ToHttpResult())
            .Produces<Guid>();
        admin.MapPut("{id:guid}", async (Guid id, UpdateTaxonRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateTaxon(kind, id, body.SortOrder, body.Translations, body.Version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        admin.MapDelete("{id:guid}", async (Guid id, long version, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ArchiveTaxon(kind, id, version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);

        endpoints.MapPut($"/admin/products/{{productId:guid}}/{path}", async (Guid productId, SetProductTaxaRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SetProductTaxa(kind, productId, body.Ids), ct)).ToHttpResult())
            .WithTags(tag).WithMetadata(new RequirePermissionAttribute(CatalogPermissions.CollectionManage))
            .Produces(StatusCodes.Status204NoContent);
    }
}
