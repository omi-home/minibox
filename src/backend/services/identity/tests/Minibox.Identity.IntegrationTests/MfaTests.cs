using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Features.Mfa.Commands.BeginMfaEnrollment;
using Minibox.Identity.Application.Features.Mfa.Commands.ConfirmMfaEnrollment;
using Minibox.Identity.Application.Features.Mfa.Commands.VerifyMfa;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Đăng ký/xác minh TOTP và mã khôi phục qua pipeline Mediator thật trên PostgreSQL tạm (spec §3.5).</summary>
public sealed class MfaTests : IAsyncLifetime
{
    readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero));
    readonly TestUser _user = new();
    readonly Guid _staff = Guid.CreateVersion7();
    IdentityHost _host = null!;
    WebApplicationFactory<Program> _api = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await IdentityHost.StartAsync();
        await _host.Exec($"INSERT INTO account (id, kind, email) VALUES ('{_staff}', 'staff', 'an@minibox.vn')");
        _user.UserId = _staff.ToString();
        _api = _host.CreateApi(s => s.AddSingleton<TimeProvider>(_time).AddSingleton<ICurrentUser>(_user));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    Task<Result<MfaEnrollment>> Begin() => IdentityHost.Send<Result<MfaEnrollment>>(_api, new BeginMfaEnrollment());

    Task<Result<IReadOnlyList<string>>> Confirm(string code) => IdentityHost.Send<Result<IReadOnlyList<string>>>(_api, new ConfirmMfaEnrollment(code));

    Task<Result<TokenResponse>> Verify(string code) => IdentityHost.Send<Result<TokenResponse>>(_api, new VerifyMfa(code, null));

    static byte[] SecretOf(MfaEnrollment e) => Base32Decode(e.Secret);

    internal static byte[] Base32Decode(string s)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(s.Select(c => Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0')));
        return [.. Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2))];
    }

    string CodeNow(byte[] secret) => Totp.Code(secret, Totp.StepAt(_time.Now));

    async Task<(byte[] Secret, IReadOnlyList<string> RecoveryCodes)> Enroll()
    {
        var secret = SecretOf((await Begin()).Value);
        var codes = (await Confirm(CodeNow(secret))).Value;
        _time.Now += Totp.Step; // mã vừa dùng để xác nhận không dùng lại được
        return (secret, codes);
    }

    [Fact(DisplayName = "Đăng ký trả secret và otpauth URI; xác nhận đúng mã trả 10 mã khôi phục khác nhau và ghi audit MfaEnabled")]
    public async Task enrollment_flow()
    {
        var enrollment = await Begin();
        Assert.True(enrollment.IsSuccess);
        Assert.StartsWith("otpauth://totp/Minibox:an%40minibox.vn?secret=" + enrollment.Value.Secret, enrollment.Value.OtpauthUri);
        Assert.Equal(IdentityErrors.InvalidMfaCode.Code, (await Confirm("000000")).Error.Code);

        var codes = (await Confirm(CodeNow(SecretOf(enrollment.Value)))).Value;
        Assert.Equal(MfaPolicy.RecoveryCodeCount, codes.Distinct().Count());
        Assert.Equal(IdentityErrors.MfaAlreadyEnrolled.Code, (await Begin()).Error.Code);
        Assert.Equal(1L, await _host.Scalar<long>($"SELECT count(*) FROM audit_log WHERE action = 'MfaEnabled' AND aggregate_id = '{_staff}'"));
    }

    [Fact(DisplayName = "ac200_2 Mã TOTP sai bị từ chối; mã đúng cấp phiên có amr chứa mfa và auth_time mới")]
    public async Task ac200_2_wrong_code_rejected_correct_code_grants_mfa()
    {
        Assert.Equal(IdentityErrors.MfaNotEnrolled.Code, (await Verify("123456")).Error.Code);
        var (secret, _) = await Enroll();

        Assert.Equal(IdentityErrors.InvalidMfaCode.Code, (await Verify("000000")).Error.Code);

        var code = CodeNow(secret);
        var ok = await Verify(code);
        Assert.True(ok.IsSuccess);
        Assert.Contains("mfa", ok.Value.AuthMethods);
        Assert.Equal(_time.Now.UtcDateTime, await _host.Scalar<DateTime>($"SELECT max(auth_time) FROM refresh_token WHERE account_id = '{_staff}'"));
        Assert.Equal(IdentityErrors.InvalidMfaCode.Code, (await Verify(code)).Error.Code); // không phát lại cùng mã
    }

    [Fact(DisplayName = "Sai mã TOTP liên tiếp bị khóa tạm như đăng nhập (bộ đếm còn lại dù lệnh trả lỗi)")]
    public async Task wrong_codes_are_throttled()
    {
        var (secret, _) = await Enroll();
        for (var i = 0; i < LoginThrottle.AccountThreshold; i++)
            await Verify("000000");
        Assert.Equal(IdentityErrors.MfaThrottled.Code, (await Verify(CodeNow(secret))).Error.Code);
    }

    [Fact(DisplayName = "ac200_3 Mỗi mã khôi phục chỉ dùng được một lần")]
    public async Task ac200_3_recovery_code_single_use()
    {
        var (_, codes) = await Enroll();
        var first = await Verify(codes[0].ToLowerInvariant()); // không phân biệt hoa thường
        Assert.True(first.IsSuccess);
        Assert.Contains("mfa", first.Value.AuthMethods);
        Assert.Equal(IdentityErrors.InvalidMfaCode.Code, (await Verify(codes[0])).Error.Code);
        Assert.True((await Verify(codes[1])).IsSuccess);
    }

    [Fact(DisplayName = "ac200_4 Secret TOTP lưu mã hóa AES-GCM, mã khôi phục lưu bản băm")]
    public async Task ac200_4_secret_encrypted_recovery_hashed()
    {
        var (secret, codes) = await Enroll();
        var stored = await _host.Scalar<byte[]>($"SELECT secret_enc FROM mfa_totp WHERE account_id = '{_staff}'");
        Assert.Equal(12 + 16 + secret.Length, stored.Length); // nonce + tag + ciphertext
        Assert.False(stored.AsSpan().IndexOf(secret) >= 0);

        // Đổi ciphertext sang tài khoản khác (AAD là account id) thì không giải mã được.
        var other = Guid.CreateVersion7();
        await _host.Exec($"INSERT INTO account (id, kind, email) VALUES ('{other}', 'staff', 'binh@minibox.vn')");
        await _host.Exec($"INSERT INTO mfa_totp (account_id, secret_enc, confirmed_at) SELECT '{other}', secret_enc, confirmed_at FROM mfa_totp WHERE account_id = '{_staff}'");
        _user.UserId = other.ToString();
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => Verify(CodeNow(secret)));

        var hashes = new List<byte[]>();
        await using (var cmd = _host.Db.CreateCommand($"SELECT code_hash FROM mfa_recovery_code WHERE account_id = '{_staff}'"))
        await using (var r = await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken))
            while (await r.ReadAsync(TestContext.Current.CancellationToken))
                hashes.Add(r.GetFieldValue<byte[]>(0));
        Assert.Equal(MfaPolicy.RecoveryCodeCount, hashes.Count);
        Assert.Contains(hashes, h => h.SequenceEqual(SHA256.HashData(Encoding.ASCII.GetBytes(codes[0].Replace("-", "")))));
        Assert.DoesNotContain(hashes, h => Encoding.ASCII.GetString(h).Contains(codes[0].Replace("-", "")[..8]));
    }

    [Fact(DisplayName = "Lệnh MFA không có người gọi đăng nhập → Unauthenticated")]
    public async Task requires_signed_in_user()
    {
        _user.UserId = null;
        Assert.Equal(IdentityErrors.Unauthenticated.Code, (await Begin()).Error.Code);
        Assert.Equal(IdentityErrors.Unauthenticated.Code, (await Verify("123456")).Error.Code);
    }
}
