using JasperFx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Minibox.BuildingBlocks.Infrastructure.Tests;

/// <summary>Sự kiện mẫu của spike.</summary>
public sealed record SpikeItemCreated(Guid Id);

/// <summary>Đếm message nhận được từ RabbitMQ.</summary>
public static class SpikeItemCreatedHandler
{
    public static readonly System.Collections.Concurrent.ConcurrentBag<Guid> Received = [];

    public static void Handle(SpikeItemCreated e) => Received.Add(e.Id);
}

/// <summary>Bản ghi nghiệp vụ mẫu.</summary>
public sealed class SpikeItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>DbContext của spike: bảng nghiệp vụ + ánh xạ bảng envelope của Wolverine trong cùng schema.</summary>
public sealed class SpikeDbContext(DbContextOptions<SpikeDbContext> options) : DbContext(options)
{
    public DbSet<SpikeItem> Items => Set<SpikeItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<SpikeItem>(e =>
        {
            e.ToTable("item");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
        });
        b.MapWolverineEnvelopeStorage(WolverineOutboxSpikeTests.Schema);
    }
}

/// <summary>
/// Spike #429 (spec §2.1, thay spike #188 Dapper): outbox Wolverine ghi cùng transaction của EF Core <see cref="DbContext"/> (Npgsql),
/// chạy bằng role app chỉ có DML. Bảng Wolverine do migrator tạo (tương đương migration DbUp), app dùng <c>AutoCreate.None</c>.
/// </summary>
public sealed class WolverineOutboxSpikeTests : IAsyncLifetime
{
    public const string Schema = "spike";
    const string Queue = "spike-item-created";
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    IHost _app = null!;
    string WolverineDdl = "";

    // Mật khẩu "pw" chỉ dùng cho container Testcontainers tạm, không sao chép sang cấu hình thật.
    string As(string role) => new NpgsqlConnectionStringBuilder(_pg.GetConnectionString()) { Username = role, Password = "pw" }.ConnectionString;

    IHost Build(string cs) =>
        Host.CreateDefaultBuilder().ConfigureServices(s => s.AddDbContextWithWolverineIntegration<SpikeDbContext>(o => o.UseNpgsql(cs)))
            .UseWolverine(o =>
            {
                o.PersistMessagesWithPostgresql(cs, Schema);
                o.UseEntityFrameworkCoreTransactions();
                o.AutoBuildMessageStorageOnStartup = AutoCreate.None;
                o.UseRabbitMq(new Uri(_mq.GetConnectionString())).AutoProvision();
                o.PublishMessage<SpikeItemCreated>().ToRabbitQueue(Queue).UseDurableOutbox();
                o.ListenToRabbitQueue(Queue);
                o.Discovery.DisableConventionalDiscovery().IncludeType(typeof(SpikeItemCreatedHandler));
            }).Build();

    static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    async Task<long> Count(string sql)
    {
        await using var c = new NpgsqlConnection(As($"{Schema}_app"));
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(), _mq.StartAsync());
        // Thu nhỏ của bootstrap (spec §4.2–4.3): migrator chủ schema, app chỉ DML qua default privileges.
        await Exec(_pg.GetConnectionString(), $"""
            CREATE ROLE {Schema}_migrator LOGIN PASSWORD 'pw';
            CREATE ROLE {Schema}_app LOGIN PASSWORD 'pw';
            REVOKE ALL ON SCHEMA public FROM PUBLIC;
            CREATE SCHEMA {Schema} AUTHORIZATION {Schema}_migrator;
            GRANT USAGE ON SCHEMA {Schema} TO {Schema}_app;
            ALTER DEFAULT PRIVILEGES FOR ROLE {Schema}_migrator IN SCHEMA {Schema} GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {Schema}_app;
            ALTER DEFAULT PRIVILEGES FOR ROLE {Schema}_migrator IN SCHEMA {Schema} GRANT USAGE, SELECT ON SEQUENCES TO {Schema}_app;
            ALTER ROLE {Schema}_migrator SET search_path = {Schema};
            ALTER ROLE {Schema}_app SET search_path = {Schema};
            """);

        _app = Build(As($"{Schema}_app"));

        // DDL của Wolverine sinh offline từ cấu hình (host chưa start) → đây là nội dung file migration DbUp.
        WolverineDdl = ((Weasel.Core.Migrations.IDatabase)_app.Services.GetRequiredService<IMessageStore>()).ToDatabaseScript();

        // "Migration" chạy bằng migrator: bảng nghiệp vụ + bảng Wolverine.
        await Exec(As($"{Schema}_migrator"), "CREATE TABLE item (id uuid PRIMARY KEY, name text NOT NULL)");
        await Exec(As($"{Schema}_migrator"), WolverineDdl);

        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        _app.Dispose();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    /// <summary>Ghi nghiệp vụ qua EF Core + publish qua Wolverine trong một transaction tường minh của DbContext (mẫu Unit of Work).</summary>
    async Task WriteAndPublish(Guid id, bool commit)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SpikeDbContext>>();
        var db = outbox.DbContext;

        await using var tx = await db.Database.BeginTransactionAsync();
        db.Items.Add(new SpikeItem { Id = id, Name = "rau cải" });
        await outbox.PublishAsync(new SpikeItemCreated(id));
        await db.SaveChangesAsync(); // envelope ghi vào wolverine_outgoing_envelopes trong cùng tx

        // Trong tx (cùng kết nối) đã thấy envelope; kết nối khác thì chưa → envelope đi cùng transaction nghiệp vụ.
        Assert.Equal(1, await db.Database.SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM wolverine_outgoing_envelopes").SingleAsync());
        Assert.Equal(0, await Count("SELECT count(*) FROM wolverine_outgoing_envelopes"));

        if (commit)
        {
            await tx.CommitAsync();
            await outbox.FlushOutgoingMessagesAsync(); // sau commit mới đẩy ra RabbitMQ
        }
        else await tx.RollbackAsync();
    }

    static async Task WaitUntil(Func<Task<bool>> done)
    {
        var until = DateTime.UtcNow + Timeout;
        while (!await done())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("hết giờ chờ điều kiện của spike");
            await Task.Delay(100);
        }
    }

    [Fact(DisplayName = "spike #429: commit → dữ liệu + message ra RabbitMQ đúng một lần, outbox dọn sạch")]
    public async Task commit_delivers_once()
    {
        var id = Guid.CreateVersion7();
        await WriteAndPublish(id, commit: true);

        // Bản gửi trùng chỉ có thể đến từ envelope còn trong outbox; chờ nhận được và outbox về 0 rồi mới đếm.
        await WaitUntil(async () => SpikeItemCreatedHandler.Received.Contains(id) && await Count("SELECT count(*) FROM wolverine_outgoing_envelopes") == 0);
        Assert.Equal(1, SpikeItemCreatedHandler.Received.Count(x => x == id));
        Assert.Equal(1, await Count($"SELECT count(*) FROM item WHERE id = '{id}'"));
    }

    [Fact(DisplayName = "spike #429: rollback → không có dữ liệu, không có message, không còn gì trong outbox")]
    public async Task rollback_discards_message()
    {
        var id = Guid.CreateVersion7();
        await WriteAndPublish(id, commit: false);
        Assert.Equal(0, await Count($"SELECT count(*) FROM item WHERE id = '{id}'"));
        Assert.Equal(0, await Count("SELECT count(*) FROM wolverine_outgoing_envelopes"));

        // Sentinel commit sau cùng queue: nhận được sentinel mà không có message đã rollback → message đó bị huỷ, không gửi trễ.
        var sentinel = Guid.CreateVersion7();
        await WriteAndPublish(sentinel, commit: true);
        await WaitUntil(() => Task.FromResult(SpikeItemCreatedHandler.Received.Contains(sentinel)));
        Assert.DoesNotContain(id, SpikeItemCreatedHandler.Received);
    }

    [Fact(DisplayName = "spike #429: SaveChangesAndFlushMessagesAsync (không mở transaction tường minh) cũng ghi dữ liệu + message nguyên tử")]
    public async Task implicit_transaction_delivers_once()
    {
        var id = Guid.CreateVersion7();
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<SpikeDbContext>>();
            outbox.DbContext.Items.Add(new SpikeItem { Id = id, Name = "cà rốt" });
            await outbox.PublishAsync(new SpikeItemCreated(id));
            await outbox.SaveChangesAndFlushMessagesAsync(TestContext.Current.CancellationToken);
        }

        await WaitUntil(async () => SpikeItemCreatedHandler.Received.Contains(id) && await Count("SELECT count(*) FROM wolverine_outgoing_envelopes") == 0);
        Assert.Equal(1, SpikeItemCreatedHandler.Received.Count(x => x == id));
        Assert.Equal(1, await Count($"SELECT count(*) FROM item WHERE id = '{id}'"));
    }

    [Fact(DisplayName = "spike #429: DDL Wolverine sinh offline, nằm trong schema service, áp dụng được bằng migrator")]
    public void ddl_is_schema_scoped()
    {
        Assert.Contains($"{Schema}.wolverine_outgoing_envelopes", WolverineDdl);
        Assert.Contains($"{Schema}.wolverine_incoming_envelopes", WolverineDdl);
        Assert.DoesNotContain("public.", WolverineDdl);
    }
}
