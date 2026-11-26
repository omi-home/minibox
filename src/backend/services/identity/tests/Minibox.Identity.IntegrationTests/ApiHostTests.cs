using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Api thật trên PostgreSQL tạm: health check và chế độ --migrate.</summary>
[Collection(MigrateCollection.Name)]
public sealed class ApiHostTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync(Ct);
        await using var c = new NpgsqlConnection(_pg.GetConnectionString());
        await c.OpenAsync(Ct);
        // Thay cho bootstrap: schema + role app (migration REVOKE quyền của nó trên bảng chỉ ghi thêm).
        await using var cmd = new NpgsqlCommand("CREATE SCHEMA identity; CREATE ROLE identity_app", c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    public ValueTask DisposeAsync() => _pg.DisposeAsync();

    // Môi trường thật: search_path của identity_migrator do bootstrap đặt là schema identity.
    string MigratorConnection => _pg.GetConnectionString() + ";Search Path=identity";

    [Theory(DisplayName = "Api trả 200 ở /health/live và /health/ready khi PostgreSQL sẵn sàng")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task health_endpoints_ok(string path)
    {
        // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role identity_app.
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:App", _pg.GetConnectionString()));
        var response = await factory.CreateClient().GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "--migrate áp migration rồi thoát với mã 0")]
    public void migrate_flag_exits_zero()
    {
        Assert.Equal(0, Migrate());
        Assert.Equal(0, Migrate()); // chạy lại không đổi gì
    }

    [Fact(DisplayName = "Assembly Api nhúng migration của identity (migrations/*.sql)")]
    public void api_embeds_migrations() =>
        Assert.Contains("migrations/0001_account.sql", typeof(Program).Assembly.GetManifestResourceNames());

    [Fact(DisplayName = "Bảng account: kind chỉ customer|staff, email/username duy nhất, phải có một trong hai")]
    public async Task account_table_constraints()
    {
        Assert.Equal(0, Migrate());
        await using var c = new NpgsqlConnection(MigratorConnection);
        await c.OpenAsync(Ct);

        async Task Exec(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, c);
            await cmd.ExecuteNonQueryAsync(Ct);
        }
        const string insert = "INSERT INTO identity.account (id, kind, email, username, password_hash) VALUES (gen_random_uuid(), ";

        await Exec(insert + "'staff', 'an@minibox.vn', 'kho.an', '$argon2id$x')");
        await Exec(insert + "'customer', 'binh@minibox.vn', NULL, NULL)");
        await Assert.ThrowsAsync<PostgresException>(() => Exec(insert + "'admin', 'c@minibox.vn', NULL, NULL)"));
        await Assert.ThrowsAsync<PostgresException>(() => Exec(insert + "'customer', 'an@minibox.vn', NULL, NULL)"));
        await Assert.ThrowsAsync<PostgresException>(() => Exec(insert + "'customer', NULL, 'kho.an', NULL)"));
        await Assert.ThrowsAsync<PostgresException>(() => Exec(insert + "'customer', NULL, NULL, NULL)"));
    }

    int Migrate()
    {
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, MigratorConnection);
        try
        {
            return (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!;
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
    }
}

/// <summary>Test gọi --migrate đặt biến môi trường toàn process: chạy tuần tự với nhau.</summary>
[CollectionDefinition(Name)]
public sealed class MigrateCollection
{
    /// <summary>Tên collection.</summary>
    public const string Name = "migrate";
}
