using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Application.Accounts;

/// <summary>Đăng nhập thành công: tài khoản và phiên refresh token vừa mở.</summary>
/// <param name="AccountId">Tài khoản.</param>
/// <param name="Kind">Loại tài khoản.</param>
/// <param name="Session">Phiên refresh token (amr <c>pwd</c>).</param>
public sealed record LoginSuccess(Guid AccountId, AccountKind Kind, RefreshSession Session);

/// <summary>Đăng nhập bằng email/username + mật khẩu, có giới hạn đăng nhập sai (<see cref="LoginThrottle"/>).</summary>
public interface IPasswordLogin
{
    /// <summary>Mã lỗi chung cho sai mật khẩu và tài khoản không tồn tại.</summary>
    const string InvalidCredentialsCode = "Identity.InvalidCredentials";

    /// <summary>Mã lỗi khi định danh hoặc IP đang bị khóa tạm.</summary>
    const string ThrottledCode = "Identity.LoginThrottled";

    /// <summary>Kiểm mật khẩu và mở phiên.</summary>
    /// <param name="identifier">Email hoặc username (không phân biệt hoa thường, bỏ khoảng trắng hai đầu).</param>
    /// <param name="password">Mật khẩu.</param>
    /// <param name="ip">IP của người dùng cuối (BFF chuyển tiếp).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Phiên mới; lỗi <see cref="InvalidCredentialsCode"/> (cùng thông báo dù tài khoản có tồn tại hay không) hoặc
    /// <see cref="ThrottledCode"/> khi đang bị khóa tạm.</returns>
    Task<Result<LoginSuccess>> LoginAsync(string? identifier, string? password, string ip, CancellationToken cancellationToken = default);
}
