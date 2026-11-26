using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Tokens;

namespace Minibox.Identity.Application.Accounts;

/// <summary>Secret TOTP vừa sinh, chờ người dùng quét vào authenticator và xác nhận.</summary>
/// <param name="Secret">Secret Base32 (cho nhập tay).</param>
/// <param name="OtpauthUri">URI <c>otpauth://totp/...</c> để hiển thị dạng QR.</param>
public sealed record MfaEnrollment(string Secret, string OtpauthUri);

/// <summary>MFA TOTP và mã khôi phục (spec §3.5).</summary>
/// <remarks>Secret lưu mã hóa AES-GCM, mã khôi phục lưu SHA-256. Sai mã bị giới hạn như đăng nhập (<see cref="IPasswordLogin.ThrottledCode"/>).</remarks>
public interface IMfa
{
    /// <summary>Mã lỗi khi mã TOTP/mã khôi phục sai, đã dùng hoặc sai định dạng.</summary>
    const string InvalidCodeCode = "Identity.InvalidMfaCode";

    /// <summary>Mã lỗi khi tài khoản chưa có MFA đã xác nhận (hoặc chưa bắt đầu đăng ký).</summary>
    const string NotEnrolledCode = "Identity.MfaNotEnrolled";

    /// <summary>Mã lỗi khi đăng ký lại trong khi MFA đã bật.</summary>
    const string AlreadyEnrolledCode = "Identity.MfaAlreadyEnrolled";

    /// <summary>Sinh secret mới (thay secret đang chờ xác nhận nếu có).</summary>
    /// <param name="accountId">Tài khoản đã đăng nhập.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Secret và otpauth URI; lỗi <see cref="AlreadyEnrolledCode"/> nếu MFA đã bật.</returns>
    Task<Result<MfaEnrollment>> BeginEnrollmentAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Bật MFA khi người dùng nhập đúng mã từ secret đang chờ; cấp bộ mã khôi phục mới.</summary>
    /// <param name="accountId">Tài khoản.</param>
    /// <param name="code">Mã 6 chữ số.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns><c>MfaPolicy.RecoveryCodeCount</c> mã khôi phục (chỉ lộ một lần); lỗi <see cref="NotEnrolledCode"/> nếu chưa
    /// bắt đầu đăng ký, <see cref="InvalidCodeCode"/> nếu sai mã.</returns>
    Task<Result<IReadOnlyList<string>>> ConfirmEnrollmentAsync(Guid accountId, string code, CancellationToken cancellationToken = default);

    /// <summary>Kiểm mã TOTP hoặc mã khôi phục (yếu tố thứ hai sau mật khẩu, hoặc xác thực lại) và mở phiên mới.</summary>
    /// <param name="accountId">Tài khoản đã qua bước mật khẩu.</param>
    /// <param name="code">Mã 6 chữ số hoặc mã khôi phục (mỗi mã khôi phục dùng một lần).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Phiên mới <c>amr = [pwd, mfa]</c>, <c>auth_time</c> = bây giờ; lỗi <see cref="NotEnrolledCode"/>,
    /// <see cref="InvalidCodeCode"/> hoặc <see cref="IPasswordLogin.ThrottledCode"/>.</returns>
    /// <remarks>Người gọi thu hồi phiên cũ (chỉ có <c>pwd</c>) sau khi nhận phiên mới. Mã TOTP đã dùng không dùng lại được.</remarks>
    Task<Result<RefreshSession>> VerifyAsync(Guid accountId, string code, CancellationToken cancellationToken = default);
}
