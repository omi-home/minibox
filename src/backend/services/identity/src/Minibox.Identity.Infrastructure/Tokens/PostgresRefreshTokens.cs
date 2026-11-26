using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Infrastructure.Tokens;

/// <summary>Refresh token trên bảng <c>identity.refresh_token</c>: token ngẫu nhiên 256 bit, chỉ lưu SHA-256.</summary>
/// <param name="dbs">Factory DbContext identity (role <c>identity_app</c>).</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Xoay vòng khoá dòng bằng <c>FOR UPDATE</c>: hai request song song cùng token thì request sau thấy token đã dùng
/// và thu hồi cả phiên (đúng spec, không có thời gian ân hạn).</remarks>
public sealed class PostgresRefreshTokens(IDbContextFactory<IdentityDbContext> dbs, TimeProvider time) : IRefreshTokens
{
    static readonly Error Invalid = Error.Failure(IRefreshTokens.InvalidCode, "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <inheritdoc />
    public async Task<RefreshSession> IssueAsync(Guid accountId, AccountKind kind, IReadOnlyList<string> authMethods, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var familyExpires = SessionPolicy.MaxLifetime(kind) is { } max ? now + max : (DateTimeOffset?)null;
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        var session = Add(db, Guid.CreateVersion7(), accountId, kind, now, authMethods, familyExpires, now);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    /// <inheritdoc />
    public async Task<Result<RefreshSession>> RotateAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var hash = Hash(token);
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = (await db.Set<RefreshTokenRow>()
            .FromSql($"SELECT * FROM refresh_token WHERE token_hash = {hash} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault();
        if (current is null || current.RevokedAt is not null || current.ExpiresAt <= now)
            return Invalid;
        if (current.UsedAt is not null)
        {
            await RevokeFamilyAsync(db, current.FamilyId, now, cancellationToken); // token đã xoay bị đưa lại: coi như bị đánh cắp
            await tx.CommitAsync(cancellationToken);
            return Invalid;
        }

        var kind = await db.Set<Account>().Where(a => a.Id == current.AccountId).Select(a => a.Kind).SingleAsync(cancellationToken);
        current.UsedAt = now;
        var next = Add(db, current.FamilyId, current.AccountId, kind, current.AuthTime, current.Amr, current.FamilyExpiresAt, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return next;
    }

    /// <inheritdoc />
    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var now = time.GetUtcNow();
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        var tokens = db.Set<RefreshTokenRow>();
        await tokens.Where(t => t.RevokedAt == null && tokens.Any(x => x.TokenHash == hash && x.FamilyId == t.FamilyId))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    static Task RevokeFamilyAsync(IdentityDbContext db, Guid family, DateTimeOffset now, CancellationToken ct) =>
        db.Set<RefreshTokenRow>().Where(t => t.FamilyId == family && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    static RefreshSession Add(IdentityDbContext db, Guid family, Guid accountId, AccountKind kind,
        DateTimeOffset authTime, IReadOnlyList<string> amr, DateTimeOffset? familyExpires, DateTimeOffset now)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var expires = now + SessionPolicy.IdleTimeout(kind);
        if (familyExpires < expires)
            expires = familyExpires.Value;
        db.Add(new RefreshTokenRow
        {
            TokenHash = Hash(token), FamilyId = family, AccountId = accountId, AuthTime = authTime, Amr = [.. amr],
            IssuedAt = now, ExpiresAt = expires, FamilyExpiresAt = familyExpires,
        });
        return new RefreshSession(token, expires, accountId, authTime, [.. amr], kind);
    }

    static byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
}
