using Minibox.Identity.Application.Tokens;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure;
using Minibox.Identity.Infrastructure.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Refresh token xoay vòng trên PostgreSQL tạm đã migrate (spec §3.2).</summary>
[Collection(MigrateCollection.Name)]
public sealed class RefreshTokenTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero));
    NpgsqlDataSource _db = null!;
    PostgresRefreshTokens _tokens = null!;

    sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

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
        _tokens = new PostgresRefreshTokens(IdentityDbContext.Factory(_db), _time);
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

    async Task<Guid> Account(AccountKind kind)
    {
        var id = Guid.CreateVersion7();
        await Exec($"INSERT INTO account (id, kind, username) VALUES ('{id}', '{kind.ToString().ToLowerInvariant()}', 'u{id:N}')");
        return id;
    }

    async Task<RefreshSession> Login(AccountKind kind) => await _tokens.IssueAsync(await Account(kind), kind, ["pwd"], Ct);

    [Fact(DisplayName = "Xoay vòng: token mới khác token cũ, giữ auth_time/amr; DB chỉ lưu bản băm")]
    public async Task rotate_returns_new_token_and_stores_hash_only()
    {
        var first = await Login(AccountKind.Customer);
        _time.Now += TimeSpan.FromMinutes(1);
        var second = await _tokens.RotateAsync(first.Token, Ct);

        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Token, second.Value.Token);
        Assert.Equal(first.AuthTime, second.Value.AuthTime);
        Assert.Equal(["pwd"], second.Value.AuthMethods);
        Assert.Equal(_time.Now + TimeSpan.FromDays(30), second.Value.ExpiresAt); // khách: 30 ngày trượt
        await using var cmd = _db.CreateCommand("SELECT count(*) FROM refresh_token WHERE token_hash = sha256(convert_to($1, 'UTF8'))");
        cmd.Parameters.Add(new() { Value = first.Token });
        Assert.Equal(1L, await cmd.ExecuteScalarAsync(Ct));
    }

    [Fact(DisplayName = "ac195_5 Dùng lại refresh token đã xoay thu hồi cả chuỗi token")]
    public async Task ac195_5_reuse_revokes_whole_chain()
    {
        var first = await Login(AccountKind.Customer);
        var second = (await _tokens.RotateAsync(first.Token, Ct)).Value;

        var reuse = await _tokens.RotateAsync(first.Token, Ct);
        Assert.Equal(IRefreshTokens.InvalidCode, reuse.Error.Code);
        Assert.Equal(IRefreshTokens.InvalidCode, (await _tokens.RotateAsync(second.Token, Ct)).Error.Code);
    }

    [Fact(DisplayName = "ac195_6 Phiên nhân viên hết hạn sau 30 phút không hoạt động")]
    public async Task ac195_6_staff_idle_timeout_30_minutes()
    {
        var token = (await Login(AccountKind.Staff)).Token;
        _time.Now += TimeSpan.FromMinutes(29);
        token = (await _tokens.RotateAsync(token, Ct)).Value.Token; // còn hoạt động → gia hạn
        _time.Now += TimeSpan.FromMinutes(29);
        token = (await _tokens.RotateAsync(token, Ct)).Value.Token;

        _time.Now += TimeSpan.FromMinutes(30);
        Assert.Equal(IRefreshTokens.InvalidCode, (await _tokens.RotateAsync(token, Ct)).Error.Code);
    }

    [Fact(DisplayName = "ac195_6 Phiên nhân viên tối đa 8 giờ dù vẫn hoạt động")]
    public async Task ac195_6_staff_max_8_hours()
    {
        var login = await Login(AccountKind.Staff);
        var token = login.Token;
        for (var i = 0; i < 16; i++) // 16 × 29 phút = 7 giờ 44 phút
        {
            _time.Now += TimeSpan.FromMinutes(29);
            var next = (await _tokens.RotateAsync(token, Ct)).Value;
            Assert.True(next.ExpiresAt <= login.AuthTime + TimeSpan.FromHours(8));
            token = next.Token;
        }
        _time.Now = login.AuthTime + TimeSpan.FromHours(8);
        Assert.Equal(IRefreshTokens.InvalidCode, (await _tokens.RotateAsync(token, Ct)).Error.Code);
    }

    [Fact(DisplayName = "ac195_7 Đăng xuất thu hồi refresh token")]
    public async Task ac195_7_logout_revokes_refresh_token()
    {
        var other = await Login(AccountKind.Staff);
        var token = (await _tokens.RotateAsync((await Login(AccountKind.Staff)).Token, Ct)).Value.Token;

        await _tokens.RevokeAsync(token, Ct);
        Assert.Equal(IRefreshTokens.InvalidCode, (await _tokens.RotateAsync(token, Ct)).Error.Code);
        Assert.True((await _tokens.RotateAsync(other.Token, Ct)).IsSuccess); // phiên khác không bị ảnh hưởng
    }

    [Fact(DisplayName = "Token không tồn tại trả cùng lỗi chung")]
    public async Task unknown_token_invalid() =>
        Assert.Equal(IRefreshTokens.InvalidCode, (await _tokens.RotateAsync("khong-ton-tai", Ct)).Error.Code);
}
