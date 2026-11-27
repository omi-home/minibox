using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Infrastructure.Messaging;
using Minibox.BuildingBlocks.Infrastructure.Migrations;
using Minibox.BuildingBlocks.Web;
using Minibox.Localization.Api.Endpoints.Locales;
using Minibox.Localization.Api.Endpoints.Messages;
using Minibox.Localization.Application;
using Minibox.Localization.Infrastructure;
using Minibox.Localization.Infrastructure.Messaging;
using Minibox.Localization.Infrastructure.Persistence;
using Npgsql;
using Wolverine;

// --migrate: áp migration bằng role localization_migrator rồi thoát (spec §4.4). Chuỗi kết nối migrator chỉ lấy từ biến môi trường
// do job deploy cấp từ secret; không đặt trong appsettings, không dùng cho runtime.
if (DbMigrator.RunIfRequested(args, "localization", typeof(Program).Assembly,
        () => Environment.GetEnvironmentVariable(Program.MigratorConnectionVariable)
              ?? throw new InvalidOperationException($"Thiếu biến môi trường {Program.MigratorConnectionVariable}")) is int code)
    return code;

// Composition root (spec §5.2): chỉ đăng ký và map; endpoint nằm ở Endpoints/, xử lý ở Application/Features (StructureRules kiểm).
var builder = WebApplication.CreateBuilder(args);
// Runtime dùng role localization_app (chỉ DML): ConnectionStrings__App; RabbitMQ: ConnectionStrings__RabbitMq (amqp://…).
// Lúc build sinh OpenAPI (contracts/openapi) host không có cấu hình: dùng giá trị giữ chỗ, không kết nối.
var appConnection = builder.Configuration.RequiredConnectionString("App", "Host=openapi-build");
var rabbitMq = builder.Configuration.RequiredConnectionString("RabbitMq", "amqp://openapi-build");
// URL nội bộ của identity (trùng iss của access token) để lấy JWKS: Identity__Authority.
var identityAuthority = builder.Configuration["Identity:Authority"]
    ?? (MiniboxConfiguration.IsGeneratingOpenApi ? "http://openapi-build" : throw new InvalidOperationException("Thiếu Identity:Authority"));

builder.AddMiniboxObservability("localization");
// Wolverine kết nối PostgreSQL/RabbitMQ ngay khi dựng host; bước sinh OpenAPI lúc build chỉ cần metadata endpoint nên bỏ qua.
if (!MiniboxConfiguration.IsGeneratingOpenApi)
    builder.Host.UseWolverine(o =>
    {
        o.UseMiniboxMessaging(appConnection, LocalizationDbContext.Schema, new Uri(rabbitMq));
        o.PublishLocalizationEvents();
        // Handler nghiệp vụ đi qua Mediator; tắt quét quy ước để Wolverine không nhận nhầm *Handler. Consumer khai báo tường minh khi có.
        o.Discovery.DisableConventionalDiscovery();
    });
builder.Services
    .AddMiniboxWeb()
    .AddMiniboxJwtAuthentication(identityAuthority)
    .AddSingleton(_ => NpgsqlDataSource.Create(appConnection))
    .AddLocalizationApplication()
    .AddLocalizationInfrastructure(appConnection)
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

var app = builder.Build();
app.UseMiniboxWeb();
app.MapMiniboxHealthChecks();
app.MapLocaleEndpoints();
app.MapMessageEndpoints();
app.Run();
return 0;

/// <summary>Điểm vào của service localization: Api, hoặc job migrate khi có cờ <c>--migrate</c>.</summary>
public partial class Program
{
    /// <summary>Biến môi trường chứa chuỗi kết nối role <c>localization_migrator</c>, chỉ đọc khi chạy <c>--migrate</c>.</summary>
    public const string MigratorConnectionVariable = "MINIBOX_MIGRATOR_CONNECTION";
}
