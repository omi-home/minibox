using System.Security.Cryptography;
using System.Text;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;
using Npgsql;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Đăng nhập mật khẩu trên <c>identity.account</c>, bộ đếm sai ở <c>identity.login_throttle</c>.</summary>
/// <param name="dataSource">Kết nối role <c>identity_app</c>.</param>
/// <param name="hasher">Băm/kiểm mật khẩu.</param>
/// <param name="refreshTokens">Mở phiên khi đúng mật khẩu.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Tài khoản không tồn tại hoặc không có mật khẩu vẫn chạy Verify với bản băm giả để thời gian phản hồi không lộ tài khoản.
/// Không log định danh, mật khẩu hay token.</remarks>
public sealed class PostgresPasswordLogin(NpgsqlDataSource dataSource, IPasswordHasher hasher, IRefreshTokens refreshTokens, TimeProvider time)
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
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        await using (var locked = new NpgsqlCommand("SELECT 1 FROM login_throttle WHERE key = ANY($1) AND locked_until > $2", conn)
                     { Parameters = { new() { Value = new[] { accountKey, ipKey } }, new() { Value = now } } })
            if (await locked.ExecuteScalarAsync(cancellationToken) is not null)
                return Throttled;

        Guid? id = null;
        AccountKind kind = default;
        string? hash = null;
        if (login.Length > 0)
        {
            await using var find = new NpgsqlCommand("SELECT id, kind, password_hash FROM account WHERE email = $1 OR username = $1", conn)
                { Parameters = { new() { Value = login } } };
            await using var r = await find.ExecuteReaderAsync(cancellationToken);
            if (await r.ReadAsync(cancellationToken))
            {
                id = r.GetGuid(0);
                kind = Enum.Parse<AccountKind>(r.GetString(1), ignoreCase: true);
                hash = r.IsDBNull(2) ? null : r.GetString(2);
            }
        }

        // Mật khẩu rỗng/quá dài bị từ chối không băm (chống DoS); không phụ thuộc tài khoản nên không lộ gì qua thời gian.
        var verification = password is { Length: > 0 and <= PasswordPolicy.MaxLength }
            ? hasher.Verify(hash ?? _dummyHash.Value, password)
            : PasswordVerification.Failed;
        if (hash is null || verification == PasswordVerification.Failed)
        {
            await RecordFailureAsync(conn, accountKey, LoginThrottle.AccountThreshold, now, cancellationToken);
            await RecordFailureAsync(conn, ipKey, LoginThrottle.IpThreshold, now, cancellationToken);
            return InvalidCredentials;
        }

        await using (var reset = new NpgsqlCommand("DELETE FROM login_throttle WHERE key = $1", conn) { Parameters = { new() { Value = accountKey } } })
            await reset.ExecuteNonQueryAsync(cancellationToken); // bộ đếm IP giữ nguyên: đăng nhập đúng một tài khoản không xoá dấu dò tài khoản khác
        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            await using var rehash = new NpgsqlCommand("UPDATE account SET password_hash = $2, version = version + 1 WHERE id = $1", conn)
                { Parameters = { new() { Value = id!.Value }, new() { Value = hasher.Hash(password!) } } };
            await rehash.ExecuteNonQueryAsync(cancellationToken);
        }
        var session = await refreshTokens.IssueAsync(id!.Value, kind, ["pwd"], cancellationToken);
        return new LoginSuccess(id.Value, kind, session);
    }

    static async Task RecordFailureAsync(NpgsqlConnection conn, string key, int threshold, DateTimeOffset now, CancellationToken ct)
    {
        await using var count = new NpgsqlCommand("""
            INSERT INTO login_throttle (key, failures, last_failure_at) VALUES ($1, 1, $2)
            ON CONFLICT (key) DO UPDATE SET
              failures = CASE WHEN login_throttle.last_failure_at < $3 THEN 1 ELSE login_throttle.failures + 1 END,
              last_failure_at = $2
            RETURNING failures
            """, conn) { Parameters = { new() { Value = key }, new() { Value = now }, new() { Value = now - LoginThrottle.Window } } };
        var failures = (int)(await count.ExecuteScalarAsync(ct))!;
        if (LoginThrottle.LockFor(failures, threshold) is not { } lockFor)
            return;
        await using var lockCmd = new NpgsqlCommand("UPDATE login_throttle SET locked_until = $2 WHERE key = $1", conn)
            { Parameters = { new() { Value = key }, new() { Value = now + lockFor } } };
        await lockCmd.ExecuteNonQueryAsync(ct);
    }
}
