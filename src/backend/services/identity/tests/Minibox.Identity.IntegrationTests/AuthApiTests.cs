using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Minibox.Identity.Api.Endpoints.Auth;
using Minibox.Identity.Application.Features.Mfa.Commands.BeginMfaEnrollment;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Infrastructure.Security;

namespace Minibox.Identity.IntegrationTests;

/// <summary>API phiên cho BFF (/auth/*) trên Api thật + PostgreSQL tạm: đăng nhập → đăng ký MFA → MFA → làm mới → đăng xuất.</summary>
public sealed class AuthApiTests : IAsyncLifetime
{
    const string Password = "Mat-khau-du-dai-1";
    readonly Guid _staff = Guid.CreateVersion7();
    IdentityHost _host = null!;
    WebApplicationFactory<Program> _api = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await IdentityHost.StartAsync();
        await _host.Exec($"INSERT INTO account (id, kind, email, password_hash) VALUES ('{_staff}', 'staff', 'an@minibox.vn', '{new Argon2idPasswordHasher().Hash(Password)}')");
        _api = _host.CreateApi();
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    async Task<HttpResponseMessage> Post(string path, object? body, string? bearer = null)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (bearer is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return await client.PostAsJsonAsync(path, body ?? new { }, Ct);
    }

    static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    /// <summary>Nhân viên (chỉ pwd) tự bật MFA qua API, trả một mã khôi phục (tránh chờ bước TOTP kế tiếp).</summary>
    async Task<string> EnrollRecoveryCode(string accessToken)
    {
        var enrollment = await Ok<MfaEnrollment>(await Post("/auth/mfa/enrollment", null, accessToken));
        var code = Totp.Code(MfaTests.Base32Decode(enrollment.Secret), Totp.StepAt(DateTimeOffset.UtcNow));
        return (await Ok<string[]>(await Post("/auth/mfa/enrollment/confirm", new MfaConfirmRequest(code), accessToken)))[0];
    }

    [Fact(DisplayName = "Đăng nhập (pwd) → đăng ký MFA → MFA (pwd+mfa, thu hồi phiên cũ) → làm mới xoay token → đăng xuất thu hồi phiên")]
    public async Task session_flow()
    {
        var login = await Ok<TokenResponse>(await Post("/auth/login", new LoginRequest("AN@minibox.vn", Password, "10.0.0.1")));
        Assert.Equal(["pwd"], login.AuthMethods);
        var recovery = await EnrollRecoveryCode(login.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/mfa/verify", new MfaVerifyRequest(recovery, login.RefreshToken))).StatusCode);
        var mfa = await Ok<TokenResponse>(await Post("/auth/mfa/verify", new MfaVerifyRequest(recovery, login.RefreshToken), login.AccessToken));
        Assert.Equal(["pwd", "mfa"], mfa.AuthMethods);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/refresh", new RefreshRequest(login.RefreshToken))).StatusCode);

        var refreshed = await Ok<TokenResponse>(await Post("/auth/refresh", new RefreshRequest(mfa.RefreshToken)));
        Assert.NotEqual(mfa.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(["pwd", "mfa"], refreshed.AuthMethods);

        Assert.Equal(HttpStatusCode.NoContent, (await Post("/auth/logout", new RefreshRequest(refreshed.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/refresh", new RefreshRequest(refreshed.RefreshToken))).StatusCode);
        Assert.Equal(2L, await _host.Scalar<long>($"SELECT count(*) FROM audit_log WHERE action = 'SessionRevoked' AND aggregate_id = '{_staff}'")); // mfa + logout
    }

    [Fact(DisplayName = "Sai mật khẩu và refresh token rỗng không trả token")]
    public async Task invalid_credentials_rejected()
    {
        Assert.NotEqual(HttpStatusCode.OK, (await Post("/auth/login", new LoginRequest("an@minibox.vn", "sai", "10.0.0.1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/auth/refresh", new RefreshRequest(null!))).StatusCode); // client sai hợp đồng vẫn 401, không 500
    }
}
