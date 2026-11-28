using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine.Persistence.Durability;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Api thật trên PostgreSQL + RabbitMQ tạm: health check, chế độ --migrate, bảng Wolverine khớp cấu hình.</summary>
[Collection(MigrateCollection.Name)]
public sealed class ApiHostTests : IAsyncLifetime
{
    IdentityHost _host = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await IdentityHost.StartAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Theory(DisplayName = "Api trả 200 ở /health/live và /health/ready khi PostgreSQL sẵn sàng")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task health_endpoints_ok(string path) =>
        Assert.Equal(HttpStatusCode.OK, (await _host.CreateApi().CreateClient().GetAsync(path, Ct)).StatusCode);

    [Fact(DisplayName = "--migrate (entry point) chạy lại sau khi đã migrate vẫn thoát mã 0")]
    public void migrate_flag_exits_zero()
    {
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, _host.Connection);
        try
        {
            Assert.Equal(0, (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
    }

    [Fact(DisplayName = "Assembly Api nhúng script của identity (scripts/migrations/*.sql)")]
    public void api_embeds_migrations() =>
        Assert.Contains("scripts/migrations/0001_account.sql", typeof(Program).Assembly.GetManifestResourceNames());

    [Fact(DisplayName = "Bảng Wolverine do migration 0011_wolverine.sql tạo khớp cấu hình Wolverine hiện tại")]
    public async Task wolverine_tables_match_configuration()
    {
        var store = (Weasel.Core.Migrations.IDatabase)_host.CreateApi().Services.GetRequiredService<IMessageStore>();
        await store.AssertDatabaseMatchesConfigurationAsync(Ct);
    }

    [Fact(DisplayName = "Bảng account: kind chỉ customer|staff, email/username duy nhất, phải có một trong hai")]
    public async Task account_table_constraints()
    {
        const string insert = "INSERT INTO identity.account (id, kind, email, username, password_hash) VALUES (gen_random_uuid(), ";
        await _host.Exec(insert + "'staff', 'an@minibox.vn', 'kho.an', '$argon2id$x')");
        await _host.Exec(insert + "'customer', 'binh@minibox.vn', NULL, NULL)");
        await Assert.ThrowsAsync<PostgresException>(() => _host.Exec(insert + "'admin', 'c@minibox.vn', NULL, NULL)"));
        await Assert.ThrowsAsync<PostgresException>(() => _host.Exec(insert + "'customer', 'an@minibox.vn', NULL, NULL)"));
        await Assert.ThrowsAsync<PostgresException>(() => _host.Exec(insert + "'customer', NULL, 'kho.an', NULL)"));
        await Assert.ThrowsAsync<PostgresException>(() => _host.Exec(insert + "'customer', NULL, NULL, NULL)"));
    }
}
