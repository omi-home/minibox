using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>MFA TOTP trên <c>identity.mfa_totp</c> và <c>identity.mfa_recovery_code</c>; sai mã đếm ở <c>identity.login_throttle</c>.</summary>
/// <param name="dbs">Factory DbContext identity (role <c>identity_app</c>).</param>
/// <param name="encryptionKey">Khóa AES-256 (32 byte) mã hóa secret TOTP, đọc từ secret <c>Mfa:EncryptionKey</c>.</param>
/// <param name="refreshTokens">Mở phiên sau khi xác minh.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Dòng <c>mfa_totp</c> khóa <c>FOR UPDATE</c> khi kiểm mã nên hai request song song cùng mã chỉ một cái thành công.
/// Không log secret, mã hay mã khôi phục.</remarks>
public sealed class PostgresMfa(IDbContextFactory<IdentityDbContext> dbs, byte[] encryptionKey, IRefreshTokens refreshTokens, TimeProvider time) : IMfa
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
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        var account = await db.Set<Account>().Where(a => a.Id == accountId)
            .Select(a => new { Label = a.Email ?? a.Username, Confirmed = db.Set<MfaTotpRow>().Any(m => m.AccountId == a.Id && m.ConfirmedAt != null) })
            .FirstOrDefaultAsync(cancellationToken);
        if (account is null)
            return NotEnrolled;
        if (account.Confirmed)
            return AlreadyEnrolled;

        var secret = RandomNumberGenerator.GetBytes(Totp.SecretLength);
        var encrypted = Encrypt(accountId, secret);
        if (await db.Database.ExecuteSqlAsync($"""
                INSERT INTO mfa_totp (account_id, secret_enc) VALUES ({accountId}, {encrypted})
                ON CONFLICT (account_id) DO UPDATE SET secret_enc = {encrypted}, last_used_step = NULL WHERE mfa_totp.confirmed_at IS NULL
                """, cancellationToken) == 0)
            return AlreadyEnrolled; // vừa được xác nhận bởi request song song
        var base32 = Totp.Base32(secret);
        var uri = $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(account.Label!)}?secret={base32}&issuer={Issuer}&algorithm=SHA1&digits=6&period=30";
        return new MfaEnrollment(base32, uri);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ConfirmEnrollmentAsync(Guid accountId, string code, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var throttleKey = ThrottleKey(accountId);
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        if (await PostgresPasswordLogin.IsLockedAsync(db, [throttleKey], now, cancellationToken))
            return Throttled;

        string[] codes;
        await using (var tx = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var totp = (await db.Set<MfaTotpRow>()
                .FromSql($"SELECT * FROM mfa_totp WHERE account_id = {accountId} AND confirmed_at IS NULL FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault();
            if (totp is null)
                return NotEnrolled;
            if (Totp.Match(Decrypt(accountId, totp.SecretEnc), code, now, null) is not { } step)
            {
                await tx.CommitAsync(cancellationToken);
                await PostgresPasswordLogin.RecordFailureAsync(db, throttleKey, LoginThrottle.AccountThreshold, now, cancellationToken);
                return InvalidCode;
            }

            totp.ConfirmedAt = now;
            totp.LastUsedStep = step;
            await db.Set<MfaRecoveryCodeRow>().Where(c => c.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
            codes = [.. Enumerable.Range(0, MfaPolicy.RecoveryCodeCount).Select(_ => MfaPolicy.NewRecoveryCode())];
            db.AddRange(codes.Select(c => new MfaRecoveryCodeRow { AccountId = accountId, CodeHash = HashRecoveryCode(MfaPolicy.NormalizeRecoveryCode(c)!) }));
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        await ResetAsync(db, throttleKey, cancellationToken);
        return codes;
    }

    /// <inheritdoc />
    public async Task<Result<RefreshSession>> VerifyAsync(Guid accountId, string code, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var throttleKey = ThrottleKey(accountId);
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        if (await PostgresPasswordLogin.IsLockedAsync(db, [throttleKey], now, cancellationToken))
            return Throttled;

        AccountKind kind;
        bool ok;
        await using (var tx = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            var totp = (await db.Set<MfaTotpRow>()
                .FromSql($"SELECT * FROM mfa_totp WHERE account_id = {accountId} AND confirmed_at IS NOT NULL FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault();
            if (totp is null)
                return NotEnrolled;
            kind = await db.Set<Account>().Where(a => a.Id == accountId).Select(a => a.Kind).SingleAsync(cancellationToken);

            if ((code ?? "").Trim() is { Length: 6 } six)
            {
                var step = Totp.Match(Decrypt(accountId, totp.SecretEnc), six, now, totp.LastUsedStep);
                ok = step is not null;
                if (ok)
                {
                    totp.LastUsedStep = step;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            else
            {
                var hash = MfaPolicy.NormalizeRecoveryCode(code ?? "") is { } recovery ? HashRecoveryCode(recovery) : null;
                ok = hash is not null && await db.Set<MfaRecoveryCodeRow>()
                    .Where(c => c.AccountId == accountId && c.CodeHash == hash && c.UsedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), cancellationToken) == 1;
            }
            await tx.CommitAsync(cancellationToken);
        }
        if (!ok)
        {
            await PostgresPasswordLogin.RecordFailureAsync(db, throttleKey, LoginThrottle.AccountThreshold, now, cancellationToken);
            return InvalidCode;
        }

        await ResetAsync(db, throttleKey, cancellationToken);
        // ponytail: amr cố định pwd+mfa vì hiện chỉ có đăng nhập mật khẩu; thêm tham số phương thức gốc khi có đăng nhập xã hội.
        return await refreshTokens.IssueAsync(accountId, kind, ["pwd", "mfa"], cancellationToken);
    }

    static string ThrottleKey(Guid accountId) => "mfa:" + accountId;

    static Task ResetAsync(IdentityDbContext db, string key, CancellationToken ct) =>
        db.Set<LoginThrottleRow>().Where(t => t.Key == key).ExecuteDeleteAsync(ct);

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
