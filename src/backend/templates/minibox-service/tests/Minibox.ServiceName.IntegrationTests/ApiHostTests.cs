using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.ServiceName.IntegrationTests;

/// <summary>Api thật trên PostgreSQL tạm: health check và chế độ --migrate.</summary>
public sealed class ApiHostTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync(Ct);
        await using var c = new NpgsqlConnection(_pg.GetConnectionString());
        await c.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand("CREATE SCHEMA servicename", c);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    public ValueTask DisposeAsync() => _pg.DisposeAsync();

    [Theory(DisplayName = "Api trả 200 ở /health/live và /health/ready khi PostgreSQL sẵn sàng")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task health_endpoints_ok(string path)
    {
        // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role servicename_app.
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:App", _pg.GetConnectionString()));
        var response = await factory.CreateClient().GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "--migrate áp migration rồi thoát với mã 0")]
    public void migrate_flag_exits_zero()
    {
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, _pg.GetConnectionString());
        try
        {
            var code = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }]);
            Assert.Equal(0, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
    }
}
