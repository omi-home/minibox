using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using JasperFx;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Domain;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

namespace Minibox.BuildingBlocks.Web.Tests;

public sealed record ItemCreated(Guid ItemId) : IDomainEvent;

public sealed class Item : AggregateRoot<Guid>
{
    Item(Guid id, string name) : base(id) => Name = name;

    public string Name { get; private set; }

    public static Item Create(string name)
    {
        var item = new Item(Guid.CreateVersion7(), name);
        item.Raise(new ItemCreated(item.Id));
        return item;
    }

    public static Item Load(Guid id, string name) => new(id, name);
}

public sealed class WebTestDb(DbContextOptions<WebTestDb> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Item>().IsAggregateRoot();
        b.MapWolverineEnvelopeStorage(WebPipelineTests.Schema);
        b.UseSnakeCaseNames();
    }
}

public sealed class ItemRepository(DbContext db) : EfRepository<Item, Guid>(db);

public sealed record CreateItem(string Name, string IdempotencyKey) : ICommand<Guid>, IIdempotentCommand;

public sealed class CreateItemValidator : AbstractValidator<CreateItem>
{
    public CreateItemValidator() => RuleFor(x => x.Name).NotEmpty();
}

public sealed class CreateItemHandler(ItemRepository items) : ICommandHandler<CreateItem, Guid>
{
    public async ValueTask<Result<Guid>> Handle(CreateItem command, CancellationToken cancellationToken)
    {
        var item = Item.Create(command.Name);
        await items.AddAsync(item, cancellationToken);
        await Task.Delay(200, cancellationToken); // nới cửa sổ để hai request song song chồng nhau
        return item.Id;
    }
}

public sealed record CreateItemRequest(string Name);

sealed class AllowAll : IAuthorizer
{
    public ValueTask<bool> HasPermissionAsync(string permission, string scope, CancellationToken cancellationToken = default) => ValueTask.FromResult(true);
}

/// <summary>Endpoint thật → Mediator pipeline → EfUnitOfWork/PostgresIdempotencyStore trên PostgreSQL thật (role app chỉ DML).</summary>
public sealed class WebPipelineTests : IAsyncLifetime
{
    public const string Schema = "webtest";
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    WebApplication _app = null!;
    HttpClient _http = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Mật khẩu "pw" chỉ dùng cho container tạm của test.
    string As(string role) => new NpgsqlConnectionStringBuilder(_pg.GetConnectionString()) { Username = role, Password = "pw" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync(Ct);
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

        var appCs = As($"{Schema}_app");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Host.UseWolverine(o =>
        {
            o.PersistMessagesWithPostgresql(appCs, Schema);
            o.AutoBuildMessageStorageOnStartup = AutoCreate.None;
            o.UseEntityFrameworkCoreTransactions();
            o.Discovery.DisableConventionalDiscovery();
        });
        builder.Services
            .AddMiniboxWeb()
            .AddSingleton(NpgsqlDataSource.Create(appCs))
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IAuthorizer, AllowAll>()
            .AddMiniboxPersistence<WebTestDb>(appCs)
            .AddScoped<ItemRepository>()
            .AddSingleton<IValidator<CreateItem>, CreateItemValidator>()
            .AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.PipelineBehaviors =
                [
                    typeof(LoggingBehavior<,>),
                    typeof(ValidationBehavior<,>),
                    typeof(AuthorizationBehavior<,>),
                    typeof(TransactionBehavior<,>),
                    typeof(IdempotencyBehavior<,>),
                ];
            });

        _app = builder.Build();
        _app.UseMiniboxWeb();
        _app.MapMiniboxHealthChecks();
        _app.MapPost("/items", async (CreateItemRequest request, [FromHeader(Name = IdempotencyKey.HeaderName)] string key, Mediator.ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreateItem(request.Name, key), ct)).ToHttpResult());

        var wolverineDdl = ((Weasel.Core.Migrations.IDatabase)_app.Services.GetRequiredService<IMessageStore>()).ToDatabaseScript();
        var migrator = As($"{Schema}_migrator");
        await Exec(migrator, "CREATE TABLE item (id uuid PRIMARY KEY, name text NOT NULL, version bigint NOT NULL DEFAULT 0)");
        foreach (var template in new[] { "audit-log.sql", "idempotency-key.sql" })
            await Exec(migrator, (await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "database", "templates", template), Ct)).Replace("{{svc}}", Schema));
        await Exec(migrator, wolverineDdl);

        await _app.StartAsync(Ct);
        _http = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        await _pg.DisposeAsync();
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "database", "templates"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc repo");
    }

    static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    async Task<long> Count(string sql)
    {
        await using var c = new NpgsqlConnection(As($"{Schema}_app"));
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(sql, c);
        return (long)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    Task<HttpResponseMessage> Post(string name, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/items") { Content = JsonContent.Create(new CreateItemRequest(name)) };
        if (key is not null) request.Headers.Add(IdempotencyKey.HeaderName, key);
        return _http.SendAsync(request, Ct);
    }

    static async Task<JsonElement> Problem(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    static string NewKey() => Guid.NewGuid().ToString("N");

    [Fact(DisplayName = "ac187_1: command lỗi validation trả ProblemDetails 400 và không ghi gì vào DB")]
    public async Task ac187_1_validation_error_is_400_problem_and_writes_nothing()
    {
        var response = await Post("", NewKey());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Problem(response);
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(ValidationBehavior<CreateItem, Result<Guid>>.ErrorCode, problem.GetProperty("code").GetString());
        Assert.Equal(0, await Count("SELECT count(*) FROM item"));
        Assert.Equal(0, await Count("SELECT count(*) FROM audit_log"));
        Assert.Equal(0, await Count("SELECT count(*) FROM idempotency_key"));
    }

    [Fact(DisplayName = "ac187_5: gửi lặp cùng Idempotency-Key trả lại kết quả lần đầu, không tạo bản ghi thứ hai")]
    public async Task ac187_5_repeat_returns_first_result()
    {
        var key = NewKey();
        var first = await Post("rau muống", key);
        var second = await Post("rau muống", key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(Ct), await second.Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, await Count("SELECT count(*) FROM item WHERE name = 'rau muống'"));
    }

    [Fact(DisplayName = "ac187_5: hai request cùng Idempotency-Key chạy song song chỉ tạo một bản ghi, cùng kết quả")]
    public async Task ac187_5_concurrent_repeat_creates_one_record()
    {
        var key = NewKey();
        var responses = await Task.WhenAll(Post("cải ngọt", key), Post("cải ngọt", key));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(Ct), await responses[1].Content.ReadAsStringAsync(Ct));
        Assert.Equal(1, await Count("SELECT count(*) FROM item WHERE name = 'cải ngọt'"));
    }

    [Fact(DisplayName = "Cùng Idempotency-Key khác nội dung → 409, không ghi thêm")]
    public async Task same_key_different_payload_is_409()
    {
        var key = NewKey();
        await Post("bắp cải", key);
        var reused = await Post("súp lơ", key);

        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Equal(IdempotencyBehavior<CreateItem, Result<Guid>>.KeyReusedCode, (await Problem(reused)).GetProperty("code").GetString());
        Assert.Equal(0, await Count("SELECT count(*) FROM item WHERE name = 'súp lơ'"));
    }

    [Theory(DisplayName = "Request ghi thiếu/sai Idempotency-Key → 400 ProblemDetails trước khi vào endpoint")]
    [InlineData(null)]
    [InlineData("short")]
    public async Task missing_or_invalid_key_is_400(string? key)
    {
        var response = await Post("cà rốt", key);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(IdempotencyKey.InvalidKeyCode, (await Problem(response)).GetProperty("code").GetString());
        Assert.Equal(0, await Count("SELECT count(*) FROM item WHERE name = 'cà rốt'"));
    }

    [Fact(DisplayName = "Health: /health/live và /health/ready (PostgreSQL) trả 200")]
    public async Task health_checks()
    {
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/health/live", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/health/ready", Ct)).StatusCode);
    }

    [Theory(DisplayName = "ErrorType → mã HTTP của ProblemDetails")]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Failure, 422)]
    [InlineData(ErrorType.Unauthorized, 401)]
    public void error_type_maps_to_status(ErrorType type, int status) => Assert.Equal(status, ResultHttpExtensions.StatusCode(type));
}
