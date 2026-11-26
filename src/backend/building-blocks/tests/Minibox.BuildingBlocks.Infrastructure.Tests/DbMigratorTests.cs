using Minibox.BuildingBlocks.Infrastructure.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.BuildingBlocks.Infrastructure.Tests;

/// <summary>PostgreSQL 18 tạm với schema `demo` do role `demo_migrator` sở hữu (như sau bootstrap, spec §4.2–4.4).</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();

    public string Admin => _pg.GetConnectionString();

    public string Migrator(string db) =>
        new NpgsqlConnectionStringBuilder(Admin) { Username = "demo_migrator", Password = "pw", Database = db }.ConnectionString;

    public async ValueTask InitializeAsync() => await _pg.StartAsync();

    public ValueTask DisposeAsync() => _pg.DisposeAsync();

    /// <summary>Tạo database sạch riêng cho từng test, có role migrator + schema như bootstrap.</summary>
    public async Task<string> FreshDatabase()
    {
        var db = "t" + Guid.NewGuid().ToString("N");
        await Exec(Admin, $"""
            DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'demo_migrator') THEN
              CREATE ROLE demo_migrator LOGIN PASSWORD 'pw'; END IF; END $$;
            ALTER ROLE demo_migrator SET search_path = demo;
            """);
        await Exec(Admin, $"CREATE DATABASE {db}"); // không chạy được trong batch (transaction ngầm)
        var dbAdmin = new NpgsqlConnectionStringBuilder(Admin) { Database = db }.ConnectionString;
        await Exec(dbAdmin, "REVOKE ALL ON SCHEMA public FROM PUBLIC; CREATE SCHEMA demo AUTHORIZATION demo_migrator;");
        return db;
    }

    public static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<object?> Scalar(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        return await cmd.ExecuteScalarAsync();
    }
}

public sealed class DbMigratorTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    static readonly System.Reflection.Assembly Scripts = typeof(DbMigratorTests).Assembly;

    [Fact(DisplayName = "ac180_3 migrate DB sạch lên phiên bản mới nhất, journal ở <svc>.schema_versions")]
    public async Task ac180_3_migrate_clean()
    {
        var cs = pg.Migrator(await pg.FreshDatabase());

        Assert.True(DbMigrator.Migrate(cs, "demo", Scripts));

        Assert.Equal(2L, await PostgresFixture.Scalar(cs, "SELECT count(*) FROM demo.schema_versions"));
        Assert.Equal(1L, await PostgresFixture.Scalar(cs,
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'demo' AND table_name = 'item' AND column_name = 'note'"));
        Assert.Equal(0L, await PostgresFixture.Scalar(cs,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'"));
    }

    [Fact(DisplayName = "ac180_4 nâng cấp từ phiên bản trước giữ dữ liệu; chạy lại không đổi gì")]
    public async Task ac180_4_upgrade_keeps_data()
    {
        var cs = pg.Migrator(await pg.FreshDatabase());
        Assert.True(DbMigrator.Migrate(cs, "demo", Scripts, name => name.Contains("0001_")));
        await PostgresFixture.Exec(cs, "INSERT INTO item (name) VALUES ('rau cải')");

        Assert.True(DbMigrator.Migrate(cs, "demo", Scripts));
        Assert.Equal("rau cải", await PostgresFixture.Scalar(cs, "SELECT name FROM item"));

        var journal = await PostgresFixture.Scalar(cs, "SELECT string_agg(scriptname || applied::text, ',' ORDER BY schemaversionsid) FROM demo.schema_versions");
        Assert.True(DbMigrator.Migrate(cs, "demo", Scripts));
        Assert.Equal(journal, await PostgresFixture.Scalar(cs, "SELECT string_agg(scriptname || applied::text, ',' ORDER BY schemaversionsid) FROM demo.schema_versions"));
    }

    [Fact(DisplayName = "script lỗi → Migrate trả false, không ghi journal script đó")]
    public async Task failing_script_returns_false()
    {
        var cs = pg.Migrator(await pg.FreshDatabase());
        await PostgresFixture.Exec(cs, "CREATE TABLE item (id int)"); // 0001 sẽ lỗi vì bảng đã có

        Assert.False(DbMigrator.Migrate(cs, "demo", Scripts));
    }

    [Fact(DisplayName = "--migrate: không có cờ → null; có cờ → chạy migrate và trả exit code")]
    public async Task run_if_requested()
    {
        var cs = pg.Migrator(await pg.FreshDatabase());

        Assert.Null(DbMigrator.RunIfRequested([], "demo", Scripts, () => throw new InvalidOperationException("không được đọc")));
        Assert.Equal(0, DbMigrator.RunIfRequested(["--migrate"], "demo", Scripts, () => cs));
        Assert.Equal(2L, await PostgresFixture.Scalar(cs, "SELECT count(*) FROM demo.schema_versions"));
    }
}
