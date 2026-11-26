using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Application.Behaviors;
using Npgsql;

namespace Minibox.BuildingBlocks.Web;

/// <summary>Đăng ký và pipeline HTTP dùng chung cho Api của mọi service.</summary>
public static class WebSetup
{
    /// <summary>Đăng ký ProblemDetails, <see cref="ICurrentUser"/> từ claim <c>sub</c>, health check PostgreSQL (tag <c>ready</c>).</summary>
    /// <param name="services">DI container.</param>
    /// <returns><paramref name="services"/> để gọi tiếp.</returns>
    public static IServiceCollection AddMiniboxWeb(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddHealthChecks().AddCheck<NpgsqlHealthCheck>("postgres", tags: ["ready"]);
        return services;
    }

    /// <summary>Exception → ProblemDetails 500 (không lộ chi tiết); bắt buộc <c>Idempotency-Key</c> hợp lệ cho mọi request ghi.</summary>
    /// <param name="app">Ứng dụng.</param>
    /// <returns><paramref name="app"/> để gọi tiếp.</returns>
    public static IApplicationBuilder UseMiniboxWeb(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.Use(RequireIdempotencyKey);
        return app;
    }

    /// <summary>Map <c>/health/live</c> (tiến trình còn chạy, không kiểm phụ thuộc) và <c>/health/ready</c> (kiểm tag <c>ready</c>).</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapMiniboxHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        return endpoints;
    }

    static Task RequireIdempotencyKey(HttpContext context, RequestDelegate next)
    {
        var method = context.Request.Method;
        var writes = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
        if (writes && !IdempotencyKey.IsValid(context.Request.Headers[IdempotencyKey.HeaderName].ToString()))
            return ResultHttpExtensions.Problem(IdempotencyKey.InvalidError).ExecuteAsync(context);
        return next(context);
    }

    sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        public string? UserId => accessor.HttpContext?.User.FindFirstValue("sub");
    }

    sealed class NpgsqlHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var command = dataSource.CreateCommand("SELECT 1");
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL không phản hồi"); // không trả chuỗi lỗi: có thể chứa host/user
            }
        }
    }
}
