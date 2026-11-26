using Microsoft.EntityFrameworkCore;
using Minibox.Identity.Application.Accounts;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Đọc quyền hiệu lực từ <c>role_assignment</c> ⋈ <c>role_permission</c>.</summary>
/// <param name="dbs">Factory DbContext identity (role <c>identity_app</c>).</param>
public sealed class PostgresPermissions(IDbContextFactory<IdentityDbContext> dbs) : IPermissions
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ForAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbs.CreateDbContextAsync(cancellationToken);
        var rows = await (
            from ra in db.Set<RoleAssignmentRow>()
            join rp in db.Set<RolePermissionRow>() on ra.RoleId equals rp.RoleId
            where ra.AccountId == accountId
            select new { ra.Scope, rp.PermissionCode }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.Scope).ToDictionary(
            g => g.Key,
            g => (IReadOnlyCollection<string>)g.Select(r => r.PermissionCode).Distinct().Order(StringComparer.Ordinal).ToArray());
    }
}
