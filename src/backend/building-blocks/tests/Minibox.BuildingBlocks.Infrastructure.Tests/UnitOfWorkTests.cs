using System.Collections.Concurrent;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.BuildingBlocks.Infrastructure.Messaging;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.RabbitMQ;

namespace Minibox.BuildingBlocks.Infrastructure.Tests;

public sealed record ItemCreated(Guid ItemId, string Name) : IDomainEvent;

public sealed record ItemRenamed(Guid ItemId, string Name) : IDomainEvent;

/// <summary>Integration event mẫu (sẽ nằm trong *.Contracts).</summary>
public sealed record ItemCreatedV1(Guid ItemId);

public sealed class Item : AggregateRoot<Guid>
{
    Item(Guid id, string name) : base(id) => Name = name;

    public string Name { get; private set; }

    public static Item Create(Guid id, string name)
    {
        var item = new Item(id, name);
        item.Raise(new ItemCreated(id, name));
        return item;
    }

    public static Item Load(Guid id, string name) => new(id, name);

    public void Rename(string name)
    {
        Name = name;
        Raise(new ItemRenamed(Id, name));
    }
}

public sealed record ItemRow(Guid Id, string Name, long Version);

public sealed class ItemRepository(NpgsqlUnitOfWork uow) : DapperRepository<Item, Guid, ItemRow>(uow)
{
    protected override string Table => "item";
    protected override string InsertSql => "INSERT INTO item (id, name) VALUES (@Id, @Name)";
    protected override string UpdateSetSql => "name = @Name";
    protected override Item ToAggregate(ItemRow row) => Item.Load(row.Id, row.Name);
    protected override object ToParameters(Item item) => new { item.Id, item.Name };
}

sealed class ItemEventMapper : IIntegrationEventMapper
{
    public object? Map(IDomainEvent domainEvent) => domainEvent is ItemCreated e ? new ItemCreatedV1(e.ItemId) : null;
}

sealed class TestUser : ICurrentUser
{
    public string? UserId => "u1";
}

public static class ItemCreatedV1Handler
{
    public static readonly ConcurrentBag<Guid> Received = [];

    public static void Handle(ItemCreatedV1 e) => Received.Add(e.ItemId);
}

/// <summary>NpgsqlUnitOfWork + DapperRepository + Wolverine outbox/inbox trên PostgreSQL/RabbitMQ thật, chạy bằng role app (chỉ DML).</summary>
public sealed class UnitOfWorkTests : IAsyncLifetime
{
    const string Schema = "uowtest";
    const string CaptureQueue = "item-created-capture"; // không ai nghe: đếm message thô
    const string InboxQueue = "item-created";
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    IHost _app = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Mật khẩu "pw" chỉ dùng cho container tạm của test.
    string As(string role) => new NpgsqlConnectionStringBuilder(_pg.GetConnectionString()) { Username = role, Password = "pw" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(), _mq.StartAsync());
        await using (var c = new NpgsqlConnection(_pg.GetConnectionString()))
            await c.ExecuteAsync($"""
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

        var appCs = As($"{Schema}_app");
        _app = Host.CreateDefaultBuilder()
            .UseEnvironment("Development")
            .UseWolverine(o =>
            {
                o.UseMiniboxMessaging(appCs, Schema, new Uri(_mq.GetConnectionString()));
                o.PublishMessage<ItemCreatedV1>().ToRabbitQueue(CaptureQueue);
                o.ListenToRabbitQueue(InboxQueue);
                o.Discovery.DisableConventionalDiscovery().IncludeType(typeof(ItemCreatedV1Handler));
            })
            .ConfigureServices(s => s
                .AddSingleton(NpgsqlDataSource.Create(appCs))
                .AddSingleton<ICurrentUser, TestUser>()
                .AddSingleton(TimeProvider.System)
                .AddSingleton<IIntegrationEventMapper, ItemEventMapper>()
                .AddScoped<NpgsqlUnitOfWork>()
                .AddScoped<ItemRepository>())
            .Build();

        var wolverineDdl = ((Weasel.Core.Migrations.IDatabase)_app.Services.GetRequiredService<IMessageStore>()).ToDatabaseScript();
        var auditDdl = (await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "database/templates/audit-log.sql"))).Replace("{{svc}}", Schema);
        await using (var c = new NpgsqlConnection(As($"{Schema}_migrator")))
        {
            // UNIQUE DEFERRABLE: vi phạm chỉ nổ lúc COMMIT — sau khi outbox + audit đã ghi trong transaction.
            await c.ExecuteAsync("""
                CREATE TABLE item (
                  id uuid PRIMARY KEY, name text NOT NULL, version bigint NOT NULL DEFAULT 0,
                  CONSTRAINT item_name_uq UNIQUE (name) DEFERRABLE INITIALLY DEFERRED)
                """);
            await c.ExecuteAsync(auditDdl);
            await c.ExecuteAsync(wolverineDdl);
        }
        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        _app.Dispose();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "database", "templates"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo");
    }

    async Task<long> Count(string sql, object? args = null)
    {
        await using var c = new NpgsqlConnection(As($"{Schema}_app"));
        return await c.ExecuteScalarAsync<long>(sql, args);
    }

    static async Task<bool> WaitUntil(Func<Task<bool>> done, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (await done()) return true;
            await Task.Delay(100, Ct);
        }
        return await done();
    }

    async Task<IChannel> Channel()
    {
        var conn = await new ConnectionFactory { Uri = new Uri(_mq.GetConnectionString()) }.CreateConnectionAsync(Ct);
        return await conn.CreateChannelAsync(cancellationToken: Ct);
    }

    async Task CreateAsync(Guid id, string name)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<NpgsqlUnitOfWork>();
        await uow.BeginAsync(Ct);
        await scope.ServiceProvider.GetRequiredService<ItemRepository>().AddAsync(Item.Create(id, name), Ct);
        await uow.CommitAsync(Ct);
    }

    [Fact(DisplayName = "ac187_2: lỗi lúc commit sau khi đã ghi dữ liệu + outbox + audit → rollback hết, không có message")]
    public async Task ac187_2_failure_before_commit_rolls_back_everything()
    {
        var (a, b) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<NpgsqlUnitOfWork>();
            var repo = scope.ServiceProvider.GetRequiredService<ItemRepository>();
            await uow.BeginAsync(Ct);
            await repo.AddAsync(Item.Create(a, "trùng"), Ct);
            await repo.AddAsync(Item.Create(b, "trùng"), Ct);
            await Assert.ThrowsAsync<PostgresException>(() => uow.CommitAsync(Ct));
        }

        Assert.Equal(0, await Count("SELECT count(*) FROM item WHERE id = ANY(@ids)", new { ids = new[] { a, b } }));
        Assert.Equal(0, await Count("SELECT count(*) FROM audit_log WHERE aggregate_id = ANY(@ids)", new { ids = new[] { a.ToString(), b.ToString() } }));
        Assert.Equal(0, await Count("SELECT count(*) FROM wolverine_outgoing_envelopes"));
        await using var ch = await Channel();
        Assert.Equal(0u, await ch.MessageCountAsync(CaptureQueue, Ct));
    }

    [Fact(DisplayName = "ac187_3: commit → dữ liệu + audit, integration event ra RabbitMQ đúng một lần; consumer nhận trùng chỉ xử lý một lần")]
    public async Task ac187_3_commit_publishes_once_and_inbox_dedupes()
    {
        var id = Guid.CreateVersion7();
        await CreateAsync(id, "rau cải");

        Assert.Equal(1, await Count("SELECT count(*) FROM item WHERE id = @id", new { id }));
        Assert.Equal(1, await Count(
            "SELECT count(*) FROM audit_log WHERE aggregate_id = @id AND action = 'ItemCreated' AND actor = 'u1' AND payload->>'Name' = 'rau cải'",
            new { id = id.ToString() }));
        Assert.True(await WaitUntil(async () => await Count("SELECT count(*) FROM wolverine_outgoing_envelopes") == 0, TimeSpan.FromSeconds(30)));

        await using var ch = await Channel();
        Assert.True(await WaitUntil(async () => await ch.MessageCountAsync(CaptureQueue, Ct) >= 1, TimeSpan.FromSeconds(30)));
        var raw = await ch.BasicGetAsync(CaptureQueue, autoAck: true, Ct);
        Assert.NotNull(raw);
        Assert.Null(await ch.BasicGetAsync(CaptureQueue, autoAck: true, Ct)); // đúng một bản

        // Giao lại cùng một message (cùng envelope id) hai lần tới queue có consumer: inbox bền chỉ cho xử lý một lần.
        var props = new BasicProperties(raw.BasicProperties);
        for (var i = 0; i < 2; i++)
            await ch.BasicPublishAsync("", InboxQueue, mandatory: false, props, raw.Body, Ct);

        Assert.True(await WaitUntil(() => Task.FromResult(ItemCreatedV1Handler.Received.Contains(id)), TimeSpan.FromSeconds(30)));
        Assert.True(await WaitUntil(async () => await ch.MessageCountAsync(InboxQueue, Ct) == 0, TimeSpan.FromSeconds(30)));
        await Task.Delay(1000, Ct); // bản thứ hai đã rời queue; chờ handler (nếu bị gọi nhầm) chạy xong
        Assert.Equal(1, ItemCreatedV1Handler.Received.Count(x => x == id));
    }

    [Fact(DisplayName = "ac187_4: UpdateAsync với version cũ trả Conflict, không ghi đè")]
    public async Task ac187_4_stale_version_conflicts()
    {
        var id = Guid.CreateVersion7();
        await CreateAsync(id, "v0");

        async Task<Result> Rename(string name, long expectedVersion)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<NpgsqlUnitOfWork>();
            var repo = scope.ServiceProvider.GetRequiredService<ItemRepository>();
            await uow.BeginAsync(Ct);
            var item = (await repo.GetAsync(id, Ct))!;
            item.Rename(name);
            var result = await repo.UpdateAsync(item, expectedVersion, Ct);
            if (result.IsSuccess) await uow.CommitAsync(Ct);
            else await uow.RollbackAsync(Ct);
            return result;
        }

        Assert.True((await Rename("v1", 0)).IsSuccess);
        var stale = await Rename("ghi đè", 0);

        Assert.Equal(ErrorType.Conflict, stale.Error.Type);
        Assert.Equal(1, await Count("SELECT count(*) FROM item WHERE id = @id AND name = 'v1' AND version = 1", new { id }));
        Assert.Equal(0, await Count("SELECT count(*) FROM audit_log WHERE aggregate_id = @id AND payload->>'Name' = 'ghi đè'", new { id = id.ToString() }));
    }

    [Fact(DisplayName = "GetForUpdateAsync khoá dòng tới hết transaction")]
    public async Task get_for_update_locks_row()
    {
        var id = Guid.CreateVersion7();
        await CreateAsync(id, "khoá");

        await using var first = _app.Services.CreateAsyncScope();
        var uow1 = first.ServiceProvider.GetRequiredService<NpgsqlUnitOfWork>();
        await uow1.BeginAsync(Ct);
        Assert.NotNull(await first.ServiceProvider.GetRequiredService<ItemRepository>().GetForUpdateAsync(id, Ct));

        await using var second = _app.Services.CreateAsyncScope();
        var uow2 = second.ServiceProvider.GetRequiredService<NpgsqlUnitOfWork>();
        await uow2.BeginAsync(Ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => second.ServiceProvider.GetRequiredService<ItemRepository>().GetForUpdateAsync(id, timeout.Token));
        await uow1.RollbackAsync(Ct);
    }
}
