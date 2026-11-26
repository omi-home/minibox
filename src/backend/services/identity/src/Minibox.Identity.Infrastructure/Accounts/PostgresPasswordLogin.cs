using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Đăng nhập mật khẩu trên <c>identity.account</c>, bộ đếm sai ở <c>identity.login_throttle</c>.</summary>
/// <param name="dbs">Factory DbContext identity (role <c>identity_app</c>).</param>
/// <param name="hasher">Băm/kiểm mật khẩu.</param>
/// <param name="refreshTokens">Mở phiên khi đúng mật khẩu.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Tài khoản không tồn tại hoặc không có mật khẩu vẫn chạy Verify với bản băm giả để thời gian phản hồi không lộ tài khoản.
/// Không log định danh, mật khẩu hay token.</remarks>
public sealed class PostgresPasswordLogin(IDbContextFactory<IdentityDbContext> dbs, IPasswordHasher hasher, IRefreshTokens refreshTokens, TimeProvider time)
    : IPasswordLogin
{
    static readonly Error InvalidCredentials =
        Error.Failure(IPasswordLogin.InvalidCredentialsCode, "Email/tên đăng nhập hoặc mật khẩu không đúng.");
    static readonly Error Throttled = Error.Failure(IPasswordLogin.ThrottledCode, "Đăng nhập sai nhiều lần, vui lòng thử lại sau ít phút.");

    readonly Lazy<string> _dummyHash = new(() => hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))));

    /// <inheritdoc />
    public async Task<Result<LoginSuccess>> LoginAsync(string? identifier, string? password, string ip, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var login = (identifier ?? "").Trim().ToLowerInvariant();
        var accountKey = "acct:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(login)));
        var ipKey = "ip:" + ip;
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);

        if (await IsLockedAsync(db, [accountKey, ipKey], now, cancellationToken))
            return Throttled;

        var account = login.Length == 0 ? null : await db.Set<Account>().AsNoTracking()
            .Where(a => a.Email == login || a.Username == login)
            .Select(a => new { a.Id, a.Kind, a.PasswordHash })
            .FirstOrDefaultAsync(cancellationToken);
        var hash = account?.PasswordHash;

        // Mật khẩu rỗng/quá dài bị từ chối không băm (chống DoS); không phụ thuộc tài khoản nên không lộ gì qua thời gian.
        var verification = password is { Length: > 0 and <= PasswordPolicy.MaxLength }
            ? hasher.Verify(hash ?? _dummyHash.Value, password)
            : PasswordVerification.Failed;
        if (account is null || hash is null || verification == PasswordVerification.Failed)
        {
            await RecordFailureAsync(db, accountKey, LoginThrottle.AccountThreshold, now, cancellationToken);
            await RecordFailureAsync(db, ipKey, LoginThrottle.IpThreshold, now, cancellationToken);
            return InvalidCredentials;
        }

        // Bộ đếm IP giữ nguyên: đăng nhập đúng một tài khoản không xoá dấu dò tài khoản khác.
        await db.Set<LoginThrottleRow>().Where(t => t.Key == accountKey).ExecuteDeleteAsync(cancellationToken);
        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            var rehashed = hasher.Hash(password!);
            await db.Set<Account>().Where(a => a.Id == account.Id).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.PasswordHash, rehashed)
                .SetProperty(a => a.Version, a => a.Version + 1), cancellationToken);
        }
        var session = await refreshTokens.IssueAsync(account.Id, account.Kind, ["pwd"], cancellationToken);
        return new LoginSuccess(account.Id, account.Kind, session);
    }

    internal static Task<bool> IsLockedAsync(IdentityDbContext db, string[] keys, DateTimeOffset now, CancellationToken ct) =>
        db.Set<LoginThrottleRow>().AnyAsync(t => keys.Contains(t.Key) && t.LockedUntil > now, ct);

    internal static async Task RecordFailureAsync(IdentityDbContext db, string key, int threshold, DateTimeOffset now, CancellationToken ct)
    {
        // UPSERT đếm nguyên tử trong một câu lệnh (không đọc-rồi-ghi).
        var windowStart = now - LoginThrottle.Window;
        var failures = (await db.Database.SqlQuery<int>($"""
            INSERT INTO login_throttle (key, failures, last_failure_at) VALUES ({key}, 1, {now})
            ON CONFLICT (key) DO UPDATE SET
              failures = CASE WHEN login_throttle.last_failure_at < {windowStart} THEN 1 ELSE login_throttle.failures + 1 END,
              last_failure_at = {now}
            RETURNING failures AS "Value"
            """).ToListAsync(ct)).Single();
        if (LoginThrottle.LockFor(failures, threshold) is not { } lockFor)
            return;
        var until = now + lockFor;
        await db.Set<LoginThrottleRow>().Where(t => t.Key == key).ExecuteUpdateAsync(s => s.SetProperty(t => t.LockedUntil, until), ct);
    }
}
