using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.ServiceName.Api.Endpoints.Samples;
using Minibox.ServiceName.Application.Abstractions;
using Minibox.ServiceName.Application.Features.Samples.Queries.ListSamples;
using Minibox.ServiceName.Domain.Aggregates.Samples;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Wolverine.Persistence.Durability;

namespace Minibox.ServiceName.IntegrationTests;

/// <summary>Api thật trên PostgreSQL + RabbitMQ tạm: --migrate, health, pipeline Mediator đủ 5 behavior qua endpoint, mapping EF khớp DDL.</summary>
public sealed class ApiHostTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    readonly FakeAuthorizer _authorizer = new();
    WebApplicationFactory<Program> _factory = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role servicename_app/servicename_migrator.
    // search_path như role migrator thật để migration không ghi schema rơi vào servicename.
    string Connection => _pg.GetConnectionString() + ";Search Path=servicename";

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(Ct), _mq.StartAsync(Ct));
        await using (var c = new NpgsqlConnection(_pg.GetConnectionString()))
        {
            await c.OpenAsync(Ct);
            await using var cmd = new NpgsqlCommand("CREATE SCHEMA servicename; CREATE ROLE servicename_app", c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        Assert.Equal(0, Migrate());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:App", Connection)
            .UseSetting("ConnectionStrings:RabbitMq", _mq.GetConnectionString())
            .ConfigureTestServices(s => s.AddSingleton<IAuthorizer>(_authorizer)));
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

    static HttpRequestMessage Post(string name, string key) => new(HttpMethod.Post, "/samples")
    {
        Content = JsonContent.Create(new CreateSampleRequest(name)),
        Headers = { { IdempotencyKey.HeaderName, key } },
    };

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

    [Fact(DisplayName = "ac455_1 POST /samples qua pipeline Mediator: tạo, gửi lặp cùng Idempotency-Key trả cùng Id, GET /samples đọc lại")]
    public async Task ac455_1_create_and_list_through_pipeline()
    {
        _authorizer.Granted = ["servicename.sample.create", "servicename.sample.read"];
        var http = _factory.CreateClient();
        var key = Guid.NewGuid().ToString();

        var first = await http.SendAsync(Post("mẫu", key), Ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var id = await first.Content.ReadFromJsonAsync<Guid>(Ct);
        var replay = await http.SendAsync(Post("mẫu", key), Ct);
        Assert.Equal(id, await replay.Content.ReadFromJsonAsync<Guid>(Ct));

        Assert.Equal([new SampleDto(id, "mẫu")], (await http.GetFromJsonAsync<SampleDto[]>("/samples", Ct))!);
    }

    [Fact(DisplayName = "ac455_1 Pipeline chặn: thiếu quyền 403, tên rỗng 400, cùng khoá khác nội dung 409; không ghi gì")]
    public async Task ac455_1_pipeline_rejects()
    {
        var http = _factory.CreateClient();
        _authorizer.Granted = [];
        Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(Post("mẫu", Guid.NewGuid().ToString()), Ct)).StatusCode);

        _authorizer.Granted = ["servicename.sample.create", "servicename.sample.read"];
        Assert.Equal(HttpStatusCode.BadRequest, (await http.SendAsync(Post("", Guid.NewGuid().ToString()), Ct)).StatusCode);
        var key = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(Post("a", key), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.SendAsync(Post("b", key), Ct)).StatusCode);

        Assert.Equal(["a"], (await http.GetFromJsonAsync<SampleDto[]>("/samples", Ct))!.Select(s => s.Name));
    }

    [Fact(DisplayName = "Mapping EF khớp DDL đã migrate: repository ghi, cập nhật theo version, read service LINQ đọc lại")]
    public async Task ef_mapping_matches_migrated_schema()
    {
        var sample = Sample.Create("mẫu").Value;
        await using (var scope = _factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IRepository<Sample, Guid>>().AddAsync(sample, Ct);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<Sample, Guid>>();
            var loaded = await repo.GetAsync(sample.Id, Ct);
            Assert.NotNull(loaded);
            Assert.True((await repo.UpdateAsync(loaded, 0, Ct)).IsSuccess);
            Assert.False((await repo.UpdateAsync(loaded, 0, Ct)).IsSuccess); // version đã lên 1
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
            Assert.Equal([new SampleDto(sample.Id, "mẫu")], await scope.ServiceProvider.GetRequiredService<ISampleReadService>().ListAsync(Ct));
    }

    sealed class FakeAuthorizer : IAuthorizer
    {
        public HashSet<string> Granted { get; set; } = [];

        public ValueTask<bool> HasPermissionAsync(string permission, string scope, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Granted.Contains(permission));
    }
}
