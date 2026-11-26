using System.Text;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.UnitTests;

/// <summary>TOTP theo RFC 6238 (HMAC-SHA1, 6 chữ số, bước 30 giây).</summary>
public sealed class TotpTests
{
    static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory(DisplayName = "Khớp vector kiểm thử SHA-1 của RFC 6238 (6 chữ số cuối)")]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void rfc6238_vectors(long unixSeconds, string expected) =>
        Assert.Equal(expected, Totp.Code(RfcSecret, Totp.StepAt(DateTimeOffset.FromUnixTimeSeconds(unixSeconds))));

    [Fact(DisplayName = "Chấp nhận lệch một bước; từ chối mã sai, mã lệch hai bước và mã của bước đã dùng")]
    public void match_window_and_replay()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1111111111);
        var step = Totp.StepAt(now);
        Assert.Equal(step - 1, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 1), now, null));
        Assert.Equal(step + 1, Totp.Match(RfcSecret, Totp.Code(RfcSecret, step + 1), now, null));
        Assert.Null(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step - 2), now, null));
        Assert.Null(Totp.Match(RfcSecret, "000000", now, null));
        Assert.Null(Totp.Match(RfcSecret, "12345", now, null));
        Assert.Null(Totp.Match(RfcSecret, Totp.Code(RfcSecret, step), now, lastUsedStep: step));
    }

    [Fact(DisplayName = "Base32 theo RFC 4648 không padding (định dạng secret của ứng dụng authenticator)")]
    public void base32() => Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Totp.Base32(RfcSecret));
}
