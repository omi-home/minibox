using Mediator;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Application.Features.Accounts.Commands.UpdateMyPreferences;
using Minibox.Identity.Application.Features.Accounts.Queries.GetMyPreferences;

namespace Minibox.Identity.Api.Endpoints.Me;

/// <summary>Thân request đổi tùy chọn hiển thị.</summary>
/// <param name="Locale">Locale BCP 47; <c>null</c> = bỏ chọn.</param>
/// <param name="TimeZone">Mã múi giờ IANA.</param>
public sealed record PreferencesRequest(string? Locale, string TimeZone);

/// <summary>Endpoint của chính người đang đăng nhập (Minimal API mỏng).</summary>
public static class MeEndpoints
{
    /// <summary>Map <c>GET|PUT /me/preferences</c> (cần đăng nhập; tài khoản lấy từ <c>sub</c>).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/me").WithTags("Me").RequireAuthorization();
        me.MapGet("preferences", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMyPreferences(), ct)).ToHttpResult())
            .Produces<PreferencesDto>();
        me.MapPut("preferences", async (PreferencesRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateMyPreferences(body.Locale, body.TimeZone), ct)).ToHttpResult())
            .Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
}
