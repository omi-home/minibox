using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.BuildingBlocks.Infrastructure.Localization;
using Minibox.BuildingBlocks.Infrastructure.Messaging;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence.Durability;
using Wolverine.RabbitMQ;

namespace Minibox.BuildingBlocks.Infrastructure.Tests;

/// <summary>Dòng bảng dịch theo mẫu database/templates/translation-table.sql với {{table}} = product.</summary>
public sealed class ProductTranslation
{
    public Guid ProductId { get; set; }
    public string Locale { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>DbContext của một service giả có bảng *_translation.</summary>
public sealed class TranslatedDbContext(DbContextOptions<TranslatedDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(LocaleReplicaTests.Schema);
        b.ApplyConfiguration(new LocaleReplicaConfiguration());
        b.Entity<ProductTranslation>(e =>
        {
            e.ToTable("product_translation");
            e.HasKey(x => new { x.ProductId, x.Locale });
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Locale).HasColumnName("locale");
            e.Property(x => x.Name).HasColumnName("name");
        });
        b.MapWolverineEnvelopeStorage(LocaleReplicaTests.Schema);
    }
}

/// <summary>#396: mẫu bảng *_translation + locale_replica, bản sao locale nhận localization.LocaleChanged.v1 qua RabbitMQ (exchange
/// fanout thật của localization), truy vấn bản dịch theo chuỗi fallback bằng role app chỉ có DML.</summary>
public sealed class LocaleReplicaTests : IAsyncLifetime
{
    public const string Schema = "demo";
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    IHost _app = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Mật khẩu "pw" chỉ dùng cho container Testcontainers tạm, không sao chép sang cấu hình thật.
    string As(string role) => new NpgsqlConnectionStringBuilder(_pg.GetConnectionString()) { Username = role, Password = "pw" }.ConnectionString;

    static string Template(string file)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "database", "templates", file);
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException(file);
    }

    async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        // Thu nhỏ của bootstrap (spec §4.2–4.3): migrator chủ schema, app chỉ DML qua default privileges.
        await Exec(_pg.GetConnectionString(), $"""
            CREATE ROLE {Schema}_migrator LOGIN PASSWORD 'pw';
            CREATE ROLE {Schema}_app LOGIN PASSWORD 'pw';
            CREATE SCHEMA {Schema} AUTHORIZATION {Schema}_migrator;
            GRANT USAGE ON SCHEMA {Schema} TO {Schema}_app;
            ALTER DEFAULT PRIVILEGES FOR ROLE {Schema}_migrator IN SCHEMA {Schema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {Schema}_app;
            ALTER DEFAULT PRIVILEGES FOR ROLE {Schema}_migrator IN SCHEMA {Schema} GRANT USAGE, SELECT ON SEQUENCES TO {Schema}_app;
            ALTER ROLE {Schema}_migrator SET search_path = {Schema};
            ALTER ROLE {Schema}_app SET search_path = {Schema};
            """);

        var app = As($"{Schema}_app");
        _app = Host.CreateDefaultBuilder()
            .UseEnvironment("Development")
            .ConfigureServices(s => s
                .AddDbContextWithWolverineIntegration<TranslatedDbContext>(o => o.UseNpgsql(app))
                .AddSingleton(TimeProvider.System)
                .AddLocaleReplica<TranslatedDbContext>())
            .UseWolverine(o =>
            {
                o.Discovery.DisableConventionalDiscovery();
                o.UseMiniboxMessaging(app, Schema, new Uri(_mq.GetConnectionString())).ReceiveLocaleChanges(o, Schema);
                // Thay localization phát sự kiện: cùng exchange fanout mà localization dùng thật.
                o.PublishMessage<LocaleChangedMessage>().ToRabbitExchange(LocaleReplicaSetup.LocaleChangedExchange, e => e.ExchangeType = ExchangeType.Fanout);
            })
            .Build();

        // "Migration" bằng migrator: bảng gốc + hai mẫu + bảng Wolverine.
        var migrator = As($"{Schema}_migrator");
        await Exec(migrator, "CREATE TABLE product (id uuid PRIMARY KEY)");
        await Exec(migrator, Template("locale-replica.sql"));
        await Exec(migrator, Template("translation-table.sql").Replace("{{table}}", "product"));
        await Exec(migrator, ((Weasel.Core.Migrations.IDatabase)_app.Services.GetRequiredService<IMessageStore>()).ToDatabaseScript());

        await _app.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        _app.Dispose();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    async Task<IReadOnlyList<string>> Chain(string locale)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ILocaleReplica>().ChainAsync(locale, Ct);
    }

    async Task<IReadOnlyList<string>> WaitChain(string locale, string[] expected)
    {
        var deadline = DateTime.UtcNow + Timeout;
        IReadOnlyList<string> chain;
        while (!(chain = await Chain(locale)).SequenceEqual(expected) && DateTime.UtcNow < deadline)
            await Task.Delay(200, Ct);
        return chain;
    }

    async Task Publish(LocaleChangedMessage e)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(e);
    }

    [Fact(DisplayName = "Bản sao locale: seed vi/en, nhận LocaleChanged qua exchange của localization, tắt locale thì về vi")]
    public async Task Replica_follows_locale_changed_events()
    {
        Assert.Equal(["en", "vi"], await Chain("en"));
        Assert.Equal(["vi"], await Chain("ja")); // chưa có

        await Publish(new LocaleChangedMessage("ja", "日本語", true, 2, "en"));
        Assert.Equal(["ja", "en", "vi"], await WaitChain("ja", ["ja", "en", "vi"]));

        await Publish(new LocaleChangedMessage("ja", "日本語", false, 2, "en"));
        Assert.Equal(["vi"], await WaitChain("ja", ["vi"]));
    }

    [Fact(DisplayName = "Bảng *_translation theo mẫu: khoá (id, locale), xoá bản gốc xoá bản dịch, đọc theo chuỗi fallback")]
    public async Task Translation_table_reads_with_fallback()
    {
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7();
        await Exec(As($"{Schema}_app"), $"""
            INSERT INTO product VALUES ('{a}'), ('{b}');
            INSERT INTO product_translation (product_id, locale, name) VALUES
              ('{a}', 'vi', 'Rau cải'), ('{a}', 'en', 'Mustard greens'), ('{b}', 'vi', 'Củ cải');
            """);
        await Publish(new LocaleChangedMessage("ja", "日本語", true, 2, "en"));
        var chain = await WaitChain("ja", ["ja", "en", "vi"]);

        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TranslatedDbContext>();
            var rows = await db.Set<ProductTranslation>().AsNoTracking().Where(t => chain.Contains(t.Locale)).ToListAsync(Ct);
            var names = LocaleChain.PickPerKey(rows, t => t.ProductId, t => t.Locale, chain);
            Assert.Equal("Mustard greens", names[a].Name); // ja thiếu → en
            Assert.Equal("Củ cải", names[b].Name);         // ja, en thiếu → vi
        }

        var dup = await Assert.ThrowsAsync<PostgresException>(() => Exec(As($"{Schema}_app"),
            $"INSERT INTO product_translation (product_id, locale, name) VALUES ('{a}', 'vi', 'trùng')"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, dup.SqlState);
        await Exec(As($"{Schema}_app"), $"DELETE FROM product WHERE id = '{a}'");
        await using var c = new NpgsqlConnection(As($"{Schema}_app"));
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand($"SELECT count(*) FROM product_translation WHERE product_id = '{a}'", c);
        Assert.Equal(0L, await cmd.ExecuteScalarAsync(Ct));
    }
}
