using Minibox.Identity.Application.Features.Accounts.Queries.GetMyPreferences;

namespace Minibox.Identity.Application.Abstractions;

/// <summary>Đọc quyền hiệu lực của tài khoản cho claim <c>perms</c> của access token (spec §3.6).</summary>
public interface IPermissionReadService
{
    /// <summary>Gom hợp các mã quyền từ mọi phân công của tài khoản theo phạm vi.</summary>
    /// <param name="accountId">Tài khoản.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Phạm vi (<c>*</c>, <c>wh:n</c>, <c>farm:n</c>) → mã quyền không trùng, sắp xếp ordinal; rỗng nếu không có phân công.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> ForAccountAsync(Guid accountId, CancellationToken cancellationToken = default);
}

/// <summary>Mã hoá secret TOTP trước khi lưu (spec §3.5).</summary>
public interface IMfaSecretProtector
{
    /// <summary>Mã hoá secret, gắn với tài khoản (chép ciphertext sang tài khoản khác thì không giải mã được).</summary>
    /// <param name="accountId">Tài khoản sở hữu.</param>
    /// <param name="secret">Secret thô.</param>
    /// <returns>Ciphertext để lưu.</returns>
    byte[] Protect(Guid accountId, byte[] secret);

    /// <summary>Giải mã secret đã lưu.</summary>
    /// <param name="accountId">Tài khoản sở hữu.</param>
    /// <param name="encrypted">Ciphertext từ <see cref="Protect"/>.</param>
    /// <returns>Secret thô.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">Ciphertext bị sửa hoặc thuộc tài khoản khác (lỗi dữ liệu, không phải sai mã).</exception>
    byte[] Unprotect(Guid accountId, byte[] encrypted);
}

/// <summary>Khoá ký access token đang công bố (spec §3.2).</summary>
public interface ISigningKeys
{
    /// <summary>Dựng tài liệu JWKS (RFC 7517) chỉ gồm phần công khai của các khoá đang công bố.</summary>
    /// <returns>Đối tượng <c>{"keys":[…]}</c> để serialize JSON.</returns>
    object Jwks();
}

/// <summary>Port đọc tùy chọn hiển thị của tài khoản (phía query).</summary>
public interface IAccountPreferencesReadService
{
    /// <summary>Đọc locale và múi giờ của tài khoản.</summary>
    /// <param name="accountId">Tài khoản.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Tùy chọn; <c>null</c> nếu tài khoản không tồn tại.</returns>
    Task<PreferencesDto?> GetAsync(Guid accountId, CancellationToken cancellationToken = default);
}
