using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Infrastructure.Messaging;
using Minibox.Localization.Infrastructure.Persistence;

namespace Minibox.Localization.Infrastructure;

/// <summary>Đăng ký Infrastructure của service localization.</summary>
public static class InfrastructureSetup
{
    /// <summary>Đăng ký <see cref="LocalizationDbContext"/> kèm Unit of Work, outbox Wolverine và kho Idempotency-Key
    /// (<see cref="PersistenceSetup.AddMiniboxPersistence{TDb}"/>), repository, read service cài port của Application và ánh xạ integration event.</summary>
    /// <param name="services">DI container; cần sẵn Wolverine (<c>UseMiniboxMessaging</c>), <see cref="ICurrentUser"/>, <see cref="TimeProvider"/>.</param>
    /// <param name="connectionString">Chuỗi kết nối role <c>localization_app</c> (chỉ DML).</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddLocalizationInfrastructure(this IServiceCollection services, string connectionString) => services
        .AddMiniboxPersistence<LocalizationDbContext>(connectionString)
        .AddSingleton<IIntegrationEventMapper, LocalizationIntegrationEvents>()
        .AddScoped<ILocaleRepository, LocaleRepository>()
        .AddScoped<IUiMessageRepository, UiMessageRepository>()
        .AddScoped<ILocaleReadService, LocaleReadService>()
        .AddScoped<IUiMessageReadService, UiMessageReadService>();
}
