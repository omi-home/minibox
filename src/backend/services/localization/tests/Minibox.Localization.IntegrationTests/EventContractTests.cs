using System.Text;
using System.Text.Json;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Messaging;
using Minibox.BuildingBlocks.Infrastructure.Migrations;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Contracts.Events;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Aggregates.Messages;
using Minibox.Localization.Infrastructure;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Minibox.Localization.IntegrationTests;

/// <summary>Xuất bản chuỗi và sửa locale qua Unit of Work: <c>localization.MessagesPublished.v1</c> và <c>localization.LocaleChanged.v1</c>
/// đi qua outbox ra RabbitMQ thật và khớp JSON Schema trong <c>contracts/events</c>.</summary>
public sealed class EventContractTests : IAsyncLifetime
{
    const string PublishedQueue = "localization-messages-published-capture";
    const string LocaleQueue = "localization-locale-changed-capture";
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();
    IHost _app = null!;
    string _cs = null!;

    sealed class SystemUser : ICurrentUser
    {
        public string? UserId => "system";
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    static JsonSchema Schema(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "contracts", "events", name)))
                return JsonSchema.FromFile(Path.Combine(dir.FullName, "contracts", "events", name));
        throw new FileNotFoundException(name);
    }

    static readonly JsonSchema PublishedSchema = Schema("localization.MessagesPublished.v1.json");
    static readonly JsonSchema LocaleSchema = Schema("localization.LocaleChanged.v1.json");

    static void AssertValid(JsonSchema schema, string json)
    {
        var result = schema.Evaluate(JsonDocument.Parse(json).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(result.IsValid, json + "\n" + string.Join("\n", (result.Details ?? []).Where(d => d.Errors is not null)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))));
    }

    static bool Valid(JsonSchema schema, string json) => schema.Evaluate(JsonDocument.Parse(json).RootElement).IsValid;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role localization_app.
        _cs = _pg.GetConnectionString() + ";Search Path=localization";
        await using (var c = new NpgsqlConnection(_cs))
        {
            await c.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("CREATE SCHEMA localization; CREATE ROLE localization_app", c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal(0, DbMigrator.RunIfRequested(["--migrate"], "localization", typeof(Program).Assembly, () => _cs));

        _app = Host.CreateDefaultBuilder()
            .UseEnvironment("Development")
            .UseWolverine(o =>
            {
                o.UseMiniboxMessaging(_cs, "localization", new Uri(_mq.GetConnectionString()));
                o.PublishMessage<MessagesPublishedV1>().ToRabbitQueue(PublishedQueue);
                o.PublishMessage<LocaleChangedV1>().ToRabbitQueue(LocaleQueue);
                o.Discovery.DisableConventionalDiscovery();
            })
            .ConfigureServices(s => s
                .AddSingleton(NpgsqlDataSource.Create(_cs))
                .AddSingleton<ICurrentUser, SystemUser>()
                .AddSingleton(TimeProvider.System)
                .AddLocalizationInfrastructure(_cs))
            .Build();
        await _app.StartAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        _app.Dispose();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }

    async Task InUnitOfWork(Func<IServiceProvider, Task> work)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await uow.BeginAsync(Ct);
        await work(scope.ServiceProvider);
        await uow.CommitAsync(Ct);
    }

    async Task<string?> NextMessage(string queue)
    {
        await using var conn = await new ConnectionFactory { Uri = new Uri(_mq.GetConnectionString()) }.CreateConnectionAsync(Ct);
        await using var channel = await conn.CreateChannelAsync(cancellationToken: Ct);
        for (var until = DateTime.UtcNow.AddSeconds(15); DateTime.UtcNow < until; await Task.Delay(100, Ct))
            if (await channel.BasicGetAsync(queue, autoAck: true, Ct) is { } msg)
                return Encoding.UTF8.GetString(msg.Body.Span);
        return null;
    }

    [Fact(DisplayName = "Xuất bản chuỗi phát MessagesPublished qua outbox; message trên RabbitMQ khớp JSON Schema v1, không mang nội dung chuỗi")]
    public async Task messages_published_matches_schema()
    {
        var message = UiMessage.Create("shop", "cart.title", "en", "Secret cart text", DateTimeOffset.UtcNow).Value;
        await InUnitOfWork(sp => sp.GetRequiredService<IUiMessageRepository>().AddAsync(message, Ct));
        await InUnitOfWork(async sp =>
        {
            var repo = sp.GetRequiredService<IUiMessageRepository>();
            var pending = (await repo.ListPendingAsync("shop", "en", Ct)).Single();
            pending.Publish(DateTimeOffset.UtcNow);
            Assert.True((await repo.UpdateAsync(pending, pending.Version, Ct)).IsSuccess);
        });

        var json = await NextMessage(PublishedQueue);
        Assert.NotNull(json);
        AssertValid(PublishedSchema, json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("en", doc.RootElement.GetProperty("locale").GetString());
        Assert.Equal(["cart.title"], doc.RootElement.GetProperty("keys").EnumerateArray().Select(k => k.GetString()));
        Assert.DoesNotContain("Secret", json);
    }

    [Fact(DisplayName = "Tạo locale phát LocaleChanged qua outbox; message trên RabbitMQ khớp JSON Schema v1")]
    public async Task locale_changed_matches_schema()
    {
        await InUnitOfWork(sp => sp.GetRequiredService<ILocaleRepository>().AddAsync(Locale.Create("ja", "日本語", 2, "en").Value, Ct));

        var json = await NextMessage(LocaleQueue);
        Assert.NotNull(json);
        AssertValid(LocaleSchema, json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ja", doc.RootElement.GetProperty("code").GetString());
        Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal("en", doc.RootElement.GetProperty("fallbackCode").GetString());
    }

    [Fact(DisplayName = "Schema v1 chặn message lệch: thiếu trường, keys rỗng, thêm trường lạ, mã locale sai")]
    public void schemas_reject_drift()
    {
        Assert.False(Valid(PublishedSchema, """{"locale":"en","namespace":"shop","keys":[]}"""));
        Assert.False(Valid(PublishedSchema, """{"locale":"EN","namespace":"shop","keys":["a"]}"""));
        Assert.False(Valid(PublishedSchema, """{"locale":"en","namespace":"shop","keys":["a"],"text":"x"}"""));
        Assert.False(Valid(LocaleSchema, """{"code":"ja","name":"x","sortOrder":1}"""));
        Assert.True(Valid(LocaleSchema, """{"code":"ja","name":"x","enabled":true,"sortOrder":1,"fallbackCode":null}"""));
    }
}
