using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure.Accounts;
using Npgsql;
using Wolverine.EntityFrameworkCore;

namespace Minibox.Identity.Infrastructure;

/// <summary>DbContext của service identity (schema <c>identity</c>); mapping khớp DDL ở <c>database/schemas/identity/</c>.</summary>
/// <param name="options">Tuỳ chọn EF Core (Npgsql, role app).</param>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    /// <summary>Schema của service.</summary>
    public const string Schema = "identity";

    /// <summary>Tạo factory DbContext (có pool) trên nguồn kết nối role <c>identity_app</c>, cho các store singleton.</summary>
    /// <param name="dataSource">Nguồn kết nối role app.</param>
    /// <returns>Factory; mỗi thao tác của store tạo và huỷ một DbContext.</returns>
    public static IDbContextFactory<IdentityDbContext> Factory(NpgsqlDataSource dataSource) =>
        new PooledDbContextFactory<IdentityDbContext>(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(dataSource).Options);

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Account>(e =>
        {
            e.IsAggregateRoot();
            e.Property(x => x.Kind).HasConversion(k => AccountRepository.KindText(k), s => AccountRepository.ParseKind(s));
            e.Property(x => x.Email);
            e.Property(x => x.Username);
            e.Property(x => x.PasswordHash);
        });
        modelBuilder.Entity<RefreshTokenRow>().ToTable("refresh_token").HasKey(x => x.TokenHash);
        modelBuilder.Entity<LoginThrottleRow>().ToTable("login_throttle").HasKey(x => x.Key);
        modelBuilder.Entity<MfaTotpRow>().ToTable("mfa_totp").HasKey(x => x.AccountId);
        modelBuilder.Entity<MfaRecoveryCodeRow>().ToTable("mfa_recovery_code").HasKey(x => new { x.AccountId, x.CodeHash });
        modelBuilder.Entity<RoleRow>().ToTable("role");
        modelBuilder.Entity<RoleAssignmentRow>().ToTable("role_assignment").HasKey(x => new { x.AccountId, x.RoleId, x.Scope });
        modelBuilder.Entity<RolePermissionRow>().ToTable("role_permission").HasKey(x => new { x.RoleId, x.PermissionCode });
        modelBuilder.MapWolverineEnvelopeStorage(Schema);
        modelBuilder.UseSnakeCaseNames();
    }
}

// Dòng dữ liệu không phải aggregate (token, bộ đếm, MFA, phân quyền): chỉ dùng trong Infrastructure, khớp cột của bảng cùng tên.
sealed class RefreshTokenRow
{
    public byte[] TokenHash { get; set; } = [];
    public Guid FamilyId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset AuthTime { get; set; }
    public string[] Amr { get; set; } = [];
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? FamilyExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

sealed class LoginThrottleRow
{
    public string Key { get; set; } = "";
    public int Failures { get; set; }
    public DateTimeOffset LastFailureAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

sealed class MfaTotpRow
{
    public Guid AccountId { get; set; }
    public byte[] SecretEnc { get; set; } = [];
    public DateTimeOffset? ConfirmedAt { get; set; }
    public long? LastUsedStep { get; set; }
}

sealed class MfaRecoveryCodeRow
{
    public Guid AccountId { get; set; }
    public byte[] CodeHash { get; set; } = [];
    public DateTimeOffset? UsedAt { get; set; }
}

sealed class RoleRow
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Kind { get; set; } = "";
}

sealed class RoleAssignmentRow
{
    public Guid AccountId { get; set; }
    public Guid RoleId { get; set; }
    public string Scope { get; set; } = "";
    public Guid? GrantedBy { get; set; }
}

sealed class RolePermissionRow
{
    public Guid RoleId { get; set; }
    public string PermissionCode { get; set; } = "";
}
