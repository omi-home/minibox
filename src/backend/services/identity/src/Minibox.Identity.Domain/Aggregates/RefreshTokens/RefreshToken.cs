using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Domain.Aggregates.RefreshTokens;

/// <summary>Một refresh token của phiên (spec §3.2), khoá là SHA-256 của token ngẫu nhiên 256 bit (server không giữ token thô).
/// Các token cùng <see cref="FamilyId"/> là một phiên: mỗi lần làm mới xoay sang token mới, token cũ thành đã dùng.</summary>
/// <remarks>Token đã dùng bị đưa lại là dấu hiệu bị đánh cắp: thu hồi cả family (thao tác hàng loạt của repository), không có thời gian ân hạn.</remarks>
public sealed class RefreshToken : AggregateRoot<byte[]>
{
    /// <summary>Độ dài tối đa của token client gửi lên (token thật 43 ký tự); dài hơn bị từ chối không băm.</summary>
    public const int MaxTokenLength = 64;

    RefreshToken(byte[] id, Guid familyId, Guid accountId, DateTimeOffset authTime, string[] amr,
        DateTimeOffset issuedAt, DateTimeOffset expiresAt, DateTimeOffset? familyExpiresAt) : base(id)
    {
        FamilyId = familyId;
        AccountId = accountId;
        AuthTime = authTime;
        Amr = amr;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        FamilyExpiresAt = familyExpiresAt;
    }

    /// <summary>Phiên chứa token.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Tài khoản.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Thời điểm xác thực gốc của phiên (giữ qua các lần xoay).</summary>
    public DateTimeOffset AuthTime { get; private set; }

    /// <summary>Phương thức xác thực gốc của phiên (<c>pwd</c>, <c>mfa</c>).</summary>
    public string[] Amr { get; private set; }

    /// <summary>Lúc cấp.</summary>
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>Hạn của token này: min(lúc cấp + thời gian không hoạt động, hạn tối đa của phiên).</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Hạn tối đa của phiên; <c>null</c> = không giới hạn tuyệt đối (khách).</summary>
    public DateTimeOffset? FamilyExpiresAt { get; private set; }

    /// <summary>Lúc đã xoay sang token mới.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Lúc bị thu hồi.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Phiên bản (optimistic concurrency).</summary>
    public long Version { get; private set; }

    /// <summary>Mở phiên mới.</summary>
    /// <param name="accountId">Tài khoản.</param>
    /// <param name="kind">Loại tài khoản, quyết định thời hạn phiên (<see cref="SessionPolicy"/>).</param>
    /// <param name="amr">Phương thức đã xác thực.</param>
    /// <param name="now">Thời điểm xác thực.</param>
    /// <returns>Token đầu tiên của phiên và giá trị trả cho BFF (chỉ lộ một lần).</returns>
    public static (RefreshToken Token, string Value) Open(Guid accountId, AccountKind kind, IReadOnlyList<string> amr, DateTimeOffset now) =>
        Create(Guid.CreateVersion7(), accountId, kind, now, [.. amr], SessionPolicy.MaxLifetime(kind) is { } max ? now + max : null, now);

    /// <summary>Đổi token này lấy token mới cùng phiên; token này thành đã dùng.</summary>
    /// <param name="kind">Loại tài khoản hiện tại.</param>
    /// <param name="now">Thời điểm làm mới.</param>
    /// <returns>Token mới; lỗi <see cref="IdentityErrors.RefreshTokenReused"/> nếu token đã xoay bị đưa lại (kể cả đã hết hạn),
    /// <see cref="IdentityErrors.InvalidRefreshToken"/> nếu đã thu hồi hoặc hết hạn.</returns>
    public Result<(RefreshToken Token, string Value)> Rotate(AccountKind kind, DateTimeOffset now)
    {
        if (RevokedAt is not null)
            return IdentityErrors.InvalidRefreshToken;
        if (UsedAt is not null)
            return IdentityErrors.RefreshTokenReused;
        if (ExpiresAt <= now)
            return IdentityErrors.InvalidRefreshToken;
        UsedAt = now;
        return Create(FamilyId, AccountId, kind, AuthTime, Amr, FamilyExpiresAt, now);
    }

    /// <summary>Tính khoá lưu trữ của token client gửi lên.</summary>
    /// <param name="token">Token thô.</param>
    /// <returns>SHA-256 của UTF-8.</returns>
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    static (RefreshToken, string) Create(Guid family, Guid accountId, AccountKind kind, DateTimeOffset authTime, string[] amr,
        DateTimeOffset? familyExpires, DateTimeOffset now)
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var expires = now + SessionPolicy.IdleTimeout(kind);
        if (familyExpires < expires)
            expires = familyExpires.Value;
        return (new RefreshToken(Hash(value), family, accountId, authTime, amr, now, expires, familyExpires), value);
    }
}
