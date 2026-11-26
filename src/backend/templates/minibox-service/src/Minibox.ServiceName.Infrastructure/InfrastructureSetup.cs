using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minibox.ServiceName.Application.Samples;
using Minibox.ServiceName.Infrastructure.Persistence;
using Npgsql;

namespace Minibox.ServiceName.Infrastructure;

/// <summary>Đăng ký Infrastructure của service servicename.</summary>
public static class InfrastructureSetup
{
    /// <summary>Đăng ký <see cref="ServiceNameDbContext"/> trên <see cref="NpgsqlDataSource"/> (role app), repository và read service.</summary>
    /// <param name="services">DI container; cần sẵn <see cref="NpgsqlDataSource"/>.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddServiceNameInfrastructure(this IServiceCollection services) => services
        .AddDbContext<ServiceNameDbContext>((sp, o) => o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()))
        .AddScoped<SampleRepository>()
        .AddScoped<ISampleReadService, SampleReadService>();
}
