using Mediator;
using Minibox.BuildingBlocks.Web;
using Minibox.Localization.Application.Features.Locales.Queries.ListLocales;

namespace Minibox.Localization.Api.Endpoints.Locales;

/// <summary>Endpoint locale (Minimal API mỏng): query qua <see cref="ISender"/> → <c>Result</c> → HTTP/ProblemDetails.</summary>
public static class LocaleEndpoints
{
    /// <summary>Map <c>GET /locales</c> (locale đang bật, theo thứ tự hiển thị).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapLocaleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/locales").WithTags("Locales")
            .MapGet("", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListLocales(), ct)).ToHttpResult())
            .Produces<LocaleDto[]>();
        return endpoints;
    }
}
