using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Application.Features.Accounts.Queries.GetMyPreferences;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;
using Minibox.Identity.Domain.Aggregates.Roles;

namespace Minibox.Identity.Infrastructure.Persistence;

/// <summary>Repository <see cref="Account"/> (EF, nạp kèm MFA/mã khôi phục/phân công).</summary>
/// <param name="db">DbContext của command.</param>
public sealed class AccountRepository(IdentityDbContext db) : EfRepository<Account, Guid>(db), IAccountRepository
{
    /// <inheritdoc/>
    public Task<Account?> FindByLoginAsync(string login, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(a => a.Email == login || a.Username == login, cancellationToken);
}

/// <summary>Repository <see cref="LoginThrottle"/>; khoá tuần tự bằng <c>pg_advisory_xact_lock</c> (khoá cả khi chưa có dòng).</summary>
/// <param name="db">DbContext của command.</param>
public sealed class LoginThrottleRepository(IdentityDbContext db) : EfRepository<LoginThrottle, string>(db), ILoginThrottleRepository
{
    /// <inheritdoc/>
    public async Task<LoginThrottle?> LockAsync(string key, CancellationToken cancellationToken = default)
    {
        // Khoá advisory theo transaction: FOR UPDATE không khoá được dòng chưa tồn tại (lần sai đầu tiên song song).
        await Db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
        return await GetAsync(key, cancellationToken);
    }
}

/// <summary>Repository <see cref="RefreshToken"/>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class RefreshTokenRepository(IdentityDbContext db) : EfRepository<RefreshToken, byte[]>(db), IRefreshTokenRepository
{
    /// <inheritdoc/>
    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Set.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
}

/// <summary>Đọc danh mục vai trò (không theo dõi thay đổi).</summary>
/// <param name="db">DbContext của request.</param>
public sealed class RoleRepository(IdentityDbContext db) : IRoleRepository
{
    /// <inheritdoc/>
    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.Set<Role>().AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, string>> CodesAsync(CancellationToken cancellationToken = default) =>
        await db.Set<Role>().AsNoTracking().Select(r => new { r.Id, r.Code }).ToDictionaryAsync(r => r.Id, r => r.Code, cancellationToken);
}

/// <summary>Đọc quyền hiệu lực: phân công của tài khoản ⋈ mã quyền của vai trò (LINQ, không tải entity).</summary>
/// <param name="db">DbContext của request.</param>
public sealed class PermissionReadService(IdentityDbContext db) : IPermissionReadService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ForAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var rows = await (
            from account in db.Set<Account>().AsNoTracking()
            where account.Id == accountId
            from assignment in account.Assignments
            from role in db.Set<Role>()
            where role.Id == assignment.RoleId
            from permission in role.Permissions
            select new { assignment.Scope, permission.Code }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.Scope).ToDictionary(
            g => g.Key,
            g => (IReadOnlyCollection<string>)g.Select(r => r.Code).Distinct().Order(StringComparer.Ordinal).ToArray());
    }
}

/// <summary>Đọc tùy chọn hiển thị bằng LINQ <c>AsNoTracking</c> + <c>Select</c> ra DTO.</summary>
/// <param name="db">DbContext của request.</param>
public sealed class AccountPreferencesReadService(IdentityDbContext db) : IAccountPreferencesReadService
{
    /// <inheritdoc/>
    public Task<PreferencesDto?> GetAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        db.Set<Account>().AsNoTracking().Where(a => a.Id == accountId)
            .Select(a => new PreferencesDto(a.Locale, a.TimeZone)).FirstOrDefaultAsync(cancellationToken);
}
