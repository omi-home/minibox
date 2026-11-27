using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Minibox.Identity.Domain.Accounts;

/// <summary>Mã dùng một lần theo thời gian (RFC 6238): HMAC-SHA1, 6 chữ số, bước 30 giây — mặc định của mọi ứng dụng authenticator.</summary>
public static class Totp
{
    /// <summary>Độ dài một bước thời gian.</summary>
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>Số byte secret sinh khi đăng ký (160 bit, khuyến nghị của RFC 4226).</summary>
    public const int SecretLength = 20;

    /// <summary>Tính số thứ tự bước chứa thời điểm.</summary>
    /// <param name="time">Thời điểm.</param>
    /// <returns>Số bước 30 giây kể từ Unix epoch.</returns>
    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / (long)Step.TotalSeconds;

    /// <summary>Tính mã 6 chữ số của một bước.</summary>
    /// <param name="secret">Secret dùng chung với authenticator.</param>
    /// <param name="step">Số thứ tự bước (<see cref="StepAt"/>).</param>
    /// <returns>Mã 6 chữ số, giữ số 0 đầu.</returns>
    public static string Code(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, counter, hash);
        var offset = hash[^1] & 0x0F;
        var value = (BinaryPrimitives.ReadInt32BigEndian(hash[offset..]) & 0x7FFFFFFF) % 1_000_000;
        return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Tìm bước có mã trùng <paramref name="code"/> trong khoảng lệch ±1 bước quanh <paramref name="now"/>.</summary>
    /// <param name="secret">Secret dùng chung.</param>
    /// <param name="code">Mã người dùng nhập.</param>
    /// <param name="now">Thời điểm kiểm.</param>
    /// <param name="lastUsedStep">Bước của mã đã dùng gần nhất; mọi bước ≤ giá trị này bị từ chối (chống phát lại).</param>
    /// <returns>Bước khớp để lưu làm <paramref name="lastUsedStep"/> mới; <c>null</c> nếu không khớp.</returns>
    /// <remarks>So sánh thời gian hằng để không lộ chữ số đúng qua thời gian phản hồi.</remarks>
    public static long? Match(ReadOnlySpan<byte> secret, string code, DateTimeOffset now, long? lastUsedStep)
    {
        if (code is not { Length: 6 } || !code.All(char.IsAsciiDigit))
            return null;
        var input = Encoding.ASCII.GetBytes(code);
        var current = StepAt(now);
        long? match = null;
        for (var step = current - 1; step <= current + 1; step++)
            if (step > (lastUsedStep ?? long.MinValue)
                && CryptographicOperations.FixedTimeEquals(input, Encoding.ASCII.GetBytes(Code(secret, step))))
                match ??= step;
        return match;
    }

    /// <summary>Mã hóa Base32 (RFC 4648, không padding) — định dạng secret trong otpauth URI.</summary>
    /// <param name="data">Dữ liệu.</param>
    /// <returns>Chuỗi chữ hoa A–Z và 2–7.</returns>
    public static string Base32(ReadOnlySpan<byte> data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
                sb.Append(alphabet[(buffer >> (bits -= 5)) & 31]);
        }
        if (bits > 0)
            sb.Append(alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }
}

/// <summary>Quy định MFA (spec §3.5): số mã khôi phục và định dạng của chúng.</summary>
public static class MfaPolicy
{
    /// <summary>Số mã khôi phục cấp mỗi lần đăng ký.</summary>
    public const int RecoveryCodeCount = 10;

    /// <summary>Sinh một mã khôi phục 80 bit dạng <c>XXXX-XXXX-XXXX-XXXX</c> (Base32).</summary>
    /// <returns>Mã hiển thị cho người dùng, chỉ lộ một lần.</returns>
    /// <remarks>80 bit đủ để bản băm SHA-256 không bị dò ngược khi lộ DB, nên không cần băm chậm.</remarks>
    public static string NewRecoveryCode()
    {
        var raw = Totp.Base32(RandomNumberGenerator.GetBytes(10));
        return string.Join('-', Enumerable.Range(0, 4).Select(i => raw.Substring(i * 4, 4)));
    }

    /// <summary>Chuẩn hoá mã khôi phục người dùng nhập: bỏ gạch nối, khoảng trắng; chữ hoa.</summary>
    /// <param name="code">Mã nhập.</param>
    /// <returns>16 ký tự Base32, hoặc <c>null</c> nếu sai định dạng.</returns>
    public static string? NormalizeRecoveryCode(string code)
    {
        var normalized = new string([.. code.Where(c => c is not ('-' or ' '))]).ToUpperInvariant();
        return normalized.Length == 16 && normalized.All(c => c is >= 'A' and <= 'Z' or >= '2' and <= '7') ? normalized : null;
    }
}
