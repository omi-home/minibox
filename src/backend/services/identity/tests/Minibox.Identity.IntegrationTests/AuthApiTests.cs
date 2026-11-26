using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.Identity.Application.Accounts;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure.Accounts;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Minibox.Identity.IntegrationTests;

/// <summary>API phiên cho BFF (/auth/*) trên Api thật + PostgreSQL tạm: đăng nhập → MFA → làm mới → đăng xuất.</summary>
[Collection(MigrateCollection.Name)]
public sealed class AuthApiTests : IAsyncLifetime
{
    const string Password = "Mat-khau-du-dai-1";
    readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:18-alpine").WithDatabase("minibox").Build();
    readonly Guid _staff = Guid.CreateVersion7();
    NpgsqlDataSource _db = null!;
    WebApplicationFactory<Program> _api = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync(Ct);
        // Superuser của container tạm chỉ dùng trong test; môi trường thật dùng role identity_app.
        var conn = _pg.GetConnectionString() + ";Search Path=identity";
        _db = NpgsqlDataSource.Create(conn);
        await Exec("CREATE SCHEMA identity; CREATE ROLE identity_app");
        Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, conn);
        try
        {
            Assert.Equal(0, (int)typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { "--migrate" }])!);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Program.MigratorConnectionVariable, null);
        }
        await Exec($"INSERT INTO account (id, kind, email, password_hash) VALUES ('{_staff}', 'staff', 'an@minibox.vn', '{new Argon2idPasswordHasher().Hash(Password)}')");
        _api = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:App", conn)
            .UseSetting("Jwt:Issuer", "http://localhost")
            .UseSetting("Mfa:EncryptionKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
    }

    public async ValueTask DisposeAsync()
    {
        await _api.DisposeAsync();
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }

    async Task Exec(string sql)
    {
        await using var cmd = _db.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(Ct);
    }

    /// <summary>Bật MFA cho nhân viên, trả một mã khôi phục (tránh chờ bước TOTP kế tiếp).</summary>
    async Task<string> EnrollRecoveryCode()
    {
        var mfa = _api.Services.GetRequiredService<IMfa>();
        var secret = Base32Decode((await mfa.BeginEnrollmentAsync(_staff, Ct)).Value.Secret);
        return (await mfa.ConfirmEnrollmentAsync(_staff, Totp.Code(secret, Totp.StepAt(DateTimeOffset.UtcNow)), Ct)).Value[0];
    }

    static byte[] Base32Decode(string s)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(s.Select(c => Convert.ToString(alphabet.IndexOf(c), 2).PadLeft(5, '0')));
        return [.. Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2))];
    }

    async Task<HttpResponseMessage> Post(string path, object body, string? bearer = null)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (bearer is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await client.PostAsJsonAsync(path, body, Ct);
    }

    async Task<Program.TokenResponse> Tokens(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Program.TokenResponse>(Ct))!;
    }

    [Fact(DisplayName = "Đăng nhập (pwd) → MFA (pwd+mfa, thu hồi phiên cũ) → làm mới xoay token → đăng xuất thu hồi phiên")]
    public async Task session_flow()
    {
        var recovery = await EnrollRecoveryCode();

        var login = await Tokens(await Post("/auth/login", new Program.LoginRequest("AN@minibox.vn", Password, "10.0.0.1")));
        Assert.Equal(["pwd"], login.AuthMethods);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/mfa/verify", new Program.MfaVerifyRequest(recovery, login.RefreshToken))).StatusCode);
        var mfa = await Tokens(await Post("/auth/mfa/verify", new Program.MfaVerifyRequest(recovery, login.RefreshToken), login.AccessToken));
        Assert.Equal(["pwd", "mfa"], mfa.AuthMethods);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/refresh", new Program.RefreshRequest(login.RefreshToken))).StatusCode);

        var refreshed = await Tokens(await Post("/auth/refresh", new Program.RefreshRequest(mfa.RefreshToken)));
        Assert.NotEqual(mfa.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(["pwd", "mfa"], refreshed.AuthMethods);

        Assert.Equal(HttpStatusCode.NoContent, (await Post("/auth/logout", new Program.RefreshRequest(refreshed.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/refresh", new Program.RefreshRequest(refreshed.RefreshToken))).StatusCode);
    }

    [Fact(DisplayName = "Sai mật khẩu và refresh token rỗng không trả token")]
    public async Task invalid_credentials_rejected()
    {
        Assert.NotEqual(HttpStatusCode.OK, (await Post("/auth/login", new Program.LoginRequest("an@minibox.vn", "sai", "10.0.0.1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/refresh", new Program.RefreshRequest(null!))).StatusCode); // client sai hợp đồng vẫn 401, không 500
    }
}
