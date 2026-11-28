using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Infrastructure.Security;

namespace Minibox.Identity.UnitTests;

/// <summary>Băm mật khẩu Argon2id (spec §3.1).</summary>
public class PasswordHasherTests
{
    readonly Argon2idPasswordHasher _hasher = new();

    [Fact(DisplayName = "ac195_1 Mật khẩu lưu Argon2id m=19 MiB, t=2, p=1; bản băm không chứa mật khẩu, mỗi lần băm khác salt")]
    public void ac195_1_hash_is_argon2id_with_owasp_minimum()
    {
        const string password = "tra-sua-tran-chau-42";
        var hash = _hasher.Hash(password);
        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash);
        Assert.DoesNotContain(password, hash);
        Assert.NotEqual(hash, _hasher.Hash(password));
        Assert.Equal(PasswordVerification.Success, _hasher.Verify(hash, password));
    }

    [Fact(DisplayName = "Sai mật khẩu trả Failed")]
    public void wrong_password_fails() =>
        Assert.Equal(PasswordVerification.Failed, _hasher.Verify(_hasher.Hash("tra-sua-tran-chau-42"), "tra-sua-tran-chau-43"));

    [Fact(DisplayName = "Bản băm tham số cũ vẫn đúng nhưng báo cần băm lại")]
    public void old_parameters_need_rehash()
    {
        var old = Isopoh.Cryptography.Argon2.Argon2.Hash("tra-sua-tran-chau-42", timeCost: 1, memoryCost: 8192, parallelism: 1,
            type: Isopoh.Cryptography.Argon2.Argon2Type.HybridAddressing);
        Assert.Equal(PasswordVerification.SuccessRehashNeeded, _hasher.Verify(old, "tra-sua-tran-chau-42"));
    }

    [Fact(DisplayName = "Bản băm hỏng/không phải Argon2id trả Failed, không ném")]
    public void malformed_hash_fails() => Assert.Equal(PasswordVerification.Failed, _hasher.Verify("khong-phai-hash", "x"));
}
