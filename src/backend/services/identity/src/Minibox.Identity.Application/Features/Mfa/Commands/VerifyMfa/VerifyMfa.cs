using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Mfa.Commands.VerifyMfa;

/// <summary>Lệnh xác minh yếu tố thứ hai (sau mật khẩu) hoặc xác thực lại (step-up) của người đang đăng nhập.</summary>
/// <param name="Code">Mã TOTP 6 chữ số hoặc mã khôi phục.</param>
/// <param name="RefreshToken">Refresh token của phiên hiện tại (không bắt buộc), bị thu hồi khi xác minh thành công.</param>
/// <remarks>Tài khoản lấy từ claim <c>sub</c> của access token, không từ body.</remarks>
public sealed record VerifyMfa(string? Code, string? RefreshToken) : ICommand<TokenResponse>;

/// <summary>Kiểm mã (có giới hạn nhập sai) và mở phiên mới <c>amr = [pwd, mfa]</c>, <c>auth_time</c> = bây giờ.</summary>
/// <param name="user">Người gọi.</param>
/// <param name="accounts">Repository tài khoản.</param>
/// <param name="throttles">Repository bộ đếm sai.</param>
/// <param name="tokens">Repository refresh token (thu hồi phiên cũ).</param>
/// <param name="protector">Giải mã secret TOTP.</param>
/// <param name="sessions">Mở phiên.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class VerifyMfaHandler(ICurrentUser user, IAccountRepository accounts, ILoginThrottleRepository throttles, IRefreshTokenRepository tokens,
    IMfaSecretProtector protector, SessionIssuer sessions, TimeProvider time) : ICommandHandler<VerifyMfa, TokenResponse>
{
    /// <inheritdoc/>
    /// <returns>Cặp token của phiên mới; lỗi <see cref="IdentityErrors.Unauthenticated"/>, <see cref="IdentityErrors.MfaNotEnrolled"/>,
    /// <see cref="IdentityErrors.InvalidMfaCode"/> hoặc <see cref="IdentityErrors.MfaThrottled"/>.</returns>
    public async ValueTask<Result<TokenResponse>> Handle(VerifyMfa command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.UserId, out var accountId))
            return IdentityErrors.Unauthenticated;
        var now = time.GetUtcNow();
        var key = LoginThrottle.MfaKey(accountId);
        var throttle = await throttles.LockAsync(key, cancellationToken);
        if (throttle?.IsLocked(now) == true)
            return IdentityErrors.MfaThrottled;

        var account = await accounts.GetForUpdateAsync(accountId, cancellationToken);
        if (account is not { HasMfa: true })
            return IdentityErrors.MfaNotEnrolled;
        var verified = account.VerifyMfa(protector.Unprotect(accountId, account.Mfa!.SecretEnc), command.Code, now);
        if (verified.IsFailure)
        {
            await throttles.RecordFailureAsync(throttle, key, LoginThrottle.AccountThreshold, now, cancellationToken);
            return verified.Error.WithCommit();
        }

        await throttles.ResetAsync(throttle, cancellationToken);
        if (command.RefreshToken is { Length: > 0 and <= RefreshToken.MaxTokenLength } old
            && await tokens.GetAsync(RefreshToken.Hash(old), cancellationToken) is { RevokedAt: null } previous && previous.AccountId == accountId)
        {
            await tokens.RevokeFamilyAsync(previous.FamilyId, now, cancellationToken);
            account.RecordSessionRevoked(previous.FamilyId, "mfa");
        }
        await accounts.UpdateAsync(account, account.Version, cancellationToken);
        // ponytail: amr cố định pwd+mfa vì hiện chỉ có đăng nhập mật khẩu; thêm tham số phương thức gốc khi có đăng nhập xã hội.
        return await sessions.OpenAsync(account, ["pwd", "mfa"], cancellationToken);
    }
}
