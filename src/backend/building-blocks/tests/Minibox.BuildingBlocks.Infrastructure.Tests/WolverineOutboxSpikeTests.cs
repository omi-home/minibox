using Dapper;
using JasperFx;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;
using Wolverine.RDBMS;
using Wolverine.Runtime;

namespace Minibox.BuildingBlocks.Infrastructure.Tests;

/// <summary>Sự kiện mẫu của spike.</summary>
public sealed record SpikeItemCreated(Guid Id);

/// <summary>Đếm message nhận được từ RabbitMQ.</summary>
public static class SpikeItemCreatedHandler
{
    public static readonly System.Collections.Concurrent.ConcurrentBag<Guid> Received = [];

    public static void Handle(SpikeItemCreated e) => Received.Add(e.Id);
}

/// <summary>
/// Spike #188 (spec §2.1): Wolverine outbox ghi cùng <see cref="NpgsqlTransaction"/> với Dapper, chạy bằng role app không có DDL.
/// Bảng Wolverine do migrator tạo (tương đương migration), app dùng <c>AutoCreate.None</c>.
/// </summary>
public sealed class WolverineOutboxSpikeTests : IAsyncLifetime
{
    const string Schema = "spike";
    const string Queue = "spike-item-created";
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    IHost _app = null!;
    string WolverineDdl = "";

    string As(string role) => new NpgsqlConnectionStringBuilder(_pg.GetConnectionString()) { Username = role, Password = "pw" }.ConnectionString;

    IHost Build(string cs, AutoCreate autoCreate) =>
        Host.CreateDefaultBuilder().UseWolverine(o =>
        {
            o.PersistMessagesWithPostgresql(cs, Schema);
            o.AutoBuildMessageStorageOnStartup = autoCreate;
            o.UseRabbitMq(new Uri(_mq.GetConnectionString())).AutoProvision();
            o.PublishMessage<SpikeItemCreated>().ToRabbitQueue(Queue).UseDurableOutbox();
            o.ListenToRabbitQueue(Queue);
            o.Discovery.DisableConventionalDiscovery().IncludeType(typeof(SpikeItemCreatedHandler));
        }).Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(), _mq.StartAsync());
        // Thu nhỏ của bootstrap (spec §4.2–4.3): migrator chủ schema, app chỉ DML qua default privileges.
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

        _app = Build(As($"{Schema}_app"), AutoCreate.None);

        // DDL của Wolverine sinh offline từ cấu hình (host chưa start) → đây là nội dung file migration DbUp.
        WolverineDdl = ((Weasel.Core.Migrations.IDatabase)_app.Services.GetRequiredService<IMessageStore>()).ToDatabaseScript();

        // "Migration" chạy bằng migrator: bảng nghiệp vụ + bảng Wolverine.
        await using (var c = new NpgsqlConnection(As($"{Schema}_migrator")))
        {
            await c.ExecuteAsync("CREATE TABLE item (id uuid PRIMARY KEY, name text NOT NULL)");
            await c.ExecuteAsync(WolverineDdl);
        }

        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        _app.Dispose();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    /// <summary>Ghi nghiệp vụ bằng Dapper + publish qua Wolverine trong cùng một transaction Npgsql.</summary>
    async Task WriteAndPublish(Guid id, bool commit)
    {
        var bus = (MessageContext)_app.Services.GetRequiredService<IMessageBus>();
        var store = (IMessageDatabase)_app.Services.GetRequiredService<IMessageStore>();

        await using var conn = new NpgsqlConnection(As($"{Schema}_app"));
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await bus.EnlistInOutboxAsync(new DatabaseEnvelopeTransaction(store, tx));

        await conn.ExecuteAsync("INSERT INTO item (id, name) VALUES (@id, 'rau cải')", new { id }, tx);
        await bus.PublishAsync(new SpikeItemCreated(id));

        if (commit)
        {
            await tx.CommitAsync();
            await bus.FlushOutgoingMessagesAsync(); // sau commit mới đẩy ra RabbitMQ
        }
        else await tx.RollbackAsync();
    }

    async Task<long> Count(string sql)
    {
        await using var c = new NpgsqlConnection(As($"{Schema}_app"));
        return await c.ExecuteScalarAsync<long>(sql);
    }

    static async Task WaitUntil(Func<bool> done, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (!done() && DateTime.UtcNow < until) await Task.Delay(100);
    }

    [Fact(DisplayName = "spike #188: commit → dữ liệu + message ra RabbitMQ đúng một lần, outbox dọn sạch")]
    public async Task commit_delivers_once()
    {
        var id = Guid.CreateVersion7();
        await WriteAndPublish(id, commit: true);

        await WaitUntil(() => SpikeItemCreatedHandler.Received.Contains(id), TimeSpan.FromSeconds(30));
        await Task.Delay(2000, TestContext.Current.CancellationToken); // chờ thêm để bắt bản gửi trùng nếu có
        Assert.Equal(1, SpikeItemCreatedHandler.Received.Count(x => x == id));
        Assert.Equal(1, await Count($"SELECT count(*) FROM item WHERE id = '{id}'"));
        Assert.Equal(0, await Count("SELECT count(*) FROM wolverine_outgoing_envelopes"));
    }

    [Fact(DisplayName = "spike #188: DDL Wolverine sinh offline, nằm trong schema service, áp dụng được bằng migrator")]
    public void ddl_is_schema_scoped()
    {
        Assert.Contains($"{Schema}.wolverine_outgoing_envelopes", WolverineDdl);
        Assert.Contains($"{Schema}.wolverine_incoming_envelopes", WolverineDdl);
        Assert.DoesNotContain("public.", WolverineDdl);
    }

    [Fact(DisplayName = "spike #188: rollback → không có dữ liệu, không có message, không còn gì trong outbox")]
    public async Task rollback_discards_message()
    {
        var id = Guid.CreateVersion7();
        await WriteAndPublish(id, commit: false);

        await Task.Delay(3000, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(id, SpikeItemCreatedHandler.Received);
        Assert.Equal(0, await Count($"SELECT count(*) FROM item WHERE id = '{id}'"));
        Assert.Equal(0, await Count("SELECT count(*) FROM wolverine_outgoing_envelopes"));
    }
}
