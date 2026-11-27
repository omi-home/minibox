namespace Minibox.Identity.Domain.Accounts;

/// <summary>Thời hạn phiên theo loại tài khoản (spec §3.2): khách 30 ngày trượt; nhân viên hết hạn khi không hoạt động
/// 30 phút và tối đa 8 giờ kể từ đăng nhập.</summary>
public static class SessionPolicy
{
    /// <summary>Thời gian không dùng refresh token thì phiên hết hạn.</summary>
    /// <param name="kind">Loại tài khoản.</param>
    /// <returns>30 phút cho nhân viên, 30 ngày cho khách.</returns>
    public static TimeSpan IdleTimeout(AccountKind kind) => kind == AccountKind.Staff ? TimeSpan.FromMinutes(30) : TimeSpan.FromDays(30);

    /// <summary>Thời gian tối đa của một phiên kể từ đăng nhập.</summary>
    /// <param name="kind">Loại tài khoản.</param>
    /// <returns>8 giờ cho nhân viên; <c>null</c> (không giới hạn tuyệt đối) cho khách.</returns>
    public static TimeSpan? MaxLifetime(AccountKind kind) => kind == AccountKind.Staff ? TimeSpan.FromHours(8) : null;
}
