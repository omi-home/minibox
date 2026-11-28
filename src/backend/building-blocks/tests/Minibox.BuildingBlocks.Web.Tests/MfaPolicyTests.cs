using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Minibox.BuildingBlocks.Application.Security;

namespace Minibox.BuildingBlocks.Web.Tests;

/// <summary>Chính sách MFA bắt buộc cho staff và step-up cho endpoint <see cref="SensitiveAttribute"/> (spec §3.5), không cần DB.</summary>
public sealed class MfaPolicyTests : IAsyncLifetime
{
    static readonly DateTimeOffset Now = new(2026, 11, 26, 8, 0, 0, TimeSpan.Zero);
    readonly FixedTime _time = new(Now);
    WebApplication _app = null!;
    HttpClient _http = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Services.AddMiniboxWeb().AddSingleton<TimeProvider>(_time);
        _app = builder.Build();
        // Principal giả từ header (thay JWT): X-Kind, X-Amr (phân tách dấu phẩy), X-Auth-Age (giây trước Now).
        _app.Use((context, next) =>
        {
            var h = context.Request.Headers;
            if (h.ContainsKey("X-Kind"))
            {
                var claims = new List<Claim> { new(AccessTokenClaims.Subject, "u1"), new(AccessTokenClaims.Kind, h["X-Kind"].ToString()) };
                claims.AddRange(h["X-Amr"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => new Claim(AccessTokenClaims.AuthMethods, m)));
                if (long.TryParse(h["X-Auth-Age"], out var age))
                    claims.Add(new Claim(AccessTokenClaims.AuthTime, (Now.ToUnixTimeSeconds() - age).ToString()));
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            }
            return next(context);
        });
        _app.UseMiniboxWeb();
        _app.MapGet("/admin/orders", () => "ok");
        _app.MapGet("/mfa/enroll", [AllowWithoutMfa] () => "ok");
        _app.MapGet("/admin/refunds", [Sensitive] () => "ok");
        await _app.StartAsync(Ct);
        _http = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    Task<HttpResponseMessage> Get(string path, string? kind, string amr = "", long? authAge = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (kind is not null) request.Headers.Add("X-Kind", kind);
        request.Headers.Add("X-Amr", amr);
        if (authAge is not null) request.Headers.Add("X-Auth-Age", authAge.ToString());
        return _http.SendAsync(request, Ct);
    }

    static async Task<string?> Code(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();

    [Fact(DisplayName = "ac200_1: staff chưa có MFA (amr chỉ pwd) bị 403 mfa_required ở API admin")]
    public async Task ac200_1_staff_without_mfa_is_forbidden()
    {
        var response = await Get("/admin/orders", "staff", "pwd", 0);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(MfaPolicy.MfaRequiredCode, await Code(response));
    }

    [Fact(DisplayName = "ac200_1: staff chưa có MFA vẫn gọi được API đăng ký MFA ([AllowWithoutMfa])")]
    public async Task ac200_1_staff_without_mfa_can_enroll()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get("/mfa/enroll", "staff", "pwd", 0)).StatusCode);
    }

    [Fact(DisplayName = "ac200_1: staff có amr mfa và khách chỉ pwd gọi được API thường")]
    public async Task ac200_1_staff_with_mfa_and_customer_pass()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get("/admin/orders", "staff", "pwd,mfa", 0)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get("/admin/orders", "customer", "pwd", 0)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get("/admin/orders", null)).StatusCode); // ẩn danh: để authorization của endpoint quyết định
    }

    [Theory(DisplayName = "ac200_5: endpoint [Sensitive] trả 401 step_up_required khi auth_time quá 5 phút, thiếu auth_time hoặc thiếu mfa")]
    [InlineData("pwd,mfa", 301L)]
    [InlineData("pwd,mfa", null)]
    [InlineData("pwd", 0L)]
    public async Task ac200_5_sensitive_requires_recent_mfa(string amr, long? authAge)
    {
        var response = await Get("/admin/refunds", "customer", amr, authAge);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(MfaPolicy.StepUpRequiredCode, await Code(response));
        Assert.Contains("insufficient_user_authentication", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact(DisplayName = "ac200_5: endpoint [Sensitive] cho qua khi amr có mfa và auth_time trong 5 phút")]
    public async Task ac200_5_sensitive_passes_with_recent_mfa()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get("/admin/refunds", "staff", "pwd,mfa", 300)).StatusCode);
    }

    [Fact(DisplayName = "ac200_5: endpoint [Sensitive] với người chưa đăng nhập trả 401 step_up_required")]
    public async Task ac200_5_sensitive_anonymous_is_401()
    {
        var response = await Get("/admin/refunds", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(MfaPolicy.StepUpRequiredCode, await Code(response));
    }
}

sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
