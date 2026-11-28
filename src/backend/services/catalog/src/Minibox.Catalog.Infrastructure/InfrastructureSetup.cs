using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Infrastructure.Persistence;

namespace Minibox.Catalog.Infrastructure;

/// <summary>Đăng ký Infrastructure của service catalog.</summary>
public static class InfrastructureSetup
{
    /// <summary>Đăng ký <see cref="CatalogDbContext"/> kèm Unit of Work, outbox Wolverine và kho Idempotency-Key
    /// (<see cref="PersistenceSetup.AddMiniboxPersistence{TDb}"/>), repository và read service cài port của Application.</summary>
    /// <param name="services">DI container; cần sẵn Wolverine (<c>UseMiniboxMessaging</c>), <see cref="ICurrentUser"/>, <see cref="TimeProvider"/>.</param>
    /// <param name="connectionString">Chuỗi kết nối role <c>catalog_app</c> (chỉ DML).</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, string connectionString) => services
        .AddMiniboxPersistence<CatalogDbContext>(connectionString);
}
