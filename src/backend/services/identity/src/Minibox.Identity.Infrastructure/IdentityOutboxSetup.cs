using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Infrastructure.Accounts;

namespace Minibox.Identity.Infrastructure;

/// <summary>Đăng ký ghi dữ liệu identity qua EF Core Unit of Work + outbox Wolverine (spec §2.1).</summary>
public static class IdentityOutboxSetup
{
    /// <summary>Đăng ký <see cref="IdentityDbContext"/> + Unit of Work, <see cref="AccountRepository"/> và mapper <see cref="IdentityIntegrationEvents"/>.</summary>
    /// <param name="services">DI container; cần sẵn Wolverine (<c>UseMiniboxMessaging</c>), <c>ICurrentUser</c>, <c>TimeProvider</c>.</param>
    /// <param name="connectionString">Chuỗi kết nối role <c>identity_app</c>.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddIdentityOutbox(this IServiceCollection services, string connectionString) => services
        .AddMiniboxPersistence<IdentityDbContext>(connectionString)
        .AddSingleton<IIntegrationEventMapper, IdentityIntegrationEvents>()
        .AddScoped<AccountRepository>();
}
