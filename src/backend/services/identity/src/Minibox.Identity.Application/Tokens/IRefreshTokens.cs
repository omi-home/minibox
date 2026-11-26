using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Application.Tokens;

/// <summary>Refresh token vừa cấp và phiên mà nó thuộc về.</summary>
/// <param name="Token">Giá trị trả cho BFF (chỉ lộ một lần, server chỉ giữ bản băm).</param>
/// <param name="ExpiresAt">Hạn của token này (đã tính giới hạn không hoạt động và tối đa của phiên).</param>
/// <param name="AccountId">Tài khoản.</param>
/// <param name="AuthTime">Thời điểm xác thực gốc của phiên.</param>
/// <param name="AuthMethods">Phương thức xác thực gốc của phiên.</param>
public sealed record RefreshSession(string Token, DateTimeOffset ExpiresAt, Guid AccountId, DateTimeOffset AuthTime, IReadOnlyList<string> AuthMethods);

/// <summary>Refresh token xoay vòng, phát hiện dùng lại, đăng xuất (spec §3.2).</summary>
public interface IRefreshTokens
{
    /// <summary>Mã lỗi chung cho token không hợp lệ, hết hạn, đã thu hồi hoặc bị dùng lại (không phân biệt để không lộ trạng thái).</summary>
    const string InvalidCode = "Identity.InvalidRefreshToken";

    /// <summary>Mở phiên mới sau khi đăng nhập thành công.</summary>
    /// <param name="accountId">Tài khoản.</param>
    /// <param name="kind">Loại tài khoản, quyết định thời hạn phiên (<see cref="SessionPolicy"/>).</param>
    /// <param name="authMethods">Phương thức đã xác thực (<c>pwd</c>, <c>mfa</c>…).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Refresh token đầu tiên của phiên.</returns>
    Task<RefreshSession> IssueAsync(Guid accountId, AccountKind kind, IReadOnlyList<string> authMethods, CancellationToken cancellationToken = default);

    /// <summary>Đổi refresh token lấy token mới cùng phiên; token cũ hết hiệu lực.</summary>
    /// <param name="token">Refresh token BFF gửi lên.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Token mới; lỗi <see cref="InvalidCode"/> nếu không hợp lệ. Token đã xoay bị đưa lại → thu hồi cả phiên rồi trả lỗi.</returns>
    Task<Result<RefreshSession>> RotateAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Đăng xuất: thu hồi cả phiên chứa token.</summary>
    /// <param name="token">Refresh token hiện tại.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Hoàn tất; token không tồn tại thì không làm gì.</returns>
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}
