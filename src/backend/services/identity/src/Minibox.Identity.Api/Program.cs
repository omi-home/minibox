using Minibox.BuildingBlocks.Infrastructure.Migrations;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Infrastructure.Accounts;
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
    .AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

var app = builder.Build();
app.UseMiniboxWeb();
app.MapMiniboxHealthChecks();
app.Run();
return 0;

/// <summary>Điểm vào của service identity: Api, hoặc job migrate khi có cờ <c>--migrate</c>.</summary>
public partial class Program
{
    /// <summary>Biến môi trường chứa chuỗi kết nối role <c>identity_migrator</c>, chỉ đọc khi chạy <c>--migrate</c>.</summary>
    public const string MigratorConnectionVariable = "MINIBOX_MIGRATOR_CONNECTION";
}
