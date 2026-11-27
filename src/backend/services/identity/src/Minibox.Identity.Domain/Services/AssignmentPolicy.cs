using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Domain.Services;

/// <summary>Luật đổi phân công vai trò (spec §3.6): chống leo thang đặc quyền và dạng phạm vi.</summary>
public static partial class AssignmentPolicy
{
    /// <summary>Mã quyền quản trị phân công.</summary>
    public const string ManagePermission = "identity.assignment.manage";

    /// <summary>Quyền người gọi phải có trong phạm vi để gán/thu hồi một vai trò.</summary>
    /// <param name="rolePermissions">Mã quyền của vai trò (rỗng nếu vai trò không tồn tại).</param>
    /// <returns><see cref="ManagePermission"/> cùng mọi quyền của vai trò, không trùng.</returns>
    public static IReadOnlyList<string> RequiredPermissions(IEnumerable<string> rolePermissions) =>
        [.. rolePermissions.Append(ManagePermission).Distinct(StringComparer.Ordinal)];

    /// <summary>Quyết định người gọi có được đổi phân công không: không đổi của chính mình, và phải có đủ mọi quyền yêu cầu trong phạm vi.</summary>
    /// <param name="actor">Người gọi.</param>
    /// <param name="target">Tài khoản bị đổi phân công.</param>
    /// <param name="required">Từ <see cref="RequiredPermissions"/>.</param>
    /// <param name="held">Các quyền trong <paramref name="required"/> mà người gọi có ở phạm vi đó.</param>
    /// <returns>Thành công; lỗi <see cref="IdentityErrors.AssignmentForbidden"/>.</returns>
    public static Result CanChange(Guid actor, Guid target, IEnumerable<string> required, IReadOnlySet<string> held) =>
        actor == target || required.Any(p => !held.Contains(p)) ? IdentityErrors.AssignmentForbidden : Result.Success();

    /// <summary>Kiểm dạng phạm vi: <c>*</c>, <c>wh:&lt;id&gt;</c> hoặc <c>farm:&lt;id&gt;</c> (id nguyên dương).</summary>
    /// <param name="scope">Phạm vi.</param>
    /// <returns><c>true</c> nếu hợp lệ.</returns>
    public static bool IsValidScope(string? scope) => scope is not null && ScopePattern().IsMatch(scope);

    [GeneratedRegex(@"\A(\*|(wh|farm):[1-9][0-9]{0,18})\z")]
    private static partial Regex ScopePattern();
}
