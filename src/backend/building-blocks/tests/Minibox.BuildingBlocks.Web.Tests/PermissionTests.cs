using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Application.Security;

namespace Minibox.BuildingBlocks.Web.Tests;

/// <summary>Kiểm quyền hai tầng (spec §3.6): <see cref="RequirePermissionAttribute"/> trên endpoint (hành động) và
/// <see cref="IAuthorizer"/> trong handler (phạm vi bản ghi), đọc claim <c>perms</c>.</summary>
public sealed class PermissionTests : IAsyncLifetime
{
    const string Approve = "inventory.adjustment.approve";
    WebApplication _app = null!;
    HttpClient _http = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Services.AddMiniboxWeb();
        builder.Services.AddAuthentication(HeaderAuth.Name).AddScheme<AuthenticationSchemeOptions, HeaderAuth>(HeaderAuth.Name, null);
        _app = builder.Build();
        _app.UseMiniboxWeb();
        _app.MapGet("/adjustments", [RequirePermission(Approve)] () => "ok");
        // Handler tải bản ghi (kho của phiếu = id) rồi kiểm phạm vi.
        _app.MapGet("/adjustments/{warehouse:int}", [RequirePermission(Approve)] async (int warehouse, IAuthorizer authz, CancellationToken ct) =>
            (await authz.EnsureAsync(Approve, $"wh:{warehouse}", ct)).ToHttpResult());
        await _app.StartAsync(Ct);
        _http = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    Task<HttpResponseMessage> Get(string path, string? perms)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (perms is not null) request.Headers.Add("X-Perms", perms);
        return _http.SendAsync(request, Ct);
    }

    [Theory(DisplayName = "ac203_2: gọi API trực tiếp khi thiếu mã quyền của hành động → 403")]
    [InlineData("{}")]
    [InlineData("""{"*":["inventory.adjustment.create"]}""")]
    [InlineData("""{"wh:2":["inventory.adjustment.approved"]}""")]
    [InlineData("không phải JSON")]
    public async Task ac203_2_missing_permission_is_403(string perms) =>
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/adjustments", perms)).StatusCode);

    [Fact(DisplayName = "ac203_2: có mã quyền ở bất kỳ phạm vi nào qua được tầng endpoint; chưa đăng nhập → 401")]
    public async Task ac203_2_permission_in_any_scope_passes()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get("/adjustments", """{"wh:2":["inventory.adjustment.approve"]}""")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get("/adjustments", """{"*":["inventory.adjustment.approve"]}""")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/adjustments", null)).StatusCode);
    }

    [Fact(DisplayName = "ac203_3: có quyền nhưng sai phạm vi kho của bản ghi → 403; đúng kho hoặc phạm vi * → 204")]
    public async Task ac203_3_wrong_scope_is_403()
    {
        const string wh2 = """{"wh:2":["inventory.adjustment.approve"],"farm:1":["inventory.adjustment.approve"]}""";
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/adjustments/1", wh2)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/adjustments/21", wh2)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Get("/adjustments/2", wh2)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Get("/adjustments/7", """{"*":["inventory.adjustment.approve"]}""")).StatusCode);
    }
}

/// <summary>Scheme giả cho test: principal từ header X-Perms (JSON như claim perms của token); kind customer để không dính MfaPolicy.</summary>
sealed class HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Name = "header";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Perms", out var perms)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(
        [
            new Claim(AccessTokenClaims.Subject, "u1"), new Claim(AccessTokenClaims.Kind, "customer"),
            new Claim(AccessTokenClaims.Permissions, perms.ToString(), "JSON"),
        ], Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Name)));
    }
}
