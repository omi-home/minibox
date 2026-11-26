using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.UnitTests;

/// <summary>Khóa tạm tăng dần và độ dài mật khẩu tối đa.</summary>
public class LoginThrottleTests
{
    [Theory(DisplayName = "ac195_3 Khóa tạm tăng gấp đôi sau ngưỡng, tối đa 30 phút")]
    [InlineData(4, null)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(9, 16)]
    [InlineData(10, 30)]
    [InlineData(1000, 30)]
    public void ac195_3_lock_escalates(int failures, int? minutes) =>
        Assert.Equal(minutes is { } m ? TimeSpan.FromMinutes(m) : null, LoginThrottle.LockFor(failures, LoginThrottle.AccountThreshold));

    [Fact(DisplayName = "Mật khẩu quá 128 ký tự hoặc null bị từ chối")]
    public void password_too_long_or_null_rejected()
    {
        Assert.Equal(PasswordPolicy.TooLongCode, PasswordPolicy.Validate(new string('x', 129)).Error.Code);
        Assert.Equal(PasswordPolicy.TooShortCode, PasswordPolicy.Validate(null).Error.Code);
    }
}
