using Mediator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Infrastructure.Migrations;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Application.Features.Sessions;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Minibox.Identity.IntegrationTests;

/// <summary>PostgreSQL + RabbitMQ tạm đã migrate schema identity, và Api thật (WebApplicationFactory) trên đó.</summary>
/// <remarks>Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role identity_app/identity_migrator.
/// Migrate gọi <see cref="DbMigrator"/> trực tiếp (không qua biến môi trường) nên các class test chạy song song được.</remarks>
public sealed class IdentityHost : IAsyncDisposable
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly RabbitMqContainer _mq = new RabbitMqBuilder("rabbitmq:4-alpine").Build();
    readonly List<WebApplicationFactory<Program>> _apis = [];

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Kết nối superuser, search_path = identity.</summary>
    public NpgsqlDataSource Db { get; private set; } = null!;

    /// <summary>Chuỗi kết nối (search_path như role migrator thật).</summary>
    public string Connection => _pg.GetConnectionString() + ";Search Path=identity";

    /// <summary>URI RabbitMQ.</summary>
    public string RabbitMq => _mq.GetConnectionString();

    /// <summary>Khởi động container, tạo schema + role app (thay bootstrap) và migrate.</summary>
    /// <returns>Host sẵn sàng.</returns>
    public static async Task<IdentityHost> StartAsync()
    {
        var host = new IdentityHost();
        await Task.WhenAll(host._pg.StartAsync(Ct), host._mq.StartAsync(Ct));
        host.Db = NpgsqlDataSource.Create(host.Connection);
        await host.Exec("CREATE SCHEMA identity; CREATE ROLE identity_app");
        Assert.Equal(0, host.Migrate());
        return host;
    }

    /// <summary>Áp migration của identity.</summary>
    /// <returns>Mã thoát của job migrate (0 = thành công).</returns>
    public int Migrate() => DbMigrator.RunIfRequested(["--migrate"], "identity", typeof(Program).Assembly, () => Connection) ?? -1;

    /// <summary>Tạo Api thật trỏ vào container.</summary>
    /// <param name="services">Thay service cho test (đồng hồ, người gọi…).</param>
    /// <returns>Factory; host dọn khi dispose.</returns>
    public WebApplicationFactory<Program> CreateApi(Action<IServiceCollection>? services = null)
    {
        var api = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:App", Connection)
            .UseSetting("ConnectionStrings:RabbitMq", RabbitMq)
            .UseSetting("Jwt:Issuer", "http://localhost")
            .UseSetting("Mfa:EncryptionKey", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)))
            .ConfigureTestServices(s => services?.Invoke(s)));
        _apis.Add(api);
        return api;
    }

    /// <summary>Gửi command/query qua pipeline Mediator thật (không qua HTTP).</summary>
    /// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
    /// <param name="api">Api.</param>
    /// <param name="message">Command hoặc query.</param>
    /// <returns>Kết quả.</returns>
    public static async Task<TResponse> Send<TResponse>(WebApplicationFactory<Program> api, IMessage message)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return (TResponse)(await scope.ServiceProvider.GetRequiredService<ISender>().Send((object)message, Ct))!;
    }

    /// <summary>Mở phiên trực tiếp (như sau đăng nhập) cho tài khoản đã có.</summary>
    /// <param name="api">Api.</param>
    /// <param name="accountId">Tài khoản.</param>
    /// <param name="amr">Phương thức xác thực.</param>
    /// <returns>Cặp token.</returns>
    public static async Task<TokenResponse> OpenSession(WebApplicationFactory<Program> api, Guid accountId, params string[] amr)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountRepository>().GetAsync(accountId, Ct);
        return await scope.ServiceProvider.GetRequiredService<SessionIssuer>().OpenAsync(account!, amr.Length == 0 ? ["pwd"] : amr, Ct);
    }

    /// <summary>Chạy SQL.</summary>
    /// <param name="sql">Câu lệnh.</param>
    /// <returns>Task hoàn tất khi chạy xong.</returns>
    public async Task Exec(string sql)
    {
        await using var cmd = Db.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>Đọc một giá trị.</summary>
    /// <typeparam name="T">Kiểu giá trị.</typeparam>
    /// <param name="sql">Câu truy vấn.</param>
    /// <returns>Giá trị cột đầu dòng đầu.</returns>
    public async Task<T> Scalar<T>(string sql)
    {
        await using var cmd = Db.CreateCommand(sql);
        return (T)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    /// <summary>Đọc cột chuỗi đầu tiên của mọi dòng.</summary>
    /// <param name="sql">Câu truy vấn.</param>
    /// <returns>Danh sách giá trị.</returns>
    public async Task<List<string>> Strings(string sql)
    {
        await using var cmd = Db.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(Ct);
        var result = new List<string>();
        while (await reader.ReadAsync(Ct)) result.Add(reader.GetString(0));
        return result;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var api in _apis) await api.DisposeAsync();
        if (Db is not null) await Db.DisposeAsync();
        await Task.WhenAll(_pg.DisposeAsync().AsTask(), _mq.DisposeAsync().AsTask());
    }
}

/// <summary>Đồng hồ chỉnh tay.</summary>
/// <param name="now">Thời điểm ban đầu.</param>
public sealed class FakeTime(DateTimeOffset now) : TimeProvider
{
    /// <summary>Thời điểm hiện tại.</summary>
    public DateTimeOffset Now { get; set; } = now;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Người gọi giả cho command gửi thẳng qua Mediator (không có HttpContext).</summary>
public sealed class TestUser : ICurrentUser
{
    /// <inheritdoc/>
    public string? UserId { get; set; }
}

/// <summary>Test gọi entry point với cờ --migrate đặt biến môi trường toàn process: chạy tuần tự với nhau.</summary>
[CollectionDefinition(Name)]
public sealed class MigrateCollection
{
    /// <summary>Tên collection.</summary>
    public const string Name = "migrate";
}
