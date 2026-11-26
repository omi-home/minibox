using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure;
using Minibox.Identity.Infrastructure.Accounts;
using Minibox.Identity.Infrastructure.Tokens;
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

    [Theory(DisplayName = "Mã quyền sai dạng miền.tài-nguyên.hành-động bị CHECK từ chối")]
    [InlineData("inventory.adjustment")]
    [InlineData("Inventory.adjustment.approve")]
    [InlineData("inventory..approve")]
    [InlineData("inventory.adjustment.approve.extra")]
    [InlineData("inventory.adjustment_x.approve")]
    public async Task malformed_permission_code_rejected(string code) =>
        await Assert.ThrowsAsync<PostgresException>(() => Exec($"INSERT INTO permission (code, description) VALUES ('{code}', 'x')"));

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

    sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
    }

    async Task<Guid> NewStaff(string email)
    {
        var id = Guid.CreateVersion7();
        await Exec($"INSERT INTO account (id, kind, email) VALUES ('{id}', 'staff', '{email}')");
        return id;
    }

    Task Assign(Guid account, string role, params string[] scopes) => Exec($"""
        INSERT INTO role_assignment (account_id, role_id, scope)
        SELECT '{account}'::uuid, id, s FROM role, unnest(ARRAY['{string.Join("','", scopes)}']) s WHERE code = '{role}'
        """);

    [Fact(DisplayName = "ac203_1: nhiều phân công → hợp các quyền, gom theo phạm vi (*, wh:n, farm:n)")]
    public async Task ac203_1_permissions_union_grouped_by_scope()
    {
        await Assign(_staff, "warehouse", "wh:1", "wh:2");
        await Assign(_staff, "qc", "wh:1");
        await Assign(_staff, "production", "farm:3");
        await Assign(_staff, "content", "*");
        var other = await NewStaff("khac@minibox.vn");
        await Assign(other, "finance", "*");

        var perms = await new PostgresPermissions(IdentityDbContext.Factory(_db)).ForAccountAsync(_staff, Ct);

        async Task<List<string>> Of(string role) =>
            await Strings($"SELECT permission_code FROM role_permission rp JOIN role r ON r.id = rp.role_id WHERE r.code = '{role}' ORDER BY permission_code COLLATE \"C\"");
        Assert.Equal(["*", "farm:3", "wh:1", "wh:2"], perms.Keys.Order(StringComparer.Ordinal));
        Assert.Equal((await Of("warehouse")).Concat(await Of("qc")).Order(StringComparer.Ordinal), perms["wh:1"]);
        Assert.Equal(await Of("warehouse"), perms["wh:2"]);
        Assert.Equal(await Of("production"), perms["farm:3"]);
        Assert.Equal(await Of("content"), perms["*"]);
        Assert.Empty(await new PostgresPermissions(IdentityDbContext.Factory(_db)).ForAccountAsync(Guid.CreateVersion7(), Ct));
    }

    [Fact(DisplayName = "ac203_4: thêm/sửa/xóa phân công hoặc đổi quyền của vai trò thu hồi refresh token của người bị ảnh hưởng")]
    public async Task ac203_4_permission_change_revokes_refresh_tokens()
    {
        var tokens = new PostgresRefreshTokens(IdentityDbContext.Factory(_db), new FixedTime());
        var bystander = await NewStaff("ngoai@minibox.vn");
        var bystanderSession = await tokens.IssueAsync(bystander, AccountKind.Staff, ["pwd", "mfa"], Ct);

        async Task AssertRevoked(Func<Task> change)
        {
            var session = await tokens.IssueAsync(_staff, AccountKind.Staff, ["pwd", "mfa"], Ct);
            await change();
            Assert.False((await tokens.RotateAsync(session.Token, Ct)).IsSuccess);
        }

        await AssertRevoked(() => Assign(_staff, "warehouse", "wh:1"));
        await AssertRevoked(() => Exec($"UPDATE role_assignment SET scope = 'wh:2' WHERE account_id = '{_staff}'"));
        await AssertRevoked(() => Exec("INSERT INTO role_permission (role_id, permission_code) SELECT id, 'finance.refund.create' FROM role WHERE code = 'warehouse'"));
        await AssertRevoked(() => Exec("DELETE FROM role_permission WHERE permission_code = 'inventory.issue.create' AND role_id = (SELECT id FROM role WHERE code = 'warehouse')"));
        await AssertRevoked(() => Exec($"DELETE FROM role_assignment WHERE account_id = '{_staff}'"));

        Assert.True((await tokens.RotateAsync(bystanderSession.Token, Ct)).IsSuccess); // người không bị đổi quyền giữ phiên
    }
}
