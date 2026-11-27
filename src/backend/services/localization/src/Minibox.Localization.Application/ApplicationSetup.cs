using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Minibox.Localization.Application;

/// <summary>Đăng ký Application của service localization (handler do <c>AddMediator</c> ở Api sinh mã đăng ký).</summary>
public static class ApplicationSetup
{
    /// <summary>Đăng ký mọi <see cref="IValidator{T}"/> trong assembly (singleton, không trạng thái) cho <c>ValidationBehavior</c>.</summary>
    /// <param name="services">DI container.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddLocalizationApplication(this IServiceCollection services)
    {
        var validators = typeof(ApplicationSetup).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)), (t, i) => (t, i));
        foreach (var (type, contract) in validators)
            services.AddSingleton(contract, type);
        return services;
    }
}
