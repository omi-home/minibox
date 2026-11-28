using Isopoh.Cryptography.Argon2;
using Minibox.Identity.Application.Abstractions;

namespace Minibox.Identity.Infrastructure.Security;

/// <summary>Băm mật khẩu Argon2id theo mức tối thiểu OWASP: 19 MiB, t=2, p=1 (spec §3.1).</summary>
/// <remarks>Đổi tham số → bản băm cũ vẫn kiểm được (tham số nằm trong chuỗi PHC) và được báo
/// <see cref="PasswordVerification.SuccessRehashNeeded"/> để băm lại khi đăng nhập.</remarks>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    const int MemoryKib = 19 * 1024;
    const int Iterations = 2;
    const int Parallelism = 1;
    const string CurrentPrefix = "$argon2id$v=19$m=19456,t=2,p=1$";

    /// <inheritdoc/>
    public string Hash(string password) =>
        Argon2.Hash(password, Iterations, MemoryKib, Parallelism, Argon2Type.HybridAddressing);

    /// <inheritdoc/>
    public PasswordVerification Verify(string hash, string password)
    {
        if (!hash.StartsWith("$argon2id$", StringComparison.Ordinal)) return PasswordVerification.Failed;
        try
        {
            if (!Argon2.Verify(hash, password)) return PasswordVerification.Failed;
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            return PasswordVerification.Failed;
        }
        return hash.StartsWith(CurrentPrefix, StringComparison.Ordinal)
            ? PasswordVerification.Success
            : PasswordVerification.SuccessRehashNeeded;
    }
}
