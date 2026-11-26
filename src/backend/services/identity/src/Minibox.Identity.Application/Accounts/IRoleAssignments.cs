using Minibox.BuildingBlocks.Domain;

namespace Minibox.Identity.Application.Accounts;

/// <summary>Gán/thu hồi phân công vai trò cho tài khoản, kèm audit trước/sau (spec §3.6).</summary>
/// <remarks>Kiểm quyền người gọi (<c>identity.assignment.manage</c> trong phạm vi) và step-up làm ở endpoint trước khi gọi.
/// Mỗi thay đổi ghi một dòng <c>audit_log</c> cùng transaction; trigger DB thu hồi refresh token của người bị đổi quyền.</remarks>
public interface IRoleAssignments
{
    /// <summary>Mã lỗi khi tài khoản hoặc vai trò không tồn tại, hoặc phân công cần thu hồi không có.</summary>
    const string NotFoundCode = "Identity.AssignmentNotFound";

    /// <summary>Mã lỗi khi phạm vi sai dạng hoặc vai trò khách gán cho nhân viên (và ngược lại).</summary>
    const string InvalidCode = "Identity.AssignmentInvalid";

    /// <summary>Đọc mã quyền của vai trò, để endpoint đòi người gọi có đủ các quyền đó trong phạm vi (chống leo thang đặc quyền).</summary>
    /// <param name="roleCode">Mã vai trò.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Mã quyền (rỗng với vai trò khách); <c>null</c> nếu vai trò không tồn tại.</returns>
    Task<IReadOnlyCollection<string>?> PermissionsOfRoleAsync(string roleCode, CancellationToken cancellationToken = default);

    /// <summary>Gán vai trò trong phạm vi cho tài khoản.</summary>
    /// <param name="actor">Tài khoản người thực hiện (claim <c>sub</c>).</param>
    /// <param name="accountId">Tài khoản được gán.</param>
    /// <param name="roleCode">Mã vai trò.</param>
    /// <param name="scope">Phạm vi <c>*</c>, <c>wh:n</c> hoặc <c>farm:n</c>.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Thành công (kể cả khi đã có phân công — không ghi gì thêm); lỗi <see cref="NotFoundCode"/> hoặc <see cref="InvalidCode"/>.</returns>
    Task<Result> GrantAsync(Guid actor, Guid accountId, string roleCode, string scope, CancellationToken cancellationToken = default);

    /// <summary>Thu hồi một phân công.</summary>
    /// <param name="actor">Tài khoản người thực hiện (claim <c>sub</c>).</param>
    /// <param name="accountId">Tài khoản bị thu hồi.</param>
    /// <param name="roleCode">Mã vai trò.</param>
    /// <param name="scope">Phạm vi của phân công.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Thành công; lỗi <see cref="NotFoundCode"/> khi không có phân công đó.</returns>
    Task<Result> RevokeAsync(Guid actor, Guid accountId, string roleCode, string scope, CancellationToken cancellationToken = default);
}
