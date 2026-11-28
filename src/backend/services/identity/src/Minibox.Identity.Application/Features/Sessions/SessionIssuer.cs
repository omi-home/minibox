using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;

namespace Minibox.Identity.Application.Features.Sessions;

/// <summary>Cặp token trả cho BFF; trình duyệt không bao giờ nhận.</summary>
/// <param name="AccessToken">Access JWT 5 phút.</param>
/// <param name="AccessTokenExpiresAt">Hạn access token.</param>
/// <param name="RefreshToken">Refresh token mới (token cũ đã hết hiệu lực).</param>
/// <param name="RefreshTokenExpiresAt">Hạn refresh token.</param>
/// <param name="AuthMethods">Phương thức đã xác thực của phiên (<c>pwd</c>, <c>mfa</c>).</param>
public sealed record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt,
    IReadOnlyList<string> AuthMethods);

/// <summary>Mở phiên (refresh token) và cấp access token với quyền hiện hành, trong transaction của command đang chạy.</summary>
/// <param name="tokens">Repository refresh token.</param>
/// <param name="permissions">Đọc quyền hiệu lực.</param>
/// <param name="issuer">Bộ ký access token.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class SessionIssuer(IRefreshTokenRepository tokens, IPermissionReadService permissions, IAccessTokenIssuer issuer, TimeProvider time)
{
    /// <summary>Mở phiên mới cho tài khoản.</summary>
    /// <param name="account">Tài khoản đã xác thực.</param>
    /// <param name="amr">Phương thức đã xác thực.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Cặp token của phiên mới.</returns>
    public async Task<TokenResponse> OpenAsync(Account account, IReadOnlyList<string> amr, CancellationToken cancellationToken)
    {
        var (token, value) = RefreshToken.Open(account.Id, account.Kind, amr, time.GetUtcNow());
        await tokens.AddAsync(token, cancellationToken);
        return await TokensAsync(token, value, account.Kind, cancellationToken);
    }

    /// <summary>Cấp access token cho refresh token vừa mở hoặc vừa xoay.</summary>
    /// <param name="token">Refresh token mới.</param>
    /// <param name="value">Giá trị thô của <paramref name="token"/>.</param>
    /// <param name="kind">Loại tài khoản.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Cặp token.</returns>
    public async Task<TokenResponse> TokensAsync(RefreshToken token, string value, AccountKind kind, CancellationToken cancellationToken)
    {
        var access = issuer.Issue(new AccessTokenRequest(token.AccountId.ToString(), kind.ToCode(),
            await permissions.ForAccountAsync(token.AccountId, cancellationToken), token.AuthTime, token.Amr));
        return new TokenResponse(access.Token, access.ExpiresAt, value, token.ExpiresAt, token.Amr);
    }
}

/// <summary>Ghi bộ đếm nhập sai qua repository (thêm mới hoặc cập nhật).</summary>
public static class LoginThrottleRepositoryExtensions
{
    /// <summary>Ghi một lần sai vào bộ đếm đã nạp bằng <see cref="ILoginThrottleRepository.LockAsync"/>.</summary>
    /// <param name="repository">Repository.</param>
    /// <param name="throttle">Bộ đếm đã nạp, hoặc <c>null</c> nếu chưa có.</param>
    /// <param name="key">Khoá đếm.</param>
    /// <param name="threshold">Ngưỡng khoá.</param>
    /// <param name="now">Thời điểm sai.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã ghi.</returns>
    public static async Task RecordFailureAsync(this ILoginThrottleRepository repository, LoginThrottle? throttle, string key, int threshold,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (throttle is null)
        {
            throttle = LoginThrottle.Create(key);
            throttle.RecordFailure(threshold, now);
            await repository.AddAsync(throttle, cancellationToken);
            return;
        }
        throttle.RecordFailure(threshold, now);
        await repository.UpdateAsync(throttle, throttle.Version, cancellationToken);
    }

    /// <summary>Xoá bộ đếm sau khi nhập đúng (không ghi gì nếu chưa có lần sai).</summary>
    /// <param name="repository">Repository.</param>
    /// <param name="throttle">Bộ đếm đã nạp, hoặc <c>null</c>.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã ghi.</returns>
    public static async Task ResetAsync(this ILoginThrottleRepository repository, LoginThrottle? throttle, CancellationToken cancellationToken)
    {
        if (throttle is not { Failures: > 0 })
            return;
        throttle.Reset();
        await repository.UpdateAsync(throttle, throttle.Version, cancellationToken);
    }
}
