using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Aggregates.Roles;
using Minibox.Identity.Domain.Services;

namespace Minibox.Identity.Application.Features.Assignments;

/// <summary>Kiểm người gọi được đổi phân công (spec §3.6): gom quyền người gọi có trong phạm vi rồi để <see cref="AssignmentPolicy"/> quyết định.</summary>
public static class AssignmentAccess
{
    /// <summary>Mã quyền vào endpoint quản trị phân công (tầng 1, ở bất kỳ phạm vi).</summary>
    public const string ManagePermission = AssignmentPolicy.ManagePermission;

    /// <summary>Độ dài tối đa của mã vai trò và phạm vi nhận từ request.</summary>
    public const int MaxCodeLength = 64;

    /// <summary>Đòi người gọi có <see cref="ManagePermission"/> và mọi quyền của vai trò trong phạm vi, và không đổi phân công của chính mình.</summary>
    /// <param name="user">Người gọi.</param>
    /// <param name="authorizer">Bộ kiểm quyền theo claim <c>perms</c>.</param>
    /// <param name="target">Tài khoản bị đổi phân công.</param>
    /// <param name="role">Vai trò, hoặc <c>null</c> nếu không tồn tại (khi đó chỉ đòi quyền quản trị, để thay đổi trả 404).</param>
    /// <param name="scope">Phạm vi của phân công.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Id người gọi; lỗi Forbidden nếu không được phép hoặc <c>sub</c> không phải id tài khoản.</returns>
    public static async Task<Result<Guid>> EnsureAsync(ICurrentUser user, IAuthorizer authorizer, Guid target, Role? role, string scope,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.UserId, out var actor))
            return Domain.Errors.IdentityErrors.AssignmentForbidden;
        var required = AssignmentPolicy.RequiredPermissions(role?.Permissions.Select(p => p.Code) ?? []);
        var held = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in required)
            if (await authorizer.HasPermissionAsync(permission, scope, cancellationToken))
                held.Add(permission);
        var allowed = AssignmentPolicy.CanChange(actor, target, required, held);
        return allowed.IsSuccess ? actor : allowed.Error;
    }
}
