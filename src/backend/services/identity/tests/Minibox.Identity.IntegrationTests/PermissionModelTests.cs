using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Mô hình quyền/vai trò/phân công phạm vi và seed vai trò mẫu sau khi migrate (spec §3.6, BRD §3).</summary>
[Collection(MigrateCollection.Name)]
public sealed class PermissionModelTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly Guid _staff = Guid.CreateVersion7();
    NpgsqlDataSource _db = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync(Ct);
        // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role identity_app.
        _db = NpgsqlDataSource.Create(_pg.GetConnectionString() + ";Search Path=identity");
        await Exec("CREATE SCHEMA identity; CREATE ROLE identity_app");
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, _pg.GetConnectionString() + ";Search Path=identity");
        try
        {
            Assert.Equal(0, (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
        await Exec($"INSERT INTO account (id, kind, email) VALUES ('{_staff}', 'staff', 'kho@minibox.vn')");
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }

    async Task Exec(string sql)
    {
        await using var cmd = _db.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    async Task<List<string>> Strings(string sql)
    {
        await using var cmd = _db.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(Ct);
        var result = new List<string>();
        while (await reader.ReadAsync(Ct)) result.Add(reader.GetString(0));
        return result;
    }

    [Fact(DisplayName = "ac203_6: seed đủ 11 vai trò mẫu theo BRD §3")]
    public async Task ac203_6_seeds_eleven_template_roles()
    {
        string[] expected =
        [
            "guest", "customer", "owner", "sales", "production", "warehouse", "qc", "purchasing", "finance", "content", "engineering",
        ];
        Assert.Equal(expected.Order(), await Strings("SELECT code FROM role ORDER BY code"));
    }

    [Fact(DisplayName = "ac203_6: vai trò nhân viên có quyền theo công việc BRD §3; chủ shop có mọi quyền; mã quyền dạng miền.tài-nguyên.hành-động")]
    public async Task ac203_6_role_permissions_follow_brd()
    {
        Assert.Empty(await Strings("SELECT code FROM permission WHERE code !~ '^[a-z]+(-[a-z]+)*\\.[a-z]+(-[a-z]+)*\\.[a-z]+(-[a-z]+)*$'"));
        Assert.Empty(await Strings("SELECT r.code FROM role r WHERE r.kind = 'staff' AND NOT EXISTS (SELECT 1 FROM role_permission rp WHERE rp.role_id = r.id)"));
        Assert.Empty(await Strings("SELECT p.code FROM permission p WHERE NOT EXISTS (SELECT 1 FROM role_permission rp JOIN role r ON r.id = rp.role_id WHERE r.code = 'owner' AND rp.permission_code = p.code)"));
        Assert.Contains("inventory.adjustment.create", await Strings("SELECT permission_code FROM role_permission rp JOIN role r ON r.id = rp.role_id WHERE r.code = 'warehouse'"));
        // BRD §3: kỹ thuật không mặc định có quyền thu/chi hoặc đọc PII; bán hàng không tự xác nhận tiền vào.
        Assert.DoesNotContain(await Strings("SELECT permission_code FROM role_permission rp JOIN role r ON r.id = rp.role_id WHERE r.code = 'engineering'"),
            p => p.StartsWith("finance.") || p == "customer.pii.read");
        Assert.DoesNotContain("finance.payment.confirm", await Strings("SELECT permission_code FROM role_permission rp JOIN role r ON r.id = rp.role_id WHERE r.code = 'sales'"));
    }

    [Fact(DisplayName = "Phân công: một người nhiều vai trò, nhiều phạm vi (*, wh:n, farm:n); phạm vi sai dạng bị từ chối")]
    public async Task assignment_scopes()
    {
        await Exec($"""
            INSERT INTO role_assignment (account_id, role_id, scope)
            SELECT '{_staff}'::uuid, id, s FROM role, unnest(ARRAY['wh:1', 'wh:2']) s WHERE code = 'warehouse'
            UNION ALL SELECT '{_staff}', id, 'farm:3' FROM role WHERE code = 'production'
            UNION ALL SELECT '{_staff}', id, '*' FROM role WHERE code = 'content';
            """);
        Assert.Equal(["*", "farm:3", "wh:1", "wh:2"], await Strings($"SELECT scope FROM role_assignment WHERE account_id = '{_staff}' ORDER BY scope"));

        foreach (var bad in new[] { "all", "wh:", "wh:0", "kho:1", "wh:1,wh:2", "" })
            await Assert.ThrowsAsync<PostgresException>(() =>
                Exec($"INSERT INTO role_assignment (account_id, role_id, scope) SELECT '{_staff}', id, '{bad}' FROM role WHERE code = 'qc'"));
    }
}
