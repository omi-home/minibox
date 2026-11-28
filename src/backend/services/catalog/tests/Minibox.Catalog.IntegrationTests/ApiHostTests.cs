using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Localization;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.RabbitMQ;

namespace Minibox.Catalog.IntegrationTests;

/// <summary>Api thật trên PostgreSQL + RabbitMQ tạm: --migrate, health, bảng Wolverine khớp cấu hình, bản sao locale.</summary>
public sealed class ApiHostTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    WebApplicationFactory<Program> _factory = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role catalog_app/catalog_migrator.
    // search_path như role migrator thật để migration không ghi schema rơi vào catalog.
    string Connection => _pg.GetConnectionString() + ";Search Path=catalog";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        await using (var c = new NpgsqlConnection(_pg.GetConnectionString()))
        {
            await c.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("CREATE SCHEMA catalog; CREATE ROLE catalog_app", c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal(0, Migrate());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:App", Connection)
            .UseSetting("ConnectionStrings:RabbitMq", _mq.GetConnectionString())
            // Thay localization phát sự kiện: cùng exchange fanout mà localization dùng thật.
            .ConfigureServices(s => s.ConfigureWolverine(o => o.PublishMessage<LocaleChangedMessage>()
                .ToRabbitExchange(LocaleReplicaSetup.LocaleChangedExchange, e => e.ExchangeType = ExchangeType.Fanout))));
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    int Migrate()
    {
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, Connection);
        try
        {
            return (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!;
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
    }

    [Theory(DisplayName = "Api trả 200 ở /health/live và /health/ready khi PostgreSQL sẵn sàng")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task health_endpoints_ok(string path) =>
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync(path, Ct)).StatusCode);

    [Fact(DisplayName = "--migrate chạy lại lần hai vẫn thoát mã 0 (migration đã áp bỏ qua)")]
    public void migrate_is_rerunnable() => Assert.Equal(0, Migrate());

    [Fact(DisplayName = "Bảng Wolverine do migration 0003_wolverine.sql tạo khớp cấu hình Wolverine hiện tại")]
    public async Task wolverine_tables_match_configuration()
    {
        var store = (Weasel.Core.Migrations.IDatabase)_factory.Services.GetRequiredService<IMessageStore>();
        await store.AssertDatabaseMatchesConfigurationAsync(Ct);
    }

    async Task<IReadOnlyList<string>> Chain(string locale)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILocaleReplica>().ChainAsync(locale, Ct);
    }

    [Fact(DisplayName = "#483: catalog có bản sao locale seed vi/en, cập nhật khi nhận localization.LocaleChanged.v1")]
    public async Task locale_replica_follows_locale_changed()
    {
        Assert.Equal(["en", "vi"], await Chain("en"));
        Assert.Equal(["vi"], await Chain("ja"));

        await using (var scope = _factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new LocaleChangedMessage("ja", "日本語", true, 2, "en"));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        IReadOnlyList<string> chain;
        while ((chain = await Chain("ja")).Count == 1 && DateTime.UtcNow < deadline)
            await Task.Delay(200, Ct);
        Assert.Equal(["ja", "en", "vi"], chain);
    }
}
