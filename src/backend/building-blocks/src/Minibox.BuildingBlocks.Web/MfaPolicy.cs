using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minibox.BuildingBlocks.Application.Security;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Web;

/// <summary>Đánh dấu endpoint nhạy cảm (đổi quyền, hoàn tiền, điều chỉnh kho…): đòi <c>amr</c> có <c>mfa</c> và
/// <c>auth_time</c> trong <see cref="MfaPolicyOptions.StepUpMaxAge"/>; không đạt → <c>401 step_up_required</c> (spec §3.5).</summary>
/// <example><c>app.MapPost("/refunds", [Sensitive] (…) => …)</c></example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SensitiveAttribute : Attribute;

/// <summary>Cho phép nhân viên chưa có MFA gọi endpoint (chỉ dùng cho API đăng ký MFA).</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowWithoutMfaAttribute : Attribute;

/// <summary>Cấu hình chính sách MFA; bind bằng <c>services.Configure&lt;MfaPolicyOptions&gt;(…)</c>.</summary>
public sealed class MfaPolicyOptions
{
    /// <summary>Tuổi tối đa của <c>auth_time</c> cho endpoint <see cref="SensitiveAttribute"/>; mặc định 5 phút.</summary>
    public TimeSpan StepUpMaxAge { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>Middleware bắt buộc MFA cho staff và step-up cho endpoint nhạy cảm, chạy sau authentication/authorization.</summary>
public static class MfaPolicy
{
    /// <summary>Mã lỗi (403) khi nhân viên chưa xác thực MFA gọi endpoint không có <see cref="AllowWithoutMfaAttribute"/>.</summary>
    public const string MfaRequiredCode = "mfa_required";

    /// <summary>Mã lỗi (401) khi endpoint <see cref="SensitiveAttribute"/> cần xác thực lại; BFF bật màn hình step-up.</summary>
    public const string StepUpRequiredCode = "step_up_required";

    const string Mfa = "mfa";

    internal static Task Enforce(HttpContext context, RequestDelegate next)
    {
        var endpoint = context.GetEndpoint();
        var user = context.User;
        var hasMfa = user.HasClaim(AccessTokenClaims.AuthMethods, Mfa);

        if (endpoint?.Metadata.GetMetadata<SensitiveAttribute>() is not null && !RecentMfa(context, hasMfa))
        {
            // RFC 9470: báo client cần xác thực mạnh/mới hơn.
            context.Response.Headers.WWWAuthenticate = "Bearer error=\"insufficient_user_authentication\"";
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: StepUpRequiredCode,
                detail: "Cần xác thực lại", extensions: new Dictionary<string, object?> { ["code"] = StepUpRequiredCode }).ExecuteAsync(context);
        }

        if (user.FindFirstValue(AccessTokenClaims.Kind) == "staff" && !hasMfa && endpoint?.Metadata.GetMetadata<AllowWithoutMfaAttribute>() is null)
            return ResultHttpExtensions.Problem(Error.Forbidden(MfaRequiredCode, "Nhân viên phải đăng ký và xác thực MFA")).ExecuteAsync(context);

        return next(context);
    }

    static bool RecentMfa(HttpContext context, bool hasMfa)
    {
        if (!hasMfa || !long.TryParse(context.User.FindFirstValue(AccessTokenClaims.AuthTime), out var authTime)) return false;
        var maxAge = context.RequestServices.GetRequiredService<IOptions<MfaPolicyOptions>>().Value.StepUpMaxAge;
        var now = context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        return now - DateTimeOffset.FromUnixTimeSeconds(authTime) <= maxAge;
    }
}
