using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Minibox.BuildingBlocks.Application.Security;
using Minibox.Identity.Application.Tokens;

namespace Minibox.Identity.Infrastructure.Tokens;

/// <summary>Cấp access token JWT ES256 hạn <see cref="AccessTokenClaims.Lifetime"/>, ký bằng khóa hiện hành của <see cref="SigningKeyRing"/>.</summary>
/// <param name="keys">Key ring.</param>
/// <param name="time">Đồng hồ.</param>
/// <param name="issuer">Giá trị <c>iss</c> — URL nội bộ của identity (service khác lấy discovery/JWKS từ đây).</param>
public sealed class JwtAccessTokenIssuer(SigningKeyRing keys, TimeProvider time, string issuer) : IAccessTokenIssuer
{
    static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    /// <inheritdoc />
    public IssuedAccessToken Issue(AccessTokenRequest request)
    {
        var now = time.GetUtcNow();
        var expires = now + AccessTokenClaims.Lifetime;
        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = AccessTokenClaims.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(keys.Current, SecurityAlgorithms.EcdsaSha256),
            Claims = new Dictionary<string, object>
            {
                [AccessTokenClaims.Subject] = request.Subject,
                [AccessTokenClaims.Kind] = request.Kind,
                [AccessTokenClaims.Permissions] = JsonSerializer.SerializeToElement(request.Permissions),
                [AccessTokenClaims.AuthTime] = request.AuthTime.ToUnixTimeSeconds(),
                [AccessTokenClaims.AuthMethods] = request.AuthMethods.ToArray(),
                [AccessTokenClaims.TokenId] = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(16)),
            },
        });
        return new IssuedAccessToken(token, expires);
    }
}
