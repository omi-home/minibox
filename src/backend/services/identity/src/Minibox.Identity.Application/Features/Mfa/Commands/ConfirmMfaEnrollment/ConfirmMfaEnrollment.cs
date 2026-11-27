using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Mfa.Commands.ConfirmMfaEnrollment;

/// <summary>Lệnh bật MFA khi người dùng nhập đúng mã từ secret đang chờ.</summary>
/// <param name="Code">Mã 6 chữ số.</param>
public sealed record ConfirmMfaEnrollment(string? Code) : ICommand<IReadOnlyList<string>>;

/// <summary>Kiểm mã (có giới hạn nhập sai), bật MFA và cấp bộ mã khôi phục mới.</summary>
/// <param name="user">Người gọi.</param>
/// <param name="accounts">Repository tài khoản.</param>
/// <param name="throttles">Repository bộ đếm sai.</param>
/// <param name="protector">Giải mã secret.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class ConfirmMfaEnrollmentHandler(ICurrentUser user, IAccountRepository accounts, ILoginThrottleRepository throttles,
    IMfaSecretProtector protector, TimeProvider time) : ICommandHandler<ConfirmMfaEnrollment, IReadOnlyList<string>>
{
    /// <inheritdoc/>
    /// <returns>Mã khôi phục (chỉ lộ một lần); lỗi <see cref="IdentityErrors.Unauthenticated"/>, <see cref="IdentityErrors.MfaNotEnrolled"/>,
    /// <see cref="IdentityErrors.InvalidMfaCode"/> hoặc <see cref="IdentityErrors.MfaThrottled"/>.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> Handle(ConfirmMfaEnrollment command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.UserId, out var accountId))
            return IdentityErrors.Unauthenticated;
        var now = time.GetUtcNow();
        var key = LoginThrottle.MfaKey(accountId);
        var throttle = await throttles.LockAsync(key, cancellationToken);
        if (throttle?.IsLocked(now) == true)
            return IdentityErrors.MfaThrottled;

        var account = await accounts.GetForUpdateAsync(accountId, cancellationToken);
        if (account is not { HasPendingMfa: true })
            return IdentityErrors.MfaNotEnrolled;
        var codes = account.ConfirmMfa(protector.Unprotect(accountId, account.Mfa!.SecretEnc), command.Code, now);
        if (codes.IsFailure)
        {
            await throttles.RecordFailureAsync(throttle, key, LoginThrottle.AccountThreshold, now, cancellationToken);
            return codes.Error.WithCommit();
        }
        await throttles.ResetAsync(throttle, cancellationToken);
        await accounts.UpdateAsync(account, account.Version, cancellationToken);
        return codes;
    }
}
