namespace Minibox.Identity.Domain.Aggregates.Accounts;

/// <summary>Secret TOTP của tài khoản (bảng <c>mfa_totp</c>), chỉ thay đổi qua <see cref="Account"/>.</summary>
public sealed class MfaTotp
{
    MfaTotp() { }

    internal MfaTotp(byte[] secretEnc) => SecretEnc = secretEnc;

    /// <summary>Secret đã mã hoá (AES-GCM, AAD là id tài khoản).</summary>
    public byte[] SecretEnc { get; private set; } = [];

    /// <summary>Lúc xác nhận; <c>null</c> khi còn chờ.</summary>
    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Bước TOTP đã dùng gần nhất; mọi bước ≤ giá trị này bị từ chối (chống phát lại).</summary>
    public long? LastUsedStep { get; private set; }

    internal void Restart(byte[] secretEnc)
    {
        SecretEnc = secretEnc;
        LastUsedStep = null;
    }

    internal void Confirm(DateTimeOffset now, long step)
    {
        ConfirmedAt = now;
        LastUsedStep = step;
    }

    internal void Use(long step) => LastUsedStep = step;
}

/// <summary>Mã khôi phục MFA (bảng <c>mfa_recovery_code</c>): chỉ lưu SHA-256, mỗi mã dùng một lần.</summary>
public sealed class RecoveryCode
{
    RecoveryCode() { }

    internal RecoveryCode(byte[] codeHash) => CodeHash = codeHash;

    /// <summary>SHA-256 của mã đã chuẩn hoá.</summary>
    public byte[] CodeHash { get; private set; } = [];

    /// <summary>Lúc đã dùng; <c>null</c> khi còn dùng được.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    internal void Use(DateTimeOffset now) => UsedAt = now;
}

/// <summary>Phân công một vai trò trong một phạm vi (bảng <c>role_assignment</c>).</summary>
public sealed class RoleAssignment
{
    RoleAssignment() { }

    internal RoleAssignment(Guid roleId, string scope, Guid grantedBy)
    {
        RoleId = roleId;
        Scope = scope;
        GrantedBy = grantedBy;
    }

    /// <summary>Vai trò.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>Phạm vi <c>*</c>, <c>wh:n</c> hoặc <c>farm:n</c>.</summary>
    public string Scope { get; private set; } = "";

    /// <summary>Người gán; <c>null</c> với phân công do seed/migration tạo.</summary>
    public Guid? GrantedBy { get; private set; }
}
