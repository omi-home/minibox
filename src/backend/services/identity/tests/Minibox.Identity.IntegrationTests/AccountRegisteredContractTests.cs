using System.Text;
using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Messaging;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Contracts.Events;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure;
using Minibox.Identity.Infrastructure.Accounts;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.RabbitMQ;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Tạo tài khoản qua Unit of Work: message <c>identity.AccountRegistered.v1</c> đi qua outbox ra RabbitMQ thật và khớp JSON Schema v1 (spec §6.3).</summary>
[Collection(MigrateCollection.Name)]
public sealed class AccountRegisteredContractTests : IAsyncLifetime
{
    const string CaptureQueue = "identity-account-registered-capture"; // không ai nghe: đọc message thô
    static readonly JsonSchema Schema = JsonSchema.FromFile(Path.Combine(RepoRoot(), "contracts", "events", "identity.AccountRegistered.v1.json"));
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    IHost _app = null!;
    string _cs = null!;

    sealed class SystemUser : ICurrentUser
    {
        public string? UserId => "system";
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "contracts", "events")))
                return dir.FullName;
        throw new DirectoryNotFoundException("Không tìm thấy contracts/events");
    }

    static EvaluationResults Evaluate(string json) =>
        Schema.Evaluate(JsonDocument.Parse(json).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role identity_app.
        _cs = _pg.GetConnectionString() + ";Search Path=identity";
        await Exec("CREATE SCHEMA identity; CREATE ROLE identity_app");
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, _cs);
        try
        {
            Assert.Equal(0, (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }

        _app = Host.CreateDefaultBuilder()
            .UseEnvironment("Development")
            .UseWolverine(o =>
            {
                o.UseMiniboxMessaging(_cs, "identity", new Uri(_mq.GetConnectionString()));
                o.PublishMessage<AccountRegisteredV1>().ToRabbitQueue(CaptureQueue);
                o.Discovery.DisableConventionalDiscovery();
            })
            .ConfigureServices(s => s
                .AddSingleton(NpgsqlDataSource.Create(_cs))
                .AddSingleton<ICurrentUser, SystemUser>()
                .AddSingleton(TimeProvider.System)
                .AddIdentityOutbox())
            .Build();
        // ponytail: bảng Wolverine tạo từ DDL của message store; khi host identity bật Wolverine thì chuyển thành migration.
        await Exec(((Weasel.Core.Migrations.IDatabase)_app.Services.GetRequiredService<IMessageStore>()).ToDatabaseScript());
        await _app.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        _app.Dispose();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    async Task Exec(string sql)
    {
        await using var c = new NpgsqlConnection(_cs);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    async Task<object?> Scalar(string sql)
    {
        await using var c = new NpgsqlConnection(_cs);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        return await cmd.ExecuteScalarAsync(Ct);
    }

    async Task<string?> NextMessage(TimeSpan timeout)
    {
        await using var conn = await new ConnectionFactory { Uri = new Uri(_mq.GetConnectionString()) }.CreateConnectionAsync(Ct);
        await using var channel = await conn.CreateChannelAsync(cancellationToken: Ct);
        for (var until = DateTime.UtcNow + timeout; DateTime.UtcNow < until; await Task.Delay(100, Ct))
            if (await channel.BasicGetAsync(CaptureQueue, autoAck: true, Ct) is { } msg)
                return Encoding.UTF8.GetString(msg.Body.Span);
        return null;
    }

    [Theory(DisplayName = "ac212_5 Đăng ký tài khoản qua Unit of Work phát AccountRegistered qua outbox; message trên RabbitMQ khớp JSON Schema v1")]
    [InlineData(AccountKind.Staff, "staff")]
    [InlineData(AccountKind.Customer, "customer")]
    public async Task ac212_5_account_registered_matches_schema(AccountKind kind, string expectedKind)
    {
        var account = Account.Register(kind, $"u{Guid.NewGuid():N}@minibox.vn", null, null).Value;
        await using (var scope = _app.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<NpgsqlUnitOfWork>();
            await uow.BeginAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<AccountRepository>().AddAsync(account, Ct);
            await uow.CommitAsync(Ct);
        }

        var json = await NextMessage(TimeSpan.FromSeconds(15));
        Assert.NotNull(json);
        var result = Evaluate(json);
        Assert.True(result.IsValid, json + "\n" + string.Join("\n", (result.Details ?? []).Where(d => d.Errors is not null)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(account.Id, doc.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(expectedKind, doc.RootElement.GetProperty("kind").GetString());
        Assert.DoesNotContain("minibox.vn", json); // không mang PII
        Assert.Equal(1L, await Scalar($"SELECT count(*) FROM audit_log WHERE action = 'AccountRegistered' AND aggregate_id = '{account.Id}'"));
        Assert.Equal(expectedKind, await Scalar($"SELECT kind FROM account WHERE id = '{account.Id}'"));
    }

    [Fact(DisplayName = "ac212_5 Schema v1 chặn message lệch: thiếu trường, sai kind, thêm trường lạ")]
    public void ac212_5_schema_rejects_drift()
    {
        Assert.False(Evaluate("""{"kind":"staff"}""").IsValid);
        Assert.False(Evaluate($$"""{"accountId":"{{Guid.CreateVersion7()}}","kind":"admin"}""").IsValid);
        Assert.False(Evaluate($$"""{"accountId":"{{Guid.CreateVersion7()}}","kind":"staff","email":"a@b.vn"}""").IsValid);
    }
}
