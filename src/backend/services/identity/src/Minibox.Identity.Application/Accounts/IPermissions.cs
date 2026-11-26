namespace Minibox.Identity.Application.Accounts;

/// <summary>Quyền hiệu lực của tài khoản để đưa vào claim <c>perms</c> của access token (spec §3.6).</summary>
public interface IPermissions
{
    /// <summary>Gom hợp các mã quyền từ mọi phân công của tài khoản theo phạm vi.</summary>
    /// <param name="accountId">Tài khoản.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Phạm vi (<c>*</c>, <c>wh:n</c>, <c>farm:n</c>) → mã quyền không trùng, sắp xếp ordinal; rỗng nếu không có phân công.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
}
