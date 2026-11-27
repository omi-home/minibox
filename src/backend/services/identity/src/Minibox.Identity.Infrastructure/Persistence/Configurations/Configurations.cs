using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;
using Minibox.Identity.Domain.Aggregates.Roles;

namespace Minibox.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="Account"/> ↔ <c>account</c>, MFA ↔ <c>mfa_totp</c>, mã khôi phục ↔ <c>mfa_recovery_code</c>,
/// phân công ↔ <c>role_assignment</c> (owned: nạp cùng tài khoản, ghi theo thay đổi của aggregate).</summary>
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.IsAggregateRoot();
        builder.Property(x => x.Kind).HasConversion(k => k.ToCode(), s => AccountKindCodes.Parse(s));
        builder.OwnsOne(x => x.Mfa, m =>
        {
            m.ToTable("mfa_totp");
            m.WithOwner().HasForeignKey("AccountId");
            m.HasKey("AccountId");
            m.Property<Guid>("AccountId").HasColumnName("account_id");
            m.Property(x => x.SecretEnc).HasColumnName("secret_enc");
            m.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
            m.Property(x => x.LastUsedStep).HasColumnName("last_used_step");
        });
        builder.OwnsMany(x => x.RecoveryCodes, c =>
        {
            c.ToTable("mfa_recovery_code");
            c.WithOwner().HasForeignKey("AccountId");
            c.Property<Guid>("AccountId").HasColumnName("account_id");
            c.Property(x => x.CodeHash).HasColumnName("code_hash");
            c.Property(x => x.UsedAt).HasColumnName("used_at");
            c.HasKey("AccountId", nameof(RecoveryCode.CodeHash));
        });
        builder.Navigation(x => x.RecoveryCodes).HasField("_recoveryCodes");
        builder.OwnsMany(x => x.Assignments, a =>
        {
            a.ToTable("role_assignment");
            a.WithOwner().HasForeignKey("AccountId");
            a.Property<Guid>("AccountId").HasColumnName("account_id");
            a.Property(x => x.RoleId).HasColumnName("role_id");
            a.Property(x => x.Scope).HasColumnName("scope");
            a.Property(x => x.GrantedBy).HasColumnName("granted_by");
            a.HasKey("AccountId", nameof(RoleAssignment.RoleId), nameof(RoleAssignment.Scope));
        });
        builder.Navigation(x => x.Assignments).HasField("_assignments");
    }
}

/// <summary>Mapping <see cref="Role"/> ↔ <c>role</c>, mã quyền ↔ <c>role_permission</c> (chỉ đọc).</summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.IsAggregateRoot();
        builder.Property(x => x.Kind).HasConversion(k => k.ToCode(), s => AccountKindCodes.Parse(s));
        builder.OwnsMany(x => x.Permissions, p =>
        {
            p.ToTable("role_permission");
            p.WithOwner().HasForeignKey("RoleId");
            p.Property<Guid>("RoleId").HasColumnName("role_id");
            p.Property(x => x.Code).HasColumnName("permission_code");
            p.HasKey("RoleId", nameof(RolePermission.Code));
        });
        builder.Navigation(x => x.Permissions).HasField("_permissions");
    }
}

/// <summary>Mapping <see cref="LoginThrottle"/> ↔ <c>login_throttle</c> (khoá chính là cột <c>key</c>).</summary>
public sealed class LoginThrottleConfiguration : IEntityTypeConfiguration<LoginThrottle>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LoginThrottle> builder)
    {
        builder.IsAggregateRoot();
        builder.Property(x => x.Id).HasColumnName("key");
    }
}

/// <summary>Mapping <see cref="RefreshToken"/> ↔ <c>refresh_token</c> (khoá chính là cột <c>token_hash</c>).</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.IsAggregateRoot();
        builder.Property(x => x.Id).HasColumnName("token_hash");
    }
}
