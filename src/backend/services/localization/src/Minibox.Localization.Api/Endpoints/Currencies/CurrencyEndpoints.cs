using Mediator;
using Minibox.BuildingBlocks.Web;
using Minibox.Localization.Application.Features.Currencies.Queries.ListCurrencies;

namespace Minibox.Localization.Api.Endpoints.Currencies;

/// <summary>Endpoint tiền tệ hiển thị (Minimal API mỏng).</summary>
public static class CurrencyEndpoints
{
    /// <summary>Map <c>GET /currencies</c> (công khai): tiền tệ đang bật kèm tỷ giá hiện hành.</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapCurrencyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/currencies").WithTags("Currencies")
            .MapGet("", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListCurrencies(), ct)).ToHttpResult())
            .Produces<CurrencyDto[]>();
        return endpoints;
    }
}
