using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Infrastructure.Messaging;
using Minibox.Identity.Infrastructure.Persistence;
using Minibox.Identity.Infrastructure.Security;

namespace Minibox.Identity.Infrastructure;

/// <summary>Đăng ký Infrastructure của service identity.</summary>
public static class InfrastructureSetup
{
    /// <summary>Đăng ký <see cref="IdentityDbContext"/> kèm Unit of Work, outbox, kho Idempotency-Key; repository, read service và
    /// adapter (băm mật khẩu, mã hoá MFA, key ring, ký JWT) cài port của Application.</summary>
    /// <param name="services">DI container; cần sẵn Wolverine (<c>UseMiniboxMessaging</c>), <see cref="ICurrentUser"/>, <see cref="TimeProvider"/>,
    /// <see cref="IConfiguration"/> có <c>Mfa:EncryptionKey</c> (base64 32 byte) và <c>Jwt:Issuer</c> (đọc lúc dùng lần đầu).</param>
    /// <param name="connectionString">Chuỗi kết nối role <c>identity_app</c> (chỉ DML).</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, string connectionString) => services
        .AddMiniboxPersistence<IdentityDbContext>(connectionString)
        .AddSingleton<IIntegrationEventMapper, IdentityIntegrationEvents>()
        .AddScoped<IAccountRepository, AccountRepository>()
        .AddScoped<ILoginThrottleRepository, LoginThrottleRepository>()
        .AddScoped<IRefreshTokenRepository, RefreshTokenRepository>()
        .AddScoped<IRoleRepository, RoleRepository>()
        .AddScoped<IPermissionReadService, PermissionReadService>()
        .AddScoped<IAccountPreferencesReadService, AccountPreferencesReadService>()
        .AddSingleton<IPasswordHasher, Argon2idPasswordHasher>()
        .AddSingleton<IMfaSecretProtector>(sp => new AesGcmMfaSecretProtector(Convert.FromBase64String(
            sp.GetRequiredService<IConfiguration>()["Mfa:EncryptionKey"] ?? throw new InvalidOperationException("Thiếu cấu hình Mfa:EncryptionKey"))))
        .AddSingleton(sp => new SigningKeyRing(sp.GetRequiredService<TimeProvider>(), SigningKeyRing.DefaultPeriod))
        .AddSingleton<ISigningKeys>(sp => sp.GetRequiredService<SigningKeyRing>())
        .AddSingleton<IAccessTokenIssuer>(sp => new JwtAccessTokenIssuer(sp.GetRequiredService<SigningKeyRing>(), sp.GetRequiredService<TimeProvider>(),
            JwtAccessTokenIssuer.IssuerFrom(sp.GetRequiredService<IConfiguration>())));
}
