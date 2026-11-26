using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Application.Accounts;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Phân công trên <c>role_assignment</c>; audit vào <c>audit_log</c> (append-only) cùng transaction.</summary>
/// <param name="dbs">Factory DbContext identity (role <c>identity_app</c>).</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Khoá dòng <c>account</c> (<c>FOR UPDATE</c>) để ảnh chụp trước/sau của hai thay đổi song song không chồng nhau.</remarks>
public sealed partial class PostgresRoleAssignments(IDbContextFactory<IdentityDbContext> dbs, TimeProvider time) : IRoleAssignments
{
    static readonly Error NotFound = Error.NotFound(IRoleAssignments.NotFoundCode, "Không tìm thấy tài khoản, vai trò hoặc phân công.");

    [GeneratedRegex(@"\A(\*|(wh|farm):[1-9][0-9]{0,18})\z")]
    private static partial Regex ScopePattern();

    sealed record Item(string Role, string Scope);

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<string>?> PermissionsOfRoleAsync(string roleCode, CancellationToken cancellationToken = default)
    {
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        return await db.Set<RoleRow>().Where(r => r.Code == roleCode)
            .Select(r => db.Set<RolePermissionRow>().Where(rp => rp.RoleId == r.Id).Select(rp => rp.PermissionCode).ToArray())
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> GrantAsync(Guid actor, Guid accountId, string roleCode, string scope, CancellationToken cancellationToken = default)
    {
        if (!ScopePattern().IsMatch(scope))
            return Error.Validation(IRoleAssignments.InvalidCode, "Phạm vi phải là *, wh:<id> hoặc farm:<id>.");
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var account = await LockAccountAsync(db, accountId, cancellationToken);
        var role = await db.Set<RoleRow>().Where(r => r.Code == roleCode).Select(r => new { r.Id, r.Kind }).FirstOrDefaultAsync(cancellationToken);
        if (account is null || role is null)
            return NotFound;
        if (role.Kind != AccountRepository.KindText(account.Kind))
            return Error.Validation(IRoleAssignments.InvalidCode, "Vai trò không áp dụng cho loại tài khoản này.");

        var before = await SnapshotAsync(db, accountId, cancellationToken);
        if (await db.Database.ExecuteSqlAsync($"""
                INSERT INTO role_assignment (account_id, role_id, scope, granted_by) VALUES ({accountId}, {role.Id}, {scope}, {actor}) ON CONFLICT DO NOTHING
                """, cancellationToken) == 0)
            return Result.Success(); // đã có: không đổi quyền, không audit

        await AuditAsync(db, actor, "RoleAssignmentGranted", accountId, roleCode, scope, before, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RevokeAsync(Guid actor, Guid accountId, string roleCode, string scope, CancellationToken cancellationToken = default)
    {
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await LockAccountAsync(db, accountId, cancellationToken) is null)
            return NotFound;

        var before = await SnapshotAsync(db, accountId, cancellationToken);
        var roles = db.Set<RoleRow>();
        if (await db.Set<RoleAssignmentRow>()
                .Where(ra => ra.AccountId == accountId && ra.Scope == scope && roles.Any(r => r.Id == ra.RoleId && r.Code == roleCode))
                .ExecuteDeleteAsync(cancellationToken) == 0)
            return NotFound;

        await AuditAsync(db, actor, "RoleAssignmentRevoked", accountId, roleCode, scope, before, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Result.Success();
    }

    static async Task<Account?> LockAccountAsync(IdentityDbContext db, Guid accountId, CancellationToken ct) =>
        (await db.Set<Account>().FromSql($"SELECT * FROM account WHERE id = {accountId} FOR UPDATE").AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    async Task AuditAsync(IdentityDbContext db, Guid actor, string action, Guid accountId, string role, string scope, List<Item> before, CancellationToken ct)
    {
        var after = await SnapshotAsync(db, accountId, ct);
        var now = time.GetUtcNow();
        var (id, actorText, aggregateId) = (Guid.CreateVersion7(now), actor.ToString(), accountId.ToString());
        var payload = JsonSerializer.Serialize(new { role, scope, before, after }, JsonSerializerOptions.Web);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO audit_log (id, occurred_at, actor, action, aggregate_type, aggregate_id, payload)
            VALUES ({id}, {now}, {actorText}, {action}, 'Account', {aggregateId}, {payload}::jsonb)
            """, ct);
    }

    static async Task<List<Item>> SnapshotAsync(IdentityDbContext db, Guid accountId, CancellationToken ct)
    {
        var items = await (
            from ra in db.Set<RoleAssignmentRow>()
            join r in db.Set<RoleRow>() on ra.RoleId equals r.Id
            where ra.AccountId == accountId
            select new Item(r.Code, ra.Scope)).ToListAsync(ct);
        // Thứ tự ordinal (như COLLATE "C") để ảnh chụp trước/sau ổn định.
        return [.. items.OrderBy(i => i.Role, StringComparer.Ordinal).ThenBy(i => i.Scope, StringComparer.Ordinal)];
    }
}
