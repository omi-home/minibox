using System.Security.Cryptography;
using System.Text;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure.Accounts;
using Minibox.Identity.Infrastructure.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Đăng ký/xác minh TOTP và mã khôi phục trên PostgreSQL tạm đã migrate (spec §3.5).</summary>
[Collection(MigrateCollection.Name)]
public sealed class MfaTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero));
    readonly Guid _staff = Guid.CreateVersion7();
    NpgsqlDataSource _db = null!;
    PostgresMfa _mfa = null!;

    sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync(Ct);
        // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role identity_app.
        _db = NpgsqlDataSource.Create(_pg.GetConnectionString() + ";Search Path=identity");
        await Exec("CREATE SCHEMA identity; CREATE ROLE identity_app");
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, _pg.GetConnectionString() + ";Search Path=identity");
        try
        {
            Assert.Equal(0, (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
        await Exec($"INSERT INTO account (id, kind, email) VALUES ('{_staff}', 'staff', 'an@minibox.vn')");
        _mfa = new PostgresMfa(_db, RandomNumberGenerator.GetBytes(32), new PostgresRefreshTokens(_db, _time), _time);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }

    async Task Exec(string sql)
    {
        await using var cmd = _db.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    async Task<T?> Scalar<T>(string sql)
    {
        await using var cmd = _db.CreateCommand(sql);
        return (T?)await cmd.ExecuteScalarAsync(Ct);
    }

    static byte[] SecretOf(MfaEnrollment e) => Base32Decode(e.Secret);

    static byte[] Base32Decode(string s)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(s.Select(c => Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0')));
        return [.. Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2))];
    }

    string CodeNow(byte[] secret) => Totp.Code(secret, Totp.StepAt(_time.Now));

    async Task<(byte[] Secret, IReadOnlyList<string> RecoveryCodes)> Enroll()
    {
        var enrollment = (await _mfa.BeginEnrollmentAsync(_staff, Ct)).Value;
        var secret = SecretOf(enrollment);
        var codes = (await _mfa.ConfirmEnrollmentAsync(_staff, CodeNow(secret), Ct)).Value;
        _time.Now += Totp.Step; // mã vừa dùng để xác nhận không dùng lại được
        return (secret, codes);
    }

    [Fact(DisplayName = "Đăng ký trả secret và otpauth URI; xác nhận đúng mã trả 10 mã khôi phục khác nhau")]
    public async Task enrollment_flow()
    {
        var enrollment = await _mfa.BeginEnrollmentAsync(_staff, Ct);
        Assert.True(enrollment.IsSuccess);
        Assert.StartsWith("otpauth://totp/Minibox:an%40minibox.vn?secret=" + enrollment.Value.Secret, enrollment.Value.OtpauthUri);
        Assert.Equal(IMfa.InvalidCodeCode, (await _mfa.ConfirmEnrollmentAsync(_staff, "000000", Ct)).Error.Code);

        var codes = (await _mfa.ConfirmEnrollmentAsync(_staff, CodeNow(SecretOf(enrollment.Value)), Ct)).Value;
        Assert.Equal(MfaPolicy.RecoveryCodeCount, codes.Distinct().Count());
        Assert.Equal(IMfa.AlreadyEnrolledCode, (await _mfa.BeginEnrollmentAsync(_staff, Ct)).Error.Code);
    }

    [Fact(DisplayName = "ac200_2 Mã TOTP sai bị từ chối; mã đúng cấp phiên có amr chứa mfa và auth_time mới")]
    public async Task ac200_2_wrong_code_rejected_correct_code_grants_mfa()
    {
        Assert.Equal(IMfa.NotEnrolledCode, (await _mfa.VerifyAsync(_staff, "123456", Ct)).Error.Code);
        var (secret, _) = await Enroll();

        var wrong = await _mfa.VerifyAsync(_staff, "000000", Ct);
        Assert.Equal(IMfa.InvalidCodeCode, wrong.Error.Code);

        var code = CodeNow(secret);
        var ok = await _mfa.VerifyAsync(_staff, code, Ct);
        Assert.True(ok.IsSuccess);
        Assert.Contains("mfa", ok.Value.AuthMethods);
        Assert.Equal(_time.Now, ok.Value.AuthTime);
        Assert.Equal(_staff, ok.Value.AccountId);
        Assert.Equal(IMfa.InvalidCodeCode, (await _mfa.VerifyAsync(_staff, code, Ct)).Error.Code); // không phát lại cùng mã
    }

    [Fact(DisplayName = "Sai mã TOTP liên tiếp bị khóa tạm như đăng nhập")]
    public async Task wrong_codes_are_throttled()
    {
        var (secret, _) = await Enroll();
        for (var i = 0; i < LoginThrottle.AccountThreshold; i++)
            await _mfa.VerifyAsync(_staff, "000000", Ct);
        Assert.Equal(IPasswordLogin.ThrottledCode, (await _mfa.VerifyAsync(_staff, CodeNow(secret), Ct)).Error.Code);
    }

    [Fact(DisplayName = "ac200_3 Mỗi mã khôi phục chỉ dùng được một lần")]
    public async Task ac200_3_recovery_code_single_use()
    {
        var (_, codes) = await Enroll();
        var first = await _mfa.VerifyAsync(_staff, codes[0].ToLowerInvariant(), Ct); // không phân biệt hoa thường
        Assert.True(first.IsSuccess);
        Assert.Contains("mfa", first.Value.AuthMethods);
        Assert.Equal(IMfa.InvalidCodeCode, (await _mfa.VerifyAsync(_staff, codes[0], Ct)).Error.Code);
        Assert.True((await _mfa.VerifyAsync(_staff, codes[1], Ct)).IsSuccess);
    }

    [Fact(DisplayName = "ac200_4 Secret TOTP lưu mã hóa AES-GCM, mã khôi phục lưu bản băm")]
    public async Task ac200_4_secret_encrypted_recovery_hashed()
    {
        var (secret, codes) = await Enroll();
        var stored = (await Scalar<byte[]>($"SELECT secret_enc FROM mfa_totp WHERE account_id = '{_staff}'"))!;
        Assert.Equal(12 + 16 + secret.Length, stored.Length); // nonce + tag + ciphertext
        Assert.False(stored.AsSpan().IndexOf(secret) >= 0);

        // Đổi ciphertext sang tài khoản khác (AAD là account id) thì không giải mã được.
        var other = Guid.CreateVersion7();
        await Exec($"INSERT INTO account (id, kind, email) VALUES ('{other}', 'staff', 'binh@minibox.vn')");
        await Exec($"INSERT INTO mfa_totp (account_id, secret_enc, confirmed_at) SELECT '{other}', secret_enc, confirmed_at FROM mfa_totp WHERE account_id = '{_staff}'");
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => _mfa.VerifyAsync(other, CodeNow(secret), Ct));

        var hashes = new List<byte[]>();
        await using (var cmd = _db.CreateCommand($"SELECT code_hash FROM mfa_recovery_code WHERE account_id = '{_staff}'"))
        await using (var r = await cmd.ExecuteReaderAsync(Ct))
            while (await r.ReadAsync(Ct))
                hashes.Add(r.GetFieldValue<byte[]>(0));
        Assert.Equal(MfaPolicy.RecoveryCodeCount, hashes.Count);
        Assert.Contains(hashes, h => h.SequenceEqual(SHA256.HashData(Encoding.ASCII.GetBytes(codes[0].Replace("-", "")))));
        Assert.DoesNotContain(hashes, h => Encoding.ASCII.GetString(h).Contains(codes[0].Replace("-", "")[..8]));
    }
}
