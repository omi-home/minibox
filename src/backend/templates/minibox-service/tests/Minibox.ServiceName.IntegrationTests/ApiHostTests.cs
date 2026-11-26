using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.ServiceName.Application.Samples;
using Minibox.ServiceName.Domain.Samples;
using Minibox.ServiceName.Infrastructure.Persistence;
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

    // Superuser của container tạm; search_path như role servicename_migrator thật để migration không schema rơi vào servicename.
    string MigratorConnection => _pg.GetConnectionString() + ";Search Path=servicename";

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

    [Fact(DisplayName = "--migrate áp migration rồi thoát với mã 0")]
    public void migrate_flag_exits_zero() => Assert.Equal(0, Migrate());

    [Fact(DisplayName = "Sau --migrate, mapping EF khớp DDL: repository ghi, read service LINQ đọc lại")]
    public async Task ef_mapping_matches_migrated_schema()
    {
        Assert.Equal(0, Migrate());
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:App", _pg.GetConnectionString()));
        var sample = Sample.Create("mẫu");
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SampleRepository>().AddAsync(sample, Ct);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<SampleRepository>();
            var loaded = await repo.GetAsync(sample.Id, Ct);
            Assert.NotNull(loaded);
            Assert.True((await repo.UpdateAsync(loaded, 0, Ct)).IsSuccess);
            Assert.False((await repo.UpdateAsync(loaded, 0, Ct)).IsSuccess); // version đã lên 1
        }

        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal([new SampleDto(sample.Id, "mẫu")], await scope.ServiceProvider.GetRequiredService<ISampleReadService>().ListAsync(Ct));
    }
}
