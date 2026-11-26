using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;
using Npgsql;

namespace Minibox.Identity.Infrastructure.Tokens;

/// <summary>Refresh token trên bảng <c>identity.refresh_token</c>: token ngẫu nhiên 256 bit, chỉ lưu SHA-256.</summary>
/// <param name="dataSource">Kết nối role <c>identity_app</c>.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Xoay vòng khoá dòng bằng <c>FOR UPDATE</c>: hai request song song cùng token thì request sau thấy token đã dùng
/// và thu hồi cả phiên (đúng spec, không có thời gian ân hạn).</remarks>
public sealed class PostgresRefreshTokens(NpgsqlDataSource dataSource, TimeProvider time) : IRefreshTokens
{
    static readonly Error Invalid = Error.Failure(IRefreshTokens.InvalidCode, "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <inheritdoc />
    public async Task<RefreshSession> IssueAsync(Guid accountId, AccountKind kind, IReadOnlyList<string> authMethods, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var familyExpires = SessionPolicy.MaxLifetime(kind) is { } max ? now + max : (DateTimeOffset?)null;
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        return await InsertAsync(conn, null, Guid.CreateVersion7(), accountId, kind, now, authMethods, familyExpires, now, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<RefreshSession>> RotateAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await using var select = new NpgsqlCommand("""
            SELECT r.family_id, r.account_id, a.kind, r.auth_time, r.amr, r.expires_at, r.family_expires_at, r.used_at, r.revoked_at
            FROM refresh_token r JOIN account a ON a.id = r.account_id
            WHERE r.token_hash = $1 FOR UPDATE OF r
            """, conn, tx) { Parameters = { new() { Value = Hash(token) } } };
        Guid family, accountId;
        AccountKind kind;
        DateTimeOffset authTime, expiresAt;
        string[] amr;
        DateTimeOffset? familyExpires;
        bool used, revoked;
        await using (var r = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (!await r.ReadAsync(cancellationToken))
                return Invalid;
            family = r.GetGuid(0);
            accountId = r.GetGuid(1);
            kind = Enum.Parse<AccountKind>(r.GetString(2), ignoreCase: true);
            authTime = r.GetFieldValue<DateTimeOffset>(3);
            amr = r.GetFieldValue<string[]>(4);
            expiresAt = r.GetFieldValue<DateTimeOffset>(5);
            familyExpires = r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6);
            used = !r.IsDBNull(7);
            revoked = !r.IsDBNull(8);
        }

        if (revoked || expiresAt <= now)
            return Invalid;
        if (used)
        {
            await RevokeFamilyAsync(conn, tx, family, now, cancellationToken); // token đã xoay bị đưa lại: coi như bị đánh cắp
            await tx.CommitAsync(cancellationToken);
            return Invalid;
        }

        await using (var markUsed = new NpgsqlCommand("UPDATE refresh_token SET used_at = $2 WHERE token_hash = $1", conn, tx)
                     { Parameters = { new() { Value = Hash(token) }, new() { Value = now } } })
            await markUsed.ExecuteNonQueryAsync(cancellationToken);
        var next = await InsertAsync(conn, tx, family, accountId, kind, authTime, amr, familyExpires, now, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return next;
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand("""
            UPDATE refresh_token SET revoked_at = $2
            WHERE family_id = (SELECT family_id FROM refresh_token WHERE token_hash = $1) AND revoked_at IS NULL
            """, conn) { Parameters = { new() { Value = Hash(token) }, new() { Value = time.GetUtcNow() } } };
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    static async Task RevokeFamilyAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid family, DateTimeOffset now, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("UPDATE refresh_token SET revoked_at = $2 WHERE family_id = $1 AND revoked_at IS NULL", conn, tx)
            { Parameters = { new() { Value = family }, new() { Value = now } } };
        await cmd.ExecuteNonQueryAsync(ct);
    }

    static async Task<RefreshSession> InsertAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, Guid family, Guid accountId, AccountKind kind,
        DateTimeOffset authTime, IReadOnlyList<string> amr, DateTimeOffset? familyExpires, DateTimeOffset now, CancellationToken ct)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expires = now + SessionPolicy.IdleTimeout(kind);
        if (familyExpires < expires)
            expires = familyExpires.Value;
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO refresh_token (token_hash, family_id, account_id, auth_time, amr, issued_at, expires_at, family_expires_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
            """, conn, tx)
        {
            Parameters =
            {
                new() { Value = Hash(token) }, new() { Value = family }, new() { Value = accountId }, new() { Value = authTime },
                new() { Value = amr.ToArray() }, new() { Value = now }, new() { Value = expires }, new() { Value = (object?)familyExpires ?? DBNull.Value },
            },
        };
        await cmd.ExecuteNonQueryAsync(ct);
        return new RefreshSession(token, expires, accountId, authTime, [.. amr], kind);
    }

    static byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
}
