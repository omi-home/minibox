using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Aggregates.Accounts;

namespace Minibox.Identity.Domain.Errors;

/// <summary>Lỗi nghiệp vụ của identity; mã ổn định để BFF và ProblemDetails dựa vào.</summary>
public static class IdentityErrors
{
    /// <summary>Thiếu cả email lẫn username.</summary>
    public static readonly Error LoginRequired = Error.Validation("Identity.LoginRequired", "Cần email hoặc username.");

    /// <summary>Email sai định dạng.</summary>
    public static readonly Error InvalidEmail = Error.Validation("Identity.InvalidEmail", "Email không hợp lệ.");

    /// <summary>Username sai định dạng.</summary>
    public static readonly Error InvalidUsername =
        Error.Validation("Identity.InvalidUsername", "Username gồm 3–50 ký tự chữ, số, dấu chấm, gạch dưới, gạch ngang.");

    /// <summary>Mật khẩu ngắn hơn <see cref="PasswordPolicy.MinLength"/>.</summary>
    public static readonly Error PasswordTooShort =
        Error.Validation("Identity.PasswordTooShort", $"Mật khẩu cần ít nhất {PasswordPolicy.MinLength} ký tự.");

    /// <summary>Mật khẩu dài hơn <see cref="PasswordPolicy.MaxLength"/>.</summary>
    public static readonly Error PasswordTooLong = Error.Validation("Identity.PasswordTooLong", $"Mật khẩu tối đa {PasswordPolicy.MaxLength} ký tự.");

    /// <summary>Mật khẩu nằm trong danh sách phổ biến.</summary>
    public static readonly Error PasswordTooCommon = Error.Validation("Identity.PasswordTooCommon", "Mật khẩu quá phổ biến, hãy chọn mật khẩu khác.");

    /// <summary>Sai mật khẩu hoặc tài khoản không tồn tại (cùng một lỗi để không lộ tài khoản).</summary>
    public static readonly Error InvalidCredentials = Error.Failure("Identity.InvalidCredentials", "Email/tên đăng nhập hoặc mật khẩu không đúng.");

    /// <summary>Định danh hoặc IP đang bị khoá tạm vì đăng nhập sai nhiều lần.</summary>
    public static readonly Error LoginThrottled = Error.Failure("Identity.LoginThrottled", "Đăng nhập sai nhiều lần, vui lòng thử lại sau ít phút.");

    /// <summary>Nhập sai mã MFA nhiều lần (cùng mã lỗi với <see cref="LoginThrottled"/>).</summary>
    public static readonly Error MfaThrottled = Error.Failure(LoginThrottled.Code, "Nhập sai mã nhiều lần, vui lòng thử lại sau ít phút.");

    /// <summary>Mã TOTP/mã khôi phục sai, đã dùng hoặc sai định dạng.</summary>
    public static readonly Error InvalidMfaCode = Error.Failure("Identity.InvalidMfaCode", "Mã xác thực không đúng hoặc đã được dùng.");

    /// <summary>Tài khoản chưa có MFA đã xác nhận (hoặc chưa bắt đầu đăng ký).</summary>
    public static readonly Error MfaNotEnrolled = Error.Failure("Identity.MfaNotEnrolled", "Tài khoản chưa bật xác thực hai lớp.");

    /// <summary>Đăng ký lại trong khi MFA đã bật.</summary>
    public static readonly Error MfaAlreadyEnrolled = Error.Failure("Identity.MfaAlreadyEnrolled", "Tài khoản đã bật xác thực hai lớp.");

    /// <summary>Refresh token không hợp lệ, hết hạn, đã thu hồi hoặc bị dùng lại (không phân biệt để không lộ trạng thái).</summary>
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Identity.InvalidRefreshToken", "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Refresh token đã xoay bị đưa lại: dấu hiệu bị đánh cắp, người gọi thu hồi cả phiên rồi trả <see cref="InvalidRefreshToken"/>.</summary>
    public static readonly Error RefreshTokenReused = Error.Unauthorized("Identity.RefreshTokenReused", "Refresh token đã được dùng.");

    /// <summary>Thao tác cần người gọi đã đăng nhập (claim <c>sub</c> là id tài khoản).</summary>
    public static readonly Error Unauthenticated = Error.Unauthorized("Identity.Unauthenticated", "Cần đăng nhập.");

    /// <summary>Tài khoản, vai trò hoặc phân công cần thu hồi không tồn tại.</summary>
    public static readonly Error AssignmentNotFound = Error.NotFound("Identity.AssignmentNotFound", "Không tìm thấy tài khoản, vai trò hoặc phân công.");

    /// <summary>Phạm vi phân công sai dạng.</summary>
    public static readonly Error AssignmentInvalidScope = Error.Validation("Identity.AssignmentInvalid", "Phạm vi phải là *, wh:<id> hoặc farm:<id>.");

    /// <summary>Vai trò khách gán cho nhân viên (và ngược lại).</summary>
    public static readonly Error AssignmentKindMismatch = Error.Validation("Identity.AssignmentInvalid", "Vai trò không áp dụng cho loại tài khoản này.");

    /// <summary>Người gọi thiếu quyền trong phạm vi, hoặc tự đổi phân công của mình (không lộ lý do cụ thể).</summary>
    public static readonly Error AssignmentForbidden = Error.Forbidden("Auth.Forbidden", "Không có quyền thực hiện thao tác.");

    /// <summary>Locale không đúng dạng BCP 47 (ví dụ <c>vi</c>, <c>en</c>, <c>zh-Hant</c>).</summary>
    public static readonly Error InvalidLocale = Error.Validation("Identity.InvalidLocale", "Mã ngôn ngữ không hợp lệ.");

    /// <summary>Múi giờ không phải mã IANA có thật (ví dụ <c>Asia/Ho_Chi_Minh</c>).</summary>
    public static readonly Error InvalidTimeZone = Error.Validation("Identity.InvalidTimeZone", "Múi giờ không hợp lệ.");
}
