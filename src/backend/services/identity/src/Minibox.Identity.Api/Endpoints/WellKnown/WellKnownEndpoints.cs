using Mediator;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Application.Features.Keys.Queries.GetJwks;

namespace Minibox.Identity.Api.Endpoints.WellKnown;

/// <summary>Discovery tối thiểu + JWKS cho service khác tự kiểm chữ ký (JwtAuthenticationSetup); chỉ khoá công khai, chỉ trong mạng nội bộ.</summary>
public static class WellKnownEndpoints
{
    /// <summary>Map <c>/.well-known/openid-configuration</c> và <c>/.well-known/jwks.json</c>.</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapWellKnownEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/.well-known/openid-configuration", (IConfiguration config) =>
        {
            var issuer = (config["Jwt:Issuer"] ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Issuer")).TrimEnd('/');
            return new { issuer, jwks_uri = issuer + "/.well-known/jwks.json" };
        });
        endpoints.MapGet("/.well-known/jwks.json", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetJwks(), ct)).ToHttpResult())
            .Produces<object>();
        return endpoints;
    }
}
