using Mediator;
using Minibox.BuildingBlocks.Web;
using Minibox.Localization.Application;
using Minibox.Localization.Application.Features.Locales.Commands.CreateLocale;
using Minibox.Localization.Application.Features.Locales.Commands.UpdateLocale;
using Minibox.Localization.Application.Features.Locales.Queries.ListAllLocales;
using Minibox.Localization.Application.Features.Locales.Queries.ListLocales;

namespace Minibox.Localization.Api.Endpoints.Locales;

/// <summary>Thân request thêm ngôn ngữ.</summary>
/// <param name="Code">Mã BCP 47.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <c>vi</c>.</param>
public sealed record CreateLocaleRequest(string Code, string Name, int SortOrder, string? FallbackCode);

/// <summary>Thân request sửa ngôn ngữ.</summary>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="Enabled">Bật cho người dùng chọn.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <c>vi</c>.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateLocaleRequest(string Name, bool Enabled, int SortOrder, string? FallbackCode, long Version);

/// <summary>Endpoint locale (Minimal API mỏng): request → command/query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class LocaleEndpoints
{
    /// <summary>Map <c>GET /locales</c> (công khai: locale đang bật) và <c>GET|POST /admin/locales</c>, <c>PUT /admin/locales/{code}</c>.</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapLocaleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/locales").WithTags("Locales")
            .MapGet("", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListLocales(), ct)).ToHttpResult())
            .Produces<LocaleDto[]>();

        var admin = endpoints.MapGroup("/admin/locales").WithTags("Locales");
        admin.MapGet("", [RequirePermission(LocalizationPermissions.LocaleManage)] async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListAllLocales(), ct)).ToHttpResult())
            .Produces<LocaleAdminDto[]>();
        admin.MapPost("", [RequirePermission(LocalizationPermissions.LocaleManage)] async (
                CreateLocaleRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateLocale(body.Code, body.Name, body.SortOrder, body.FallbackCode), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        admin.MapPut("{code}", [RequirePermission(LocalizationPermissions.LocaleManage)] async (
                string code, UpdateLocaleRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateLocale(code, body.Name, body.Enabled, body.SortOrder, body.FallbackCode, body.Version), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
}
