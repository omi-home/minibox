using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Infrastructure.Accounts;

namespace Minibox.Identity.Infrastructure;

/// <summary>Đăng ký ghi dữ liệu identity qua Unit of Work + outbox Wolverine (spec §2.1).</summary>
public static class IdentityOutboxSetup
{
    /// <summary>Đăng ký <see cref="NpgsqlUnitOfWork"/>, <see cref="AccountRepository"/> và mapper <see cref="IdentityIntegrationEvents"/>.</summary>
    /// <param name="services">DI container; cần sẵn Wolverine (<c>UseMiniboxMessaging</c>), <c>NpgsqlDataSource</c>, <c>ICurrentUser</c>, <c>TimeProvider</c>.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddIdentityOutbox(this IServiceCollection services) => services
        .AddSingleton<IIntegrationEventMapper, IdentityIntegrationEvents>()
        .AddScoped<NpgsqlUnitOfWork>()
        .AddScoped<AccountRepository>();
}
