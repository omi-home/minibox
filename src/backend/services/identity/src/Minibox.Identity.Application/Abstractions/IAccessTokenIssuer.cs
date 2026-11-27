namespace Minibox.Identity.Application.Abstractions;

/// <summary>Thông tin đưa vào access token.</summary>
/// <param name="Subject">Định danh tài khoản hoặc service (<c>sub</c>).</param>
/// <param name="Kind"><c>customer</c> | <c>staff</c> | <c>service</c>.</param>
/// <param name="Permissions">Phạm vi (<c>*</c>, <c>wh:2</c>, <c>farm:1</c>…) → mã quyền.</param>
/// <param name="AuthTime">Lúc người dùng xác thực.</param>
/// <param name="AuthMethods">Phương thức xác thực (<c>pwd</c>, <c>mfa</c>…).</param>
public sealed record AccessTokenRequest(
    string Subject,
    string Kind,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Permissions,
    DateTimeOffset AuthTime,
    IReadOnlyCollection<string> AuthMethods);

/// <summary>Access token đã ký.</summary>
/// <param name="Token">JWT compact.</param>
/// <param name="ExpiresAt">Thời điểm hết hạn.</param>
public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Cấp access token JWT ES256 (spec §3.2).</summary>
public interface IAccessTokenIssuer
{
    /// <summary>Ký access token mới với khóa hiện hành.</summary>
    /// <param name="request">Claim của token.</param>
    /// <returns>Token và thời điểm hết hạn.</returns>
    IssuedAccessToken Issue(AccessTokenRequest request);
}
