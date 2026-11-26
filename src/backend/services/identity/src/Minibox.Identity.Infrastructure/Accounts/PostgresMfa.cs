using System.Security.Cryptography;
using System.Text;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;
using Npgsql;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>MFA TOTP trên <c>identity.mfa_totp</c> và <c>identity.mfa_recovery_code</c>; sai mã đếm ở <c>identity.login_throttle</c>.</summary>
/// <param name="dataSource">Kết nối role <c>identity_app</c>.</param>
/// <param name="encryptionKey">Khóa AES-256 (32 byte) mã hóa secret TOTP, đọc từ secret <c>Mfa:EncryptionKey</c>.</param>
/// <param name="refreshTokens">Mở phiên sau khi xác minh.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Dòng <c>mfa_totp</c> khóa <c>FOR UPDATE</c> khi kiểm mã nên hai request song song cùng mã chỉ một cái thành công.
/// Không log secret, mã hay mã khôi phục.</remarks>
public sealed class PostgresMfa(NpgsqlDataSource dataSource, byte[] encryptionKey, IRefreshTokens refreshTokens, TimeProvider time) : IMfa
{
    const string Issuer = "Minibox";
    const int NonceSize = 12, TagSize = 16;
    static readonly Error InvalidCode = Error.Failure(IMfa.InvalidCodeCode, "Mã xác thực không đúng hoặc đã được dùng.");
    static readonly Error NotEnrolled = Error.Failure(IMfa.NotEnrolledCode, "Tài khoản chưa bật xác thực hai lớp.");
    static readonly Error AlreadyEnrolled = Error.Failure(IMfa.AlreadyEnrolledCode, "Tài khoản đã bật xác thực hai lớp.");
    static readonly Error Throttled = Error.Failure(IPasswordLogin.ThrottledCode, "Nhập sai mã nhiều lần, vui lòng thử lại sau ít phút.");

    readonly byte[] _key = encryptionKey.Length == 32
        ? encryptionKey
        : throw new ArgumentException("Khóa mã hóa MFA phải dài 32 byte (AES-256).", nameof(encryptionKey));

    /// <inheritdoc />
    public async Task<Result<MfaEnrollment>> BeginEnrollmentAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var find = new NpgsqlCommand("""
            SELECT coalesce(a.email, a.username), m.confirmed_at IS NOT NULL
            FROM account a LEFT JOIN mfa_totp m ON m.account_id = a.id WHERE a.id = $1
            """, conn) { Parameters = { new() { Value = accountId } } };
        string label;
        await using (var r = await find.ExecuteReaderAsync(cancellationToken))
        {
            if (!await r.ReadAsync(cancellationToken))
                return NotEnrolled;
            if (r.GetBoolean(1))
                return AlreadyEnrolled;
            label = r.GetString(0);
        }

        var secret = RandomNumberGenerator.GetBytes(Totp.SecretLength);
        await using var upsert = new NpgsqlCommand("""
            INSERT INTO mfa_totp (account_id, secret_enc) VALUES ($1, $2)
            ON CONFLICT (account_id) DO UPDATE SET secret_enc = $2, last_used_step = NULL WHERE mfa_totp.confirmed_at IS NULL
            """, conn) { Parameters = { new() { Value = accountId }, new() { Value = Encrypt(accountId, secret) } } };
        if (await upsert.ExecuteNonQueryAsync(cancellationToken) == 0)
            return AlreadyEnrolled; // vừa được xác nhận bởi request song song
        var base32 = Totp.Base32(secret);
        var uri = $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(label)}?secret={base32}&issuer={Issuer}&algorithm=SHA1&digits=6&period=30";
        return new MfaEnrollment(base32, uri);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ConfirmEnrollmentAsync(Guid accountId, string code, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var throttleKey = ThrottleKey(accountId);
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await PostgresPasswordLogin.IsLockedAsync(conn, [throttleKey], now, cancellationToken))
            return Throttled;

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await using var select = new NpgsqlCommand("SELECT secret_enc FROM mfa_totp WHERE account_id = $1 AND confirmed_at IS NULL FOR UPDATE", conn, tx)
            { Parameters = { new() { Value = accountId } } };
        if (await select.ExecuteScalarAsync(cancellationToken) is not byte[] encrypted)
            return NotEnrolled;
        if (Totp.Match(Decrypt(accountId, encrypted), code, now, null) is not { } step)
        {
            await tx.CommitAsync(cancellationToken);
            await PostgresPasswordLogin.RecordFailureAsync(conn, throttleKey, LoginThrottle.AccountThreshold, now, cancellationToken);
            return InvalidCode;
        }

        await ExecAsync(conn, tx, "UPDATE mfa_totp SET confirmed_at = $2, last_used_step = $3 WHERE account_id = $1", cancellationToken, accountId, now, step);
        await ExecAsync(conn, tx, "DELETE FROM mfa_recovery_code WHERE account_id = $1", cancellationToken, accountId);
        var codes = Enumerable.Range(0, MfaPolicy.RecoveryCodeCount).Select(_ => MfaPolicy.NewRecoveryCode()).ToArray();
        await using (var insert = new NpgsqlCommand("INSERT INTO mfa_recovery_code (account_id, code_hash) SELECT $1, unnest($2::bytea[])", conn, tx)
                     { Parameters = { new() { Value = accountId }, new() { Value = codes.Select(c => HashRecoveryCode(MfaPolicy.NormalizeRecoveryCode(c)!)).ToArray() } } })
            await insert.ExecuteNonQueryAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await ResetAsync(conn, throttleKey, cancellationToken);
        return codes;
    }

    /// <inheritdoc />
    public async Task<Result<RefreshSession>> VerifyAsync(Guid accountId, string code, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var throttleKey = ThrottleKey(accountId);
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        if (await PostgresPasswordLogin.IsLockedAsync(conn, [throttleKey], now, cancellationToken))
            return Throttled;

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await using var select = new NpgsqlCommand("""
            SELECT a.kind, m.secret_enc, m.last_used_step
            FROM mfa_totp m JOIN account a ON a.id = m.account_id
            WHERE m.account_id = $1 AND m.confirmed_at IS NOT NULL FOR UPDATE OF m
            """, conn, tx) { Parameters = { new() { Value = accountId } } };
        AccountKind kind;
        byte[] encrypted;
        long? lastUsed;
        await using (var r = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (!await r.ReadAsync(cancellationToken))
                return NotEnrolled;
            kind = Enum.Parse<AccountKind>(r.GetString(0), ignoreCase: true);
            encrypted = r.GetFieldValue<byte[]>(1);
            lastUsed = r.IsDBNull(2) ? null : r.GetInt64(2);
        }

        var ok = (code ?? "").Trim() is { Length: 6 } totp
            ? Totp.Match(Decrypt(accountId, encrypted), totp, now, lastUsed) is { } step
              && await ExecAsync(conn, tx, "UPDATE mfa_totp SET last_used_step = $2 WHERE account_id = $1", cancellationToken, accountId, step) == 1
            : MfaPolicy.NormalizeRecoveryCode(code ?? "") is { } recovery
              && await ExecAsync(conn, tx, "UPDATE mfa_recovery_code SET used_at = $3 WHERE account_id = $1 AND code_hash = $2 AND used_at IS NULL",
                  cancellationToken, accountId, HashRecoveryCode(recovery), now) == 1;
        await tx.CommitAsync(cancellationToken);
        if (!ok)
        {
            await PostgresPasswordLogin.RecordFailureAsync(conn, throttleKey, LoginThrottle.AccountThreshold, now, cancellationToken);
            return InvalidCode;
        }

        await ResetAsync(conn, throttleKey, cancellationToken);
        // ponytail: amr cố định pwd+mfa vì hiện chỉ có đăng nhập mật khẩu; thêm tham số phương thức gốc khi có đăng nhập xã hội.
        return await refreshTokens.IssueAsync(accountId, kind, ["pwd", "mfa"], cancellationToken);
    }

    static string ThrottleKey(Guid accountId) => "mfa:" + accountId;

    static async Task ResetAsync(NpgsqlConnection conn, string key, CancellationToken ct) =>
        await ExecAsync(conn, null, "DELETE FROM login_throttle WHERE key = $1", ct, key);

    static async Task<int> ExecAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, CancellationToken ct, params object[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var arg in args)
            cmd.Parameters.Add(new() { Value = arg });
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    static byte[] HashRecoveryCode(string normalized) => SHA256.HashData(Encoding.ASCII.GetBytes(normalized));

    byte[] Encrypt(Guid accountId, byte[] secret)
    {
        var output = new byte[NonceSize + TagSize + secret.Length];
        RandomNumberGenerator.Fill(output.AsSpan(0, NonceSize));
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(output.AsSpan(0, NonceSize), secret, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize), accountId.ToByteArray());
        return output;
    }

    // Ciphertext bị sửa hoặc chép sang tài khoản khác → AuthenticationTagMismatchException (lỗi dữ liệu, không phải sai mã).
    byte[] Decrypt(Guid accountId, byte[] encrypted)
    {
        var secret = new byte[encrypted.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(encrypted.AsSpan(0, NonceSize), encrypted.AsSpan(NonceSize + TagSize), encrypted.AsSpan(NonceSize, TagSize), secret, accountId.ToByteArray());
        return secret;
    }
}
