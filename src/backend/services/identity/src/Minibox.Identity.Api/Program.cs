using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.BuildingBlocks.Infrastructure.Migrations;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Infrastructure.Accounts;
using Minibox.Identity.Infrastructure.Tokens;
using Npgsql;

// --migrate: áp migration bằng role identity_migrator rồi thoát (spec §4.4). Chuỗi kết nối migrator chỉ lấy từ biến môi trường
// do job deploy cấp từ secret; không đặt trong appsettings, không dùng cho runtime.
if (DbMigrator.RunIfRequested(args, "identity", typeof(Program).Assembly,
        () => Environment.GetEnvironmentVariable(Program.MigratorConnectionVariable)
              ?? throw new InvalidOperationException($"Thiếu biến môi trường {Program.MigratorConnectionVariable}")) is int code)
    return code;

var builder = WebApplication.CreateBuilder(args);
builder.AddMiniboxObservability("identity");
builder.Services
    .AddMiniboxWeb()
    // Runtime dùng role identity_app (chỉ DML): ConnectionStrings__App.
    .AddSingleton(sp => NpgsqlDataSource.Create(sp.GetRequiredService<IConfiguration>().GetConnectionString("App")
        ?? throw new InvalidOperationException("Thiếu ConnectionStrings:App")))
    .AddSingleton<IPasswordHasher, Argon2idPasswordHasher>()
    .AddSingleton(TimeProvider.System)
    .AddSingleton(sp => new SigningKeyRing(sp.GetRequiredService<TimeProvider>(), SigningKeyRing.DefaultPeriod))
    .AddSingleton<IRefreshTokens, PostgresRefreshTokens>()
    .AddSingleton<IPasswordLogin, PostgresPasswordLogin>()
    .AddSingleton<IPermissions, PostgresPermissions>()
    // Mfa:EncryptionKey: base64 32 byte, từ Docker secret; mất khóa = mọi secret TOTP vô hiệu.
    .AddSingleton<IMfa>(sp => new PostgresMfa(sp.GetRequiredService<NpgsqlDataSource>(),
        Convert.FromBase64String(sp.GetRequiredService<IConfiguration>()["Mfa:EncryptionKey"] ?? throw new InvalidOperationException("Thiếu cấu hình Mfa:EncryptionKey")),
        sp.GetRequiredService<IRefreshTokens>(), sp.GetRequiredService<TimeProvider>()))
    .AddSingleton<IAccessTokenIssuer>(sp => new JwtAccessTokenIssuer(
        sp.GetRequiredService<SigningKeyRing>(), sp.GetRequiredService<TimeProvider>(), Program.Issuer(sp.GetRequiredService<IConfiguration>())))
    .AddSingleton<IRoleAssignments, PostgresRoleAssignments>();

// Identity tự kiểm token của mình: cùng luật với service khác, nhưng khóa lấy thẳng từ key ring thay vì gọi JWKS qua HTTP.
builder.Services.AddMiniboxJwtAuthentication("http://identity.self");
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<SigningKeyRing, IConfiguration>((o, keys, config) =>
{
    // Thiếu Jwt:Issuer thì không token nào khớp iss (fail-closed); request không token (health) vẫn chạy.
    var issuer = config["Jwt:Issuer"]?.TrimEnd('/');
    o.Authority = null;
    o.Configuration = new OpenIdConnectConfiguration { Issuer = issuer };
    o.TokenValidationParameters.ValidIssuer = issuer;
    o.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, _, _) => keys.Published;
});

var app = builder.Build();
app.UseMiniboxWeb();
app.MapMiniboxHealthChecks();

// Quản trị phân công (spec §3.6): mã quyền ở bất kỳ phạm vi để vào endpoint, rồi đúng phạm vi của phân công; đổi quyền là thao tác nhạy cảm (step-up).
var assignments = app.MapGroup("/admin/accounts/{accountId:guid}/assignments");
assignments.MapPost("", [RequirePermission(Program.AssignmentPermission)] [Sensitive] async (
        Guid accountId, Program.AssignmentRequest body, ICurrentUser user, IAuthorizer authz, IRoleAssignments store, CancellationToken ct) =>
    await Program.AdminAsync(user, authz, store, accountId, body.Role, body.Scope, actor => store.GrantAsync(actor, accountId, body.Role, body.Scope, ct), ct))
    .Produces(StatusCodes.Status204NoContent);
assignments.MapDelete("{role}/{scope}", [RequirePermission(Program.AssignmentPermission)] [Sensitive] async (
        Guid accountId, string role, string scope, ICurrentUser user, IAuthorizer authz, IRoleAssignments store, CancellationToken ct) =>
    await Program.AdminAsync(user, authz, store, accountId, role, scope, actor => store.RevokeAsync(actor, accountId, role, scope, ct), ct))
    .Produces(StatusCodes.Status204NoContent);
// Phiên cho BFF (spec §3.2): token chỉ trả cho BFF trong mạng nội bộ, BFF giữ trong cookie mã hóa. POST vẫn cần Idempotency-Key như mọi API ghi.
var auth = app.MapGroup("/auth");
auth.MapPost("login", async (Program.LoginRequest body, IPasswordLogin login, IPermissions perms, IAccessTokenIssuer issuer, CancellationToken ct) =>
{
    var result = await login.LoginAsync(body.Identifier, body.Password, body.Ip ?? "", ct);
    return result.IsSuccess
        ? Results.Ok(await Program.TokensAsync(result.Value.Session, perms, issuer, ct))
        : ResultHttpExtensions.Problem(result.Error);
}).Produces<Program.TokenResponse>();
// Yếu tố thứ hai và xác thực lại: tài khoản lấy từ access token (sub), không từ body; phiên cũ (chỉ pwd) bị thu hồi.
auth.MapPost("mfa/verify", [AllowWithoutMfa] async (Program.MfaVerifyRequest body, ICurrentUser user, IMfa mfa, IRefreshTokens refresh,
        IPermissions perms, IAccessTokenIssuer issuer, CancellationToken ct) =>
{
    if (!Guid.TryParse(user.UserId, out var accountId))
        return Results.Unauthorized();
    var verified = await mfa.VerifyAsync(accountId, body.Code ?? "", ct);
    if (!verified.IsSuccess)
        return ResultHttpExtensions.Problem(verified.Error);
    if (!string.IsNullOrEmpty(body.RefreshToken))
        await refresh.RevokeAsync(body.RefreshToken, ct);
    return Results.Ok(await Program.TokensAsync(verified.Value, perms, issuer, ct));
}).RequireAuthorization().Produces<Program.TokenResponse>();
auth.MapPost("refresh", async (Program.RefreshRequest body, IRefreshTokens refresh, IPermissions perms, IAccessTokenIssuer issuer, CancellationToken ct) =>
{
    var rotated = await refresh.RotateAsync(body.RefreshToken ?? "", ct);
    return rotated.IsSuccess
        ? Results.Ok(await Program.TokensAsync(rotated.Value, perms, issuer, ct))
        : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: rotated.Error.Code, detail: rotated.Error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = rotated.Error.Code });
}).Produces<Program.TokenResponse>();
auth.MapPost("logout", async (Program.RefreshRequest body, IRefreshTokens refresh, CancellationToken ct) =>
{
    if (!string.IsNullOrEmpty(body.RefreshToken))
        await refresh.RevokeAsync(body.RefreshToken, ct);
    return Results.NoContent();
}).Produces(StatusCodes.Status204NoContent);
// Discovery tối thiểu + JWKS cho service khác tự kiểm chữ ký (JwtAuthenticationSetup); chỉ khóa công khai, chỉ trong mạng nội bộ.
app.MapGet("/.well-known/openid-configuration", (IConfiguration config) =>
    new { issuer = Program.Issuer(config), jwks_uri = Program.Issuer(config) + "/.well-known/jwks.json" });
app.MapGet("/.well-known/jwks.json", (SigningKeyRing keys) => keys.Jwks());
app.Run();
return 0;

/// <summary>Điểm vào của service identity: Api, hoặc job migrate khi có cờ <c>--migrate</c>.</summary>
public partial class Program
{
    /// <summary>Biến môi trường chứa chuỗi kết nối role <c>identity_migrator</c>, chỉ đọc khi chạy <c>--migrate</c>.</summary>
    public const string MigratorConnectionVariable = "MINIBOX_MIGRATOR_CONNECTION";

    /// <summary>Mã quyền gán/thu hồi phân công.</summary>
    public const string AssignmentPermission = "identity.assignment.manage";

    /// <summary>Thân request gán vai trò.</summary>
    /// <param name="Role">Mã vai trò (<c>warehouse</c>, <c>qc</c>…).</param>
    /// <param name="Scope">Phạm vi <c>*</c>, <c>wh:n</c> hoặc <c>farm:n</c>.</param>
    public sealed record AssignmentRequest(string Role, string Scope);

    /// <summary>Thân request đăng nhập mật khẩu do BFF gửi.</summary>
    /// <param name="Identifier">Email hoặc username.</param>
    /// <param name="Password">Mật khẩu.</param>
    /// <param name="Ip">IP người dùng cuối mà BFF thấy (dùng để giới hạn đăng nhập sai).</param>
    public sealed record LoginRequest(string Identifier, string Password, string Ip);

    /// <summary>Thân request xác minh MFA (sau mật khẩu hoặc khi xác thực lại).</summary>
    /// <param name="Code">Mã TOTP 6 chữ số hoặc mã khôi phục.</param>
    /// <param name="RefreshToken">Refresh token của phiên hiện tại (không bắt buộc), bị thu hồi khi xác minh thành công.</param>
    public sealed record MfaVerifyRequest(string Code, string? RefreshToken = null);

    /// <summary>Thân request làm mới hoặc đăng xuất.</summary>
    /// <param name="RefreshToken">Refresh token hiện tại.</param>
    public sealed record RefreshRequest(string RefreshToken);

    /// <summary>Cặp token trả cho BFF; trình duyệt không bao giờ nhận.</summary>
    /// <param name="AccessToken">Access JWT 5 phút.</param>
    /// <param name="AccessTokenExpiresAt">Hạn access token.</param>
    /// <param name="RefreshToken">Refresh token mới (token cũ đã hết hiệu lực).</param>
    /// <param name="RefreshTokenExpiresAt">Hạn refresh token.</param>
    /// <param name="AuthMethods">Phương thức đã xác thực của phiên (<c>pwd</c>, <c>mfa</c>).</param>
    public sealed record TokenResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt,
        IReadOnlyList<string> AuthMethods);

    /// <summary>Cấp access token cho phiên với quyền hiện hành của tài khoản.</summary>
    /// <param name="session">Phiên refresh token vừa mở hoặc vừa xoay.</param>
    /// <param name="perms">Nguồn quyền.</param>
    /// <param name="issuer">Bộ ký access token.</param>
    /// <param name="ct">Huỷ thao tác.</param>
    /// <returns>Cặp token cho BFF.</returns>
    internal static async Task<TokenResponse> TokensAsync(RefreshSession session, IPermissions perms, IAccessTokenIssuer issuer, CancellationToken ct)
    {
        var access = issuer.Issue(new AccessTokenRequest(session.AccountId.ToString(), session.Kind.ToString().ToLowerInvariant(),
            await perms.ForAccountAsync(session.AccountId, ct), session.AuthTime, session.AuthMethods));
        return new TokenResponse(access.Token, access.ExpiresAt, session.Token, session.ExpiresAt, session.AuthMethods);
    }

    /// <summary>Chống leo thang đặc quyền rồi chạy thay đổi với actor = <c>sub</c>: người gọi phải có <see cref="AssignmentPermission"/>
    /// và mọi quyền của vai trò trong <paramref name="scope"/>, và không đổi phân công của chính mình.</summary>
    /// <param name="user">Người gọi.</param>
    /// <param name="authz">Bộ kiểm quyền.</param>
    /// <param name="store">Kho phân công (đọc quyền của vai trò).</param>
    /// <param name="accountId">Tài khoản bị đổi phân công.</param>
    /// <param name="role">Mã vai trò.</param>
    /// <param name="scope">Phạm vi của phân công bị đổi.</param>
    /// <param name="change">Thay đổi cần chạy.</param>
    /// <param name="ct">Huỷ thao tác.</param>
    /// <returns>204 khi thành công; 403 khi thiếu quyền, tự đổi phân công của mình hoặc <c>sub</c> không phải id tài khoản; lỗi của thay đổi.</returns>
    internal static async Task<IResult> AdminAsync(ICurrentUser user, IAuthorizer authz, IRoleAssignments store, Guid accountId, string role, string scope,
        Func<Guid, Task<Result>> change, CancellationToken ct)
    {
        var forbidden = ResultHttpExtensions.Problem(Error.Forbidden("Auth.Forbidden", "Không có quyền thực hiện thao tác."));
        if (!Guid.TryParse(user.UserId, out var actor) || actor == accountId)
            return forbidden;
        // Vai trò không tồn tại: không có quyền nào để so, để thay đổi trả 404. HasPermissionAsync đã tính phạm vi *.
        foreach (var permission in (await store.PermissionsOfRoleAsync(role, ct) ?? []).Append(AssignmentPermission))
            if (!await authz.HasPermissionAsync(permission, scope, ct))
                return forbidden;
        return (await change(actor)).ToHttpResult();
    }

    /// <summary>Đọc <c>Jwt:Issuer</c>: URL nội bộ của identity, dùng làm <c>iss</c> và gốc discovery/JWKS.</summary>
    /// <param name="config">Cấu hình.</param>
    /// <returns>URL không có dấu <c>/</c> cuối.</returns>
    /// <exception cref="InvalidOperationException">Thiếu cấu hình.</exception>
    public static string Issuer(IConfiguration config) =>
        (config["Jwt:Issuer"] ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Issuer")).TrimEnd('/');
}
