using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Minibox.Identity.Application.Features.Sessions;

namespace Minibox.Identity.Application;

/// <summary>Đăng ký Application của service identity (handler do <c>AddMediator</c> ở Api sinh mã đăng ký).</summary>
public static class ApplicationSetup
{
    /// <summary>Đăng ký <see cref="SessionIssuer"/> và mọi <see cref="IValidator{T}"/> trong assembly cho <c>ValidationBehavior</c>.</summary>
    /// <param name="services">DI container.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        var validators = typeof(ApplicationSetup).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>)), (t, i) => (t, i));
        foreach (var (type, contract) in validators)
            services.AddSingleton(contract, type);
        return services.AddScoped<SessionIssuer>();
    }
}
