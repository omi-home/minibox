using JasperFx;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Infrastructure.Messaging;
using Minibox.BuildingBlocks.Infrastructure.Migrations;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Api.Endpoints.Assignments;
using Minibox.Identity.Api.Endpoints.Auth;
using Minibox.Identity.Api.Endpoints.Me;
using Minibox.Identity.Api.Endpoints.WellKnown;
using Minibox.Identity.Application;
using Minibox.Identity.Infrastructure;
using Minibox.Identity.Infrastructure.Persistence;
using Minibox.Identity.Infrastructure.Security;
using Npgsql;
using Wolverine;

// --migrate: áp migration bằng role identity_migrator rồi thoát (spec §4.4). Chuỗi kết nối migrator chỉ lấy từ biến môi trường
// do job deploy cấp từ secret; không đặt trong appsettings, không dùng cho runtime.
if (DbMigrator.RunIfRequested(args, "identity", typeof(Program).Assembly,
        () => Environment.GetEnvironmentVariable(Program.MigratorConnectionVariable)
              ?? throw new InvalidOperationException($"Thiếu biến môi trường {Program.MigratorConnectionVariable}")) is int code)
    return code;

// Composition root (spec §5.2): chỉ đăng ký và map; endpoint ở Endpoints/, xử lý ở Application/Features (StructureRules kiểm).
var builder = WebApplication.CreateBuilder(args);
// Staging/production: bí mật đọc từ Docker secret /run/secrets (ConnectionStrings__App, …), không qua biến môi trường.
builder.Configuration.AddMiniboxSecrets();
// Runtime dùng role identity_app (chỉ DML): ConnectionStrings__App; RabbitMQ: ConnectionStrings__RabbitMq (amqp://…).
// Lúc build sinh OpenAPI (contracts/openapi) host không có cấu hình: dùng giá trị giữ chỗ, không kết nối.
var appConnection = builder.Configuration.RequiredConnectionString("App", "Host=openapi-build");
var rabbitMq = builder.Configuration.RequiredConnectionString("RabbitMq", "amqp://openapi-build");

builder.AddMiniboxObservability("identity");
if (!MiniboxConfiguration.IsGeneratingOpenApi)
    builder.Host.UseWolverine(o =>
    {
        o.UseMiniboxMessaging(appConnection, IdentityDbContext.Schema, new Uri(rabbitMq));
        // Handler nghiệp vụ đi qua Mediator; tắt quét quy ước để Wolverine không nhận nhầm *Handler.
        o.Discovery.DisableConventionalDiscovery();
    });
builder.Services
    .AddMiniboxWeb()
    .AddSingleton(_ => NpgsqlDataSource.Create(appConnection))
    .AddIdentityApplication()
    .AddIdentityInfrastructure(appConnection)
    .AddMediator(options =>
    {
        options.ServiceLifetime = ServiceLifetime.Scoped;
        // Thứ tự spec §5.4: logging → validation → authorization → transaction (chỉ command) → idempotency (trong transaction).
        options.PipelineBehaviors =
        [
            typeof(LoggingBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(AuthorizationBehavior<,>),
            typeof(TransactionBehavior<,>),
            typeof(IdempotencyBehavior<,>),
        ];
    });

// Identity tự kiểm token của mình: cùng luật với service khác, nhưng khoá lấy thẳng từ key ring thay vì gọi JWKS qua HTTP.
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
app.MapAuthEndpoints();
app.MapAssignmentEndpoints();
app.MapMeEndpoints();
app.MapWellKnownEndpoints();
// Lệnh JasperFx: không tham số thì chạy host; `codegen write` sinh code Wolverine lúc build image (TypeLoadMode.Static).
return await app.RunJasperFxCommands(args);

/// <summary>Điểm vào của service identity: Api, hoặc job migrate khi có cờ <c>--migrate</c>.</summary>
public partial class Program
{
    /// <summary>Biến môi trường chứa chuỗi kết nối role <c>identity_migrator</c>, chỉ đọc khi chạy <c>--migrate</c>.</summary>
    public const string MigratorConnectionVariable = "MINIBOX_MIGRATOR_CONNECTION";
}
