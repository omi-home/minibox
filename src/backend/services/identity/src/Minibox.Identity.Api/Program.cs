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
    .AddSingleton<IAccessTokenIssuer>(sp => new JwtAccessTokenIssuer(
        sp.GetRequiredService<SigningKeyRing>(), sp.GetRequiredService<TimeProvider>(), Program.Issuer(sp.GetRequiredService<IConfiguration>())));

var app = builder.Build();
app.UseMiniboxWeb();
app.MapMiniboxHealthChecks();
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

    /// <summary>Đọc <c>Jwt:Issuer</c>: URL nội bộ của identity, dùng làm <c>iss</c> và gốc discovery/JWKS.</summary>
    /// <param name="config">Cấu hình.</param>
    /// <returns>URL không có dấu <c>/</c> cuối.</returns>
    /// <exception cref="InvalidOperationException">Thiếu cấu hình.</exception>
    public static string Issuer(IConfiguration config) =>
        (config["Jwt:Issuer"] ?? throw new InvalidOperationException("Thiếu cấu hình Jwt:Issuer")).TrimEnd('/');
}
