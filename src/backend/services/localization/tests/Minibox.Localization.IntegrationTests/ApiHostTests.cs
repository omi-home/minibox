using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Application.Features.Locales.Queries.ListLocales;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine.Persistence.Durability;

namespace Minibox.Localization.IntegrationTests;

/// <summary>Api thật trên PostgreSQL + RabbitMQ tạm: --migrate, health, query qua pipeline Mediator, mapping EF và ràng buộc khớp DDL.</summary>
public sealed class ApiHostTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    WebApplicationFactory<Program> _factory = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role localization_app/localization_migrator.
    // search_path như role migrator thật để migration không ghi schema rơi vào localization.
    string Connection => _pg.GetConnectionString() + ";Search Path=localization";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        await using (var c = new NpgsqlConnection(_pg.GetConnectionString()))
        {
            await c.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("CREATE SCHEMA localization; CREATE ROLE localization_app", c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal(0, Migrate());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:App", Connection)
            .UseSetting("ConnectionStrings:RabbitMq", _mq.GetConnectionString()));
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

    [Fact(DisplayName = "Bảng Wolverine do migration 0004_wolverine.sql tạo khớp cấu hình Wolverine hiện tại")]
    public async Task wolverine_tables_match_configuration()
    {
        var store = (Weasel.Core.Migrations.IDatabase)_factory.Services.GetRequiredService<IMessageStore>();
        await store.AssertDatabaseMatchesConfigurationAsync(Ct);
    }

    [Fact(DisplayName = "GET /locales trả locale đang bật đã seed (vi rồi en, en rơi về vi), không cần đăng nhập")]
    public async Task seeded_locales_listed() =>
        Assert.Equal([new LocaleDto("vi", "Tiếng Việt", null), new LocaleDto("en", "English", "vi")],
            (await _factory.CreateClient().GetFromJsonAsync<LocaleDto[]>("/locales", Ct))!);

    [Fact(DisplayName = "Mapping EF khớp DDL: locale tắt không hiện, bật thì xếp theo sort_order")]
    public async Task ef_mapping_matches_migrated_schema()
    {
        await Exec("INSERT INTO locale (code, name, sort_order, fallback_code) VALUES ('ja', '日本語', -1, 'en')");
        await using (var scope = _factory.Services.CreateAsyncScope())
            Assert.Equal(["vi", "en"], (await scope.ServiceProvider.GetRequiredService<ILocaleReadService>().ListEnabledAsync(Ct)).Select(l => l.Code));

        await Exec("UPDATE locale SET enabled = true WHERE code = 'ja'");
        await using (var scope = _factory.Services.CreateAsyncScope())
            Assert.Equal([new LocaleDto("ja", "日本語", "en"), new LocaleDto("vi", "Tiếng Việt", null), new LocaleDto("en", "English", "vi")],
                await scope.ServiceProvider.GetRequiredService<ILocaleReadService>().ListEnabledAsync(Ct));
    }

    [Theory(DisplayName = "Ràng buộc DB chặn dữ liệu sai: mã locale/tiền tệ, fallback chính nó, tỷ giá VND/âm, chuỗi trùng hoặc xuất bản nửa vời")]
    [InlineData("INSERT INTO locale (code, name) VALUES ('VI_vn', 'x')")]
    [InlineData("INSERT INTO locale (code, name, fallback_code) VALUES ('ja', 'x', 'ja')")]
    [InlineData("INSERT INTO locale (code, name, fallback_code) VALUES ('ja', 'x', 'ko')")]
    [InlineData("INSERT INTO currency (code, name, symbol, minor_unit) VALUES ('usd', 'x', '$', 2)")]
    [InlineData("INSERT INTO exchange_rate (id, currency_code, vnd_per_unit, effective_at, actor) VALUES (gen_random_uuid(), 'VND', 1, now(), 's')")]
    [InlineData("INSERT INTO currency (code, name, symbol, minor_unit) VALUES ('USD', 'Đô la Mỹ', '$', 2); INSERT INTO exchange_rate (id, currency_code, vnd_per_unit, effective_at, actor) VALUES (gen_random_uuid(), 'USD', 0, now(), 's')")]
    [InlineData("INSERT INTO ui_message (id, namespace, key, locale_code, draft_text) VALUES (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'a'), (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'b')")]
    [InlineData("INSERT INTO ui_message (id, namespace, key, locale_code, draft_text, published_text) VALUES (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'a', 'a')")]
    [InlineData("INSERT INTO ui_message (id, namespace, key, locale_code, draft_text) VALUES (gen_random_uuid(), 'shop', 'cart.title', 'xx', 'a')")]
    public async Task constraints_reject_bad_rows(string sql) =>
        await Assert.ThrowsAsync<PostgresException>(() => Exec(sql));

    [Fact(DisplayName = "Seed: VND là tiền tệ gốc; một chuỗi giao diện mỗi (namespace, key, locale) ghi được cho mọi locale")]
    public async Task seed_and_valid_rows()
    {
        Assert.Equal(1L, await Scalar("SELECT count(*) FROM currency WHERE code = 'VND' AND enabled AND minor_unit = 0"));
        await Exec("""
            INSERT INTO ui_message (id, namespace, key, locale_code, draft_text, published_text, published_at) VALUES
              (gen_random_uuid(), 'shop', 'cart.title', 'vi', 'Giỏ hàng', 'Giỏ hàng', now()),
              (gen_random_uuid(), 'shop', 'cart.title', 'en', 'Cart', NULL, NULL)
            """);
        Assert.Equal(2L, await Scalar("SELECT count(*) FROM ui_message WHERE key = 'cart.title'"));
    }

    async Task Exec(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    async Task<long> Scalar(string sql)
    {
        await using var c = new NpgsqlConnection(Connection);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        return (long)(await cmd.ExecuteScalarAsync(Ct))!;
    }
}
