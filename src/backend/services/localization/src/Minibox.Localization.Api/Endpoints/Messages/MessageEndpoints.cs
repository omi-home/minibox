using Mediator;
using Microsoft.Net.Http.Headers;
using Minibox.BuildingBlocks.Web;
using Minibox.Localization.Application;
using Minibox.Localization.Application.Features.Messages.Commands.PublishMessages;
using Minibox.Localization.Application.Features.Messages.Commands.SaveMessageDraft;
using Minibox.Localization.Application.Features.Messages.Queries.GetPublishedMessages;
using Minibox.Localization.Application.Features.Messages.Queries.ListMessages;

namespace Minibox.Localization.Api.Endpoints.Messages;

/// <summary>Thân request lưu bản nháp chuỗi.</summary>
/// <param name="Text">Nội dung văn bản thuần (không phải HTML).</param>
/// <param name="Version">Phiên bản lúc tải khi sửa chuỗi đã có; bỏ trống khi tạo mới.</param>
public sealed record SaveMessageDraftRequest(string Text, long? Version);

/// <summary>Endpoint chuỗi giao diện (Minimal API mỏng): request → command/query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class MessageEndpoints
{
    /// <summary>Map <c>GET /messages/{locale}/{namespace}</c> (công khai, ETag/304) và API quản trị dưới <c>/admin/messages</c>.</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/messages/{locale}/{namespace}", async (string locale, string @namespace, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetPublishedMessages(locale, @namespace), ct);
                if (result.IsFailure)
                    return result.ToHttpResult();
                // no-cache: client/BFF được giữ bản sao nhưng phải hỏi lại bằng If-None-Match (rẻ: 304 không thân).
                http.Response.Headers.ETag = result.Value.ETag;
                http.Response.Headers.CacheControl = "no-cache";
                var etag = new EntityTagHeaderValue(result.Value.ETag);
                return http.Request.GetTypedHeaders().IfNoneMatch.Any(t => t.Equals(EntityTagHeaderValue.Any) || t.Compare(etag, useStrongComparison: false))
                    ? Results.StatusCode(StatusCodes.Status304NotModified)
                    : Results.Ok(result.Value.Body);
            })
            .WithTags("Messages")
            .Produces<PublishedMessagesDto>()
            .Produces(StatusCodes.Status304NotModified);

        var admin = endpoints.MapGroup("/admin/messages").WithTags("Messages");
        admin.MapGet("", [RequirePermission(LocalizationPermissions.MessageManage)] async (
                string @namespace, string? locale, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListMessages(@namespace, locale), ct)).ToHttpResult())
            .Produces<UiMessageDto[]>();
        admin.MapPut("{namespace}/{key}/{locale}", [RequirePermission(LocalizationPermissions.MessageManage)] async (
                string @namespace, string key, string locale, SaveMessageDraftRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SaveMessageDraft(@namespace, key, locale, body.Text, body.Version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        admin.MapPost("{namespace}/{locale}/publish", [RequirePermission(LocalizationPermissions.MessagePublish)] async (
                string @namespace, string locale, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PublishMessages(@namespace, locale), ct)).ToHttpResult())
            .Produces<PublishMessagesResult>();
        return endpoints;
    }
}
