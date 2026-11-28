using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Web;
using Minibox.Identity.Application.Abstractions;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Service khác kiểm access token của identity qua discovery + JWKS bằng thư viện dùng chung.</summary>
public sealed class JwksTests : IAsyncLifetime
{
    IdentityHost _host = null!;

    public async ValueTask InitializeAsync() => _host = await IdentityHost.StartAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    const string Authority = "http://localhost"; // địa chỉ gốc của TestServer

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    WebApplicationFactory<Program> Identity(DateTimeOffset now) =>
        _host.CreateApi(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FixedClock(now))));

    static string Issue(WebApplicationFactory<Program> identity, string[]? amr = null, Dictionary<string, IReadOnlyCollection<string>>? perms = null) =>
        identity.Services.GetRequiredService<IAccessTokenIssuer>()
        .Issue(new AccessTokenRequest("acc-1", "staff", perms ?? [], DateTimeOffset.UtcNow, amr ?? ["pwd", "mfa"]))
        .Token;

    /// <summary>Service "khác": chỉ dùng AddMiniboxWeb + AddMiniboxJwtAuthentication, lấy JWKS từ identity qua HTTP.</summary>
    static async Task<WebApplication> OtherService(WebApplicationFactory<Program> identity)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMiniboxWeb().AddMiniboxJwtAuthentication(Authority);
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
            o => o.BackchannelHttpHandler = identity.Server.CreateHandler());
        var app = builder.Build();
        app.UseMiniboxWeb();
        app.MapGet("/me", (ICurrentUser user) => user.UserId).RequireAuthorization();
        app.MapGet("/wh/{id:int}", [RequirePermission("inventory.adjustment.approve")] async (int id, IAuthorizer authz, CancellationToken ct) =>
            (await authz.EnsureAsync("inventory.adjustment.approve", $"wh:{id}", ct)).ToHttpResult());
        await app.StartAsync(Ct);
        return app;
    }

    static async Task<HttpResponseMessage> CallMe(WebApplication app, string? token)
    {
        var client = app.GetTestClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/me", Ct);
    }

    [Fact(DisplayName = "ac195_4 Service khác kiểm được chữ ký access token qua JWKS và đọc sub")]
    public async Task ac195_4_other_service_validates_via_jwks()
    {
        await using var identity = Identity(DateTimeOffset.UtcNow);
        await using var other = await OtherService(identity);

        var response = await CallMe(other, Issue(identity));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("acc-1", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact(DisplayName = "ac200_1 Access token staff chỉ có amr pwd (JWT thật) bị 403 mfa_required")]
    public async Task ac200_1_staff_token_without_mfa_forbidden()
    {
        await using var identity = Identity(DateTimeOffset.UtcNow);
        await using var other = await OtherService(identity);

        var response = await CallMe(other, Issue(identity, ["pwd"]));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(MfaPolicy.MfaRequiredCode, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact(DisplayName = "ac203_2 ac203_3 Claim perms của access token thật: thiếu quyền hoặc sai kho → 403, đúng kho → 204")]
    public async Task ac203_3_real_token_perms_checked()
    {
        await using var identity = Identity(DateTimeOffset.UtcNow);
        await using var other = await OtherService(identity);
        var token = Issue(identity, perms: new() { ["wh:2"] = ["inventory.adjustment.approve"] });

        async Task<HttpStatusCode> Call(string path, string t)
        {
            var client = other.GetTestClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);
            return (await client.GetAsync(path, Ct)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.NoContent, await Call("/wh/2", token));
        Assert.Equal(HttpStatusCode.Forbidden, await Call("/wh/1", token));
        Assert.Equal(HttpStatusCode.Forbidden, await Call("/wh/2", Issue(identity)));
    }

    [Fact(DisplayName = "ac195_4 Access token quá 5 phút bị từ chối 401")]
    public async Task ac195_4_expired_token_rejected()
    {
        await using var identity = Identity(DateTimeOffset.UtcNow.AddMinutes(-6));
        await using var other = await OtherService(identity);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CallMe(other, Issue(identity))).StatusCode);
    }

    [Fact(DisplayName = "Token sửa payload hoặc ký bởi khóa khác bị từ chối 401; thiếu token bị 401")]
    public async Task tampered_or_foreign_token_rejected()
    {
        await using var identity = Identity(DateTimeOffset.UtcNow);
        await using var foreign = Identity(DateTimeOffset.UtcNow); // key ring khác, cùng issuer
        await using var other = await OtherService(identity);

        var parts = Issue(identity).Split('.');
        var tampered = $"{parts[0]}.{parts[1][..^2]}{(parts[1][^2] == 'A' ? 'B' : 'A')}{parts[1][^1]}.{parts[2]}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await CallMe(other, tampered)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CallMe(other, Issue(foreign))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CallMe(other, null)).StatusCode);
    }

    [Fact(DisplayName = "Discovery trỏ jwks_uri; JWKS chỉ có khóa công khai EC P-256")]
    public async Task discovery_and_jwks_published()
    {
        await using var identity = Identity(DateTimeOffset.UtcNow);
        var client = identity.CreateClient();
        var discovery = await client.GetStringAsync("/.well-known/openid-configuration", Ct);
        Assert.Contains("\"jwks_uri\":\"http://localhost/.well-known/jwks.json\"", discovery);
        var jwks = await client.GetStringAsync("/.well-known/jwks.json", Ct);
        Assert.Contains("\"crv\":\"P-256\"", jwks);
        Assert.DoesNotContain("\"d\"", jwks);
    }
}
