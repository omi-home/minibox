using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.ServiceName.Application.Abstractions;
using Minibox.ServiceName.Domain.Aggregates.Samples;
using Minibox.ServiceName.Infrastructure.Persistence;

namespace Minibox.ServiceName.Infrastructure;

/// <summary>Đăng ký Infrastructure của service servicename.</summary>
public static class InfrastructureSetup
{
    /// <summary>Đăng ký <see cref="ServiceNameDbContext"/> kèm Unit of Work, outbox Wolverine và kho Idempotency-Key
    /// (<see cref="PersistenceSetup.AddMiniboxPersistence{TDb}"/>), repository và read service cài port của Application.</summary>
    /// <param name="services">DI container; cần sẵn Wolverine (<c>UseMiniboxMessaging</c>), <see cref="ICurrentUser"/>, <see cref="TimeProvider"/>.</param>
    /// <param name="connectionString">Chuỗi kết nối role <c>servicename_app</c> (chỉ DML).</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddServiceNameInfrastructure(this IServiceCollection services, string connectionString) => services
        .AddMiniboxPersistence<ServiceNameDbContext>(connectionString)
        .AddScoped<IRepository<Sample, Guid>, SampleRepository>()
        .AddScoped<ISampleReadService, SampleReadService>();
}
