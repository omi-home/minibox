using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Aggregates.Accounts;

namespace Minibox.Identity.Domain.Aggregates.Roles;

/// <summary>Vai trò (bảng <c>role</c>) và mã quyền của nó (<c>role_permission</c>); dữ liệu danh mục do migration/seed quản, ứng dụng chỉ đọc.</summary>
public sealed class Role : AggregateRoot<Guid>
{
    readonly List<RolePermission> _permissions = [];

    Role(Guid id, string code, AccountKind kind) : base(id)
    {
        Code = code;
        Kind = kind;
    }

    /// <summary>Mã vai trò (<c>warehouse</c>, <c>qc</c>…).</summary>
    public string Code { get; private set; }

    /// <summary>Loại tài khoản được gán vai trò này.</summary>
    public AccountKind Kind { get; private set; }

    /// <summary>Phiên bản (optimistic concurrency).</summary>
    public long Version { get; private set; }

    /// <summary>Mã quyền của vai trò.</summary>
    public IReadOnlyList<RolePermission> Permissions => _permissions;
}

/// <summary>Một mã quyền của vai trò.</summary>
public sealed class RolePermission
{
    RolePermission() { }

    /// <summary>Mã quyền <c>miền.tài-nguyên.hành-động</c>.</summary>
    public string Code { get; private set; } = "";
}
