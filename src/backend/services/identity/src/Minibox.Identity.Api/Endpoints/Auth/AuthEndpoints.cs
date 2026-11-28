using Mediator;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Application.Features.Mfa.Commands.BeginMfaEnrollment;
using Minibox.Identity.Application.Features.Mfa.Commands.ConfirmMfaEnrollment;
using Minibox.Identity.Application.Features.Mfa.Commands.VerifyMfa;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Application.Features.Sessions.Commands.Login;
using Minibox.Identity.Application.Features.Sessions.Commands.Logout;
using Minibox.Identity.Application.Features.Sessions.Commands.RefreshSession;

namespace Minibox.Identity.Api.Endpoints.Auth;

/// <summary>Thân request đăng nhập mật khẩu do BFF gửi.</summary>
/// <param name="Identifier">Email hoặc username.</param>
/// <param name="Password">Mật khẩu.</param>
/// <param name="Ip">IP người dùng cuối mà BFF thấy (dùng để giới hạn đăng nhập sai).</param>
public sealed record LoginRequest(string Identifier, string Password, string Ip);

/// <summary>Thân request xác minh MFA (sau mật khẩu hoặc khi xác thực lại).</summary>
/// <param name="Code">Mã TOTP 6 chữ số hoặc mã khôi phục.</param>
/// <param name="RefreshToken">Refresh token của phiên hiện tại (không bắt buộc), bị thu hồi khi xác minh thành công.</param>
public sealed record MfaVerifyRequest(string Code, string? RefreshToken = null);

/// <summary>Thân request xác nhận đăng ký MFA.</summary>
/// <param name="Code">Mã 6 chữ số từ secret vừa quét.</param>
public sealed record MfaConfirmRequest(string Code);

/// <summary>Thân request làm mới hoặc đăng xuất.</summary>
/// <param name="RefreshToken">Refresh token hiện tại.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>API phiên cho BFF (spec §3.2, §3.5): token chỉ trả cho BFF trong mạng nội bộ, BFF giữ trong cookie mã hoá.
/// POST vẫn cần <c>Idempotency-Key</c> như mọi API ghi.</summary>
public static class AuthEndpoints
{
    /// <summary>Map <c>/auth/login</c>, <c>/auth/mfa/*</c>, <c>/auth/refresh</c>, <c>/auth/logout</c>.</summary>
    /// <param name="endpoints">Bộ định tuyến.</param>
    /// <returns><paramref name="endpoints"/> để gọi tiếp.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth").WithTags("Auth");
        auth.MapPost("login", async (LoginRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new Login(body.Identifier, body.Password, body.Ip), ct)).ToHttpResult()).Produces<TokenResponse>();
        // Yếu tố thứ hai và xác thực lại: tài khoản lấy từ access token (sub), không từ body; phiên cũ (chỉ pwd) bị thu hồi.
        auth.MapPost("mfa/verify", [AllowWithoutMfa] async (MfaVerifyRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new VerifyMfa(body.Code, body.RefreshToken), ct)).ToHttpResult()).RequireAuthorization().Produces<TokenResponse>();
        auth.MapPost("mfa/enrollment", [AllowWithoutMfa] async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new BeginMfaEnrollment(), ct)).ToHttpResult()).RequireAuthorization().Produces<MfaEnrollment>();
        auth.MapPost("mfa/enrollment/confirm", [AllowWithoutMfa] async (MfaConfirmRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ConfirmMfaEnrollment(body.Code), ct)).ToHttpResult()).RequireAuthorization().Produces<IReadOnlyList<string>>();
        auth.MapPost("refresh", async (RefreshRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RefreshSession(body.RefreshToken), ct)).ToHttpResult()).Produces<TokenResponse>();
        auth.MapPost("logout", async (RefreshRequest body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new Logout(body.RefreshToken), ct)).ToHttpResult()).Produces(StatusCodes.Status204NoContent);
        return endpoints;
    }
}
