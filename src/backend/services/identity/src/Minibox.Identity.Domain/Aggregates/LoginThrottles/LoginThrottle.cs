using System.Security.Cryptography;
using System.Text;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.Identity.Domain.Aggregates.LoginThrottles;

/// <summary>Bộ đếm nhập sai của một khoá (định danh đăng nhập, IP hoặc MFA của tài khoản) (spec §3.1): quá ngưỡng thì khoá tạm,
/// mỗi lần sai thêm khoá gấp đôi tới <see cref="MaxLock"/>; bộ đếm quên sau <see cref="Window"/> không sai.</summary>
/// <remarks>Nạp kèm khoá advisory theo khoá đếm để hai lần sai song song không cùng lọt (đếm tuần tự).
/// ponytail: khoá theo định danh cho phép kẻ khác cố tình khoá tạm tài khoản nạn nhân (tối đa 30 phút mỗi lần);
/// nếu bị lạm dụng thì thêm CAPTCHA/thiết bị tin cậy thay vì nới ngưỡng.</remarks>
public sealed class LoginThrottle : AggregateRoot<string>
{
    /// <summary>Số lần sai liên tiếp của một định danh (hoặc mã MFA của một tài khoản) trước khi khoá.</summary>
    public const int AccountThreshold = 5;

    /// <summary>Số lần sai từ một IP (mọi định danh) trước khi khoá IP.</summary>
    public const int IpThreshold = 20;

    /// <summary>Thời gian khoá lần đầu.</summary>
    public static readonly TimeSpan BaseLock = TimeSpan.FromMinutes(1);

    /// <summary>Thời gian khoá tối đa.</summary>
    public static readonly TimeSpan MaxLock = TimeSpan.FromMinutes(30);

    /// <summary>Không sai thêm trong khoảng này thì bộ đếm về 0.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    LoginThrottle(string id) : base(id) { }

    /// <summary>Số lần sai trong cửa sổ hiện tại.</summary>
    public int Failures { get; private set; }

    /// <summary>Lần sai gần nhất.</summary>
    public DateTimeOffset LastFailureAt { get; private set; }

    /// <summary>Khoá tới lúc này; <c>null</c> hoặc đã qua là không khoá.</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Phiên bản (optimistic concurrency).</summary>
    public long Version { get; private set; }

    /// <summary>Tạo bộ đếm rỗng cho khoá.</summary>
    /// <param name="key">Khoá từ <see cref="AccountKey"/>, <see cref="IpKey"/> hoặc <see cref="MfaKey"/>.</param>
    /// <returns>Bộ đếm chưa có lần sai.</returns>
    public static LoginThrottle Create(string key) => new(key);

    /// <summary>Khoá theo định danh đăng nhập: lưu SHA-256, không lưu email/username thô.</summary>
    /// <param name="normalizedLogin">Định danh đã chuẩn hoá.</param>
    /// <returns><c>acct:</c> + hex SHA-256.</returns>
    public static string AccountKey(string normalizedLogin) =>
        "acct:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedLogin)));

    /// <summary>Khoá theo IP người dùng cuối.</summary>
    /// <param name="ip">IP do BFF chuyển tiếp.</param>
    /// <returns><c>ip:</c> + IP.</returns>
    public static string IpKey(string ip) => "ip:" + ip;

    /// <summary>Khoá cho mã MFA của tài khoản.</summary>
    /// <param name="accountId">Tài khoản.</param>
    /// <returns><c>mfa:</c> + id.</returns>
    public static string MfaKey(Guid accountId) => "mfa:" + accountId;

    /// <summary>Đang bị khoá tại thời điểm <paramref name="now"/>.</summary>
    /// <param name="now">Thời điểm kiểm.</param>
    /// <returns><c>true</c> nếu còn trong thời gian khoá.</returns>
    public bool IsLocked(DateTimeOffset now) => LockedUntil > now;

    /// <summary>Ghi một lần sai: đếm lại từ 1 nếu lần sai trước đã quá <see cref="Window"/>, tới ngưỡng thì khoá.</summary>
    /// <param name="threshold"><see cref="AccountThreshold"/> hoặc <see cref="IpThreshold"/>.</param>
    /// <param name="now">Thời điểm sai.</param>
    public void RecordFailure(int threshold, DateTimeOffset now)
    {
        Failures = LastFailureAt < now - Window ? 1 : Failures + 1;
        LastFailureAt = now;
        if (LockFor(Failures, threshold) is { } lockFor)
            LockedUntil = now + lockFor;
    }

    /// <summary>Xoá bộ đếm sau khi nhập đúng.</summary>
    public void Reset()
    {
        Failures = 0;
        LockedUntil = null;
    }

    /// <summary>Tính thời gian khoá sau một lần sai.</summary>
    /// <param name="failures">Số lần sai tính cả lần này.</param>
    /// <param name="threshold">Ngưỡng của loại bộ đếm.</param>
    /// <returns><c>null</c> khi chưa tới ngưỡng; ngược lại <see cref="BaseLock"/> × 2^(lần vượt ngưỡng), tối đa <see cref="MaxLock"/>.</returns>
    public static TimeSpan? LockFor(int failures, int threshold)
    {
        if (failures < threshold)
            return null;
        var lockFor = BaseLock * Math.Pow(2, Math.Min(failures - threshold, 16));
        return lockFor < MaxLock ? lockFor : MaxLock;
    }
}
