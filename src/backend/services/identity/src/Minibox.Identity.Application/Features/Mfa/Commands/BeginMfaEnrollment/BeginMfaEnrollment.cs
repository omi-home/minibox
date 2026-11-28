using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Mfa.Commands.BeginMfaEnrollment;

/// <summary>Lệnh bắt đầu đăng ký MFA cho người đang đăng nhập: sinh secret mới (thay secret đang chờ nếu có).</summary>
public sealed record BeginMfaEnrollment : ICommand<MfaEnrollment>;

/// <summary>Secret TOTP vừa sinh, chờ người dùng quét vào authenticator và xác nhận.</summary>
/// <param name="Secret">Secret Base32 (cho nhập tay).</param>
/// <param name="OtpauthUri">URI <c>otpauth://totp/...</c> để hiển thị dạng QR.</param>
public sealed record MfaEnrollment(string Secret, string OtpauthUri);

/// <summary>Sinh và lưu secret (mã hoá) vào tài khoản.</summary>
/// <param name="user">Người gọi.</param>
/// <param name="accounts">Repository tài khoản.</param>
/// <param name="protector">Mã hoá secret.</param>
public sealed class BeginMfaEnrollmentHandler(ICurrentUser user, IAccountRepository accounts, IMfaSecretProtector protector)
    : ICommandHandler<BeginMfaEnrollment, MfaEnrollment>
{
    /// <inheritdoc/>
    /// <returns>Secret và otpauth URI (chỉ lộ một lần); lỗi <see cref="IdentityErrors.Unauthenticated"/>,
    /// <see cref="IdentityErrors.MfaNotEnrolled"/> (tài khoản không tồn tại) hoặc <see cref="IdentityErrors.MfaAlreadyEnrolled"/>.</returns>
    public async ValueTask<Result<MfaEnrollment>> Handle(BeginMfaEnrollment command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.UserId, out var accountId))
            return IdentityErrors.Unauthenticated;
        var account = await accounts.GetForUpdateAsync(accountId, cancellationToken);
        if (account is null)
            return IdentityErrors.MfaNotEnrolled;
        var secret = MfaPolicy.NewSecret();
        var begun = account.BeginMfaEnrollment(protector.Protect(accountId, secret));
        if (begun.IsFailure)
            return begun.Error;
        await accounts.UpdateAsync(account, account.Version, cancellationToken);
        var base32 = Totp.Base32(secret);
        return new MfaEnrollment(base32, MfaPolicy.OtpauthUri(account.Label, base32));
    }
}
