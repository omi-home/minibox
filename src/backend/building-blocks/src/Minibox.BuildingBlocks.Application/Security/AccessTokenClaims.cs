namespace Minibox.BuildingBlocks.Application.Security;

/// <summary>Hợp đồng access token giữa <c>identity</c> (cấp) và mọi service (kiểm) — spec §3.2, §3.6.</summary>
public static class AccessTokenClaims
{
    /// <summary>Audience chung của access token nội bộ.</summary>
    public const string Audience = "minibox";

    /// <summary>Thuật toán ký duy nhất được chấp nhận.</summary>
    public const string Algorithm = "ES256";

    /// <summary>Thời hạn access token (D5).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>Định danh tài khoản hoặc service.</summary>
    public const string Subject = "sub";

    /// <summary><c>customer</c> | <c>staff</c> | <c>service</c>.</summary>
    public const string Kind = "kind";

    /// <summary>Đối tượng JSON phạm vi → mã quyền, ví dụ <c>{"*":[…],"wh:2":[…]}</c>.</summary>
    public const string Permissions = "perms";

    /// <summary>Thời điểm xác thực (Unix giây), dùng cho step-up.</summary>
    public const string AuthTime = "auth_time";

    /// <summary>Mảng phương thức xác thực, ví dụ <c>["pwd","mfa"]</c>.</summary>
    public const string AuthMethods = "amr";

    /// <summary>Mã token duy nhất.</summary>
    public const string TokenId = "jti";
}
