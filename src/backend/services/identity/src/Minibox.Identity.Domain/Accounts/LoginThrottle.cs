namespace Minibox.Identity.Domain.Accounts;

/// <summary>Giới hạn đăng nhập sai (spec §3.1): đếm riêng theo định danh đăng nhập và theo IP; quá ngưỡng thì khóa tạm,
/// mỗi lần sai thêm khóa gấp đôi tới <see cref="MaxLock"/>. Bộ đếm quên sau <see cref="Window"/> không sai.</summary>
/// <remarks>ponytail: khóa theo định danh cho phép kẻ khác cố tình khóa tạm tài khoản nạn nhân (tối đa 30 phút mỗi lần);
/// nếu bị lạm dụng thì thêm CAPTCHA/thiết bị tin cậy thay vì nới ngưỡng.</remarks>
public static class LoginThrottle
{
    /// <summary>Số lần sai liên tiếp của một định danh trước khi khóa.</summary>
    public const int AccountThreshold = 5;

    /// <summary>Số lần sai từ một IP (mọi định danh) trước khi khóa IP.</summary>
    public const int IpThreshold = 20;

    /// <summary>Thời gian khóa lần đầu.</summary>
    public static readonly TimeSpan BaseLock = TimeSpan.FromMinutes(1);

    /// <summary>Thời gian khóa tối đa.</summary>
    public static readonly TimeSpan MaxLock = TimeSpan.FromMinutes(30);

    /// <summary>Không sai thêm trong khoảng này thì bộ đếm về 0.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>Tính thời gian khóa sau một lần sai.</summary>
    /// <param name="failures">Số lần sai tính cả lần này.</param>
    /// <param name="threshold">Ngưỡng của loại bộ đếm (<see cref="AccountThreshold"/> hoặc <see cref="IpThreshold"/>).</param>
    /// <returns><c>null</c> khi chưa tới ngưỡng; ngược lại <see cref="BaseLock"/> × 2^(lần vượt ngưỡng), tối đa <see cref="MaxLock"/>.</returns>
    public static TimeSpan? LockFor(int failures, int threshold)
    {
        if (failures < threshold)
            return null;
        var lockFor = BaseLock * Math.Pow(2, Math.Min(failures - threshold, 16));
        return lockFor < MaxLock ? lockFor : MaxLock;
    }
}
