using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application.Security;

namespace Minibox.BuildingBlocks.Web;

/// <summary>Kiểm access token do <c>identity</c> cấp (spec §3.2): chữ ký ES256 qua JWKS cache, <c>iss</c>, <c>aud</c>, hạn.</summary>
public static class JwtAuthenticationSetup
{
    /// <summary>Đăng ký JWT bearer làm scheme mặc định; JWKS lấy qua <c>{authority}/.well-known/openid-configuration</c>,
    /// làm mới mỗi 10 phút và khi gặp <c>kid</c> lạ. Claim giữ nguyên tên JWT (<c>sub</c>, <c>perms</c>…).</summary>
    /// <param name="services">DI container.</param>
    /// <param name="authority">URL nội bộ của identity, trùng <c>iss</c> trong token.</param>
    /// <returns><paramref name="services"/> để gọi tiếp.</returns>
    public static IServiceCollection AddMiniboxJwtAuthentication(this IServiceCollection services, string authority)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.Authority = authority;
            o.Audience = AccessTokenClaims.Audience;
            o.RequireHttpsMetadata = false; // discovery/JWKS chỉ trong mạng nội bộ (Compose network), không qua Caddy
            o.MapInboundClaims = false;
            o.AutomaticRefreshInterval = TimeSpan.FromMinutes(10); // ≪ chu kỳ xoay khóa; khóa kế tiếp đã công bố trước
            o.TokenValidationParameters.ValidAlgorithms = [AccessTokenClaims.Algorithm];
            o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            o.TokenValidationParameters.NameClaimType = AccessTokenClaims.Subject;
        });
        return services;
    }
}
