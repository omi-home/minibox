using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure;
using Minibox.Identity.Infrastructure.Accounts;
using Minibox.Identity.Infrastructure.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Đăng nhập mật khẩu và giới hạn đăng nhập sai trên PostgreSQL tạm đã migrate (spec §3.1).</summary>
[Collection(MigrateCollection.Name)]
public sealed class LoginTests : IAsyncLifetime
{
    const string Password = "tra-sua-tran-chau-42";
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero));
    readonly Argon2idPasswordHasher _hasher = new();
    NpgsqlDataSource _db = null!;
    PostgresPasswordLogin _login = null!;

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
        await Exec($"INSERT INTO account (id, kind, email, username, password_hash) VALUES (gen_random_uuid(), 'staff', 'an@minibox.vn', 'kho.an', '{_hasher.Hash(Password)}')");
        _login = new PostgresPasswordLogin(IdentityDbContext.Factory(_db), _hasher, new PostgresRefreshTokens(IdentityDbContext.Factory(_db), _time), _time);
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

    Task<BuildingBlocks.Domain.Result<LoginSuccess>> Login(string identifier, string password, string ip = "10.0.0.1") =>
        _login.LoginAsync(identifier, password, ip, Ct);

    [Fact(DisplayName = "Đúng mật khẩu (email hoặc username, không phân biệt hoa thường) mở phiên amr=pwd")]
    public async Task correct_password_opens_session()
    {
        var byEmail = await Login(" An@Minibox.VN ", Password);
        Assert.True(byEmail.IsSuccess);
        Assert.Equal(AccountKind.Staff, byEmail.Value.Kind);
        Assert.Equal(["pwd"], byEmail.Value.Session.AuthMethods);
        Assert.True((await Login("kho.an", Password)).IsSuccess);
    }

    [Fact(DisplayName = "ac195_2 Sai mật khẩu và tài khoản không tồn tại trả cùng một thông báo lỗi")]
    public async Task ac195_2_same_error_for_wrong_password_and_unknown_account()
    {
        var wrong = await Login("an@minibox.vn", "sai-mat-khau-123");
        var unknown = await Login("khong-co@minibox.vn", Password);
        var tooLong = await Login("an@minibox.vn", new string('a', PasswordPolicy.MaxLength + 1));
        Assert.Equal(IPasswordLogin.InvalidCredentialsCode, wrong.Error.Code);
        Assert.Equal(wrong.Error, unknown.Error);
        Assert.Equal(wrong.Error, tooLong.Error);
    }

    [Fact(DisplayName = "ac195_3 Sai liên tiếp theo tài khoản bị khóa tạm, khóa tăng dần; đúng mật khẩu khi hết khóa thì vào được")]
    public async Task ac195_3_account_lockout_escalates()
    {
        for (var i = 0; i < LoginThrottle.AccountThreshold; i++) // mỗi lần một IP khác: khóa theo tài khoản, không theo IP
            Assert.Equal(IPasswordLogin.InvalidCredentialsCode, (await Login("an@minibox.vn", "sai-mat-khau-123", $"10.0.1.{i}")).Error.Code);

        Assert.Equal(IPasswordLogin.ThrottledCode, (await Login("an@minibox.vn", Password, "10.0.2.1")).Error.Code);

        _time.Now += LoginThrottle.BaseLock;
        Assert.Equal(IPasswordLogin.InvalidCredentialsCode, (await Login("an@minibox.vn", "sai-mat-khau-123", "10.0.2.2")).Error.Code);
        _time.Now += LoginThrottle.BaseLock; // lần vượt thứ hai khóa 2 phút
        Assert.Equal(IPasswordLogin.ThrottledCode, (await Login("an@minibox.vn", Password, "10.0.2.3")).Error.Code);
        _time.Now += LoginThrottle.BaseLock;
        Assert.True((await Login("an@minibox.vn", Password, "10.0.2.4")).IsSuccess);

        Assert.Equal(IPasswordLogin.InvalidCredentialsCode, (await Login("an@minibox.vn", "sai-mat-khau-123")).Error.Code); // đúng mật khẩu đã xoá bộ đếm
    }

    [Fact(DisplayName = "ac195_3 Sai liên tiếp từ một IP (nhiều định danh) bị khóa IP; IP khác vẫn đăng nhập được")]
    public async Task ac195_3_ip_lockout()
    {
        for (var i = 0; i < LoginThrottle.IpThreshold; i++)
            await Login($"do-tim-{i}@minibox.vn", "sai-mat-khau-123", "10.9.9.9");

        Assert.Equal(IPasswordLogin.ThrottledCode, (await Login("an@minibox.vn", Password, "10.9.9.9")).Error.Code);
        Assert.True((await Login("an@minibox.vn", Password, "10.0.0.2")).IsSuccess);
    }

    [Fact(DisplayName = "Bộ đếm lưu bản băm định danh, không lưu email thô")]
    public async Task throttle_does_not_store_identifier()
    {
        await Login("an@minibox.vn", "sai-mat-khau-123");
        await using var cmd = _db.CreateCommand("SELECT count(*) FROM login_throttle WHERE key LIKE '%minibox%'");
        Assert.Equal(0L, await cmd.ExecuteScalarAsync(Ct));
    }
}
