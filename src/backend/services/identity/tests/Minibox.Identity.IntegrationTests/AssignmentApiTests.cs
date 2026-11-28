using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Api.Endpoints.Assignments;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Application.Features.Assignments;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Application.Features.Sessions.Commands.RefreshSession;

namespace Minibox.Identity.IntegrationTests;

/// <summary>API quản trị phân công trên Api thật + PostgreSQL tạm, gọi bằng access token JWT thật.</summary>
public sealed class AssignmentApiTests : IAsyncLifetime
{
    readonly Guid _admin = Guid.CreateVersion7();
    readonly Guid _staff = Guid.CreateVersion7();
    IdentityHost _host = null!;
    WebApplicationFactory<Program> _api = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await IdentityHost.StartAsync();
        await Exec($"INSERT INTO account (id, kind, email) VALUES ('{_admin}', 'staff', 'admin@minibox.vn'), ('{_staff}', 'staff', 'kho@minibox.vn')");
        _api = _host.CreateApi();
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    Task Exec(string sql) => _host.Exec(sql);

    Task<T> Scalar<T>(string sql) => _host.Scalar<T>(sql);

    string Token(Dictionary<string, IReadOnlyCollection<string>> perms, TimeSpan? authAge = null) =>
        _api.Services.GetRequiredService<IAccessTokenIssuer>()
            .Issue(new AccessTokenRequest(_admin.ToString(), "staff", perms, DateTimeOffset.UtcNow - (authAge ?? TimeSpan.Zero), ["pwd", "mfa"]))
            .Token;

    /// <summary>Quyền quản trị phân công + quyền của <paramref name="role"/> (mặc định owner: mọi quyền) trong <paramref name="scope"/>.</summary>
    async Task<Dictionary<string, IReadOnlyCollection<string>>> Manager(string scope, string role = "owner")
    {
        var perms = await Scalar<string[]>($"SELECT array_agg(permission_code) FROM role_permission rp JOIN role r ON r.id = rp.role_id WHERE r.code = '{role}'");
        return new() { [scope] = [.. perms, AssignmentAccess.ManagePermission] };
    }

    async Task<string> Owner(TimeSpan? authAge = null) => Token(await Manager("*"), authAge);

    HttpClient Client(string token)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client;
    }

    async Task<HttpStatusCode> Grant(string token, string role, string scope, Guid? account = null) =>
        (await Client(token).PostAsJsonAsync($"/admin/accounts/{account ?? _staff}/assignments", new AssignmentRequest(role, scope), Ct)).StatusCode;

    async Task<HttpStatusCode> Revoke(string token, string role, string scope) =>
        (await Client(token).DeleteAsync($"/admin/accounts/{_staff}/assignments/{role}/{Uri.EscapeDataString(scope)}", Ct)).StatusCode;

    [Fact(DisplayName = "ac203_5 Gán/thu hồi phân công ghi audit (ai, lúc nào, trước/sau); role app không UPDATE/DELETE/TRUNCATE được audit")]
    public async Task ac203_5_assignment_changes_audited()
    {
        var token = await Owner();
        var start = DateTimeOffset.UtcNow.AddSeconds(-1);

        Assert.Equal(HttpStatusCode.NoContent, await Grant(token, "warehouse", "wh:1"));
        Assert.Equal(HttpStatusCode.NoContent, await Grant(token, "qc", "wh:1"));
        Assert.Equal(HttpStatusCode.NoContent, await Grant(token, "qc", "wh:1")); // trùng: không đổi gì, không audit
        Assert.Equal(HttpStatusCode.NoContent, await Revoke(token, "warehouse", "wh:1"));

        Assert.Equal(3L, await Scalar<long>($"SELECT count(*) FROM audit_log WHERE aggregate_type = 'Account' AND aggregate_id = '{_staff}' AND actor = '{_admin}' AND occurred_at >= '{start:O}'"));
        Assert.Equal("""{"role": "warehouse", "after": [{"role": "qc", "scope": "wh:1"}], "scope": "wh:1", "before": [{"role": "qc", "scope": "wh:1"}, {"role": "warehouse", "scope": "wh:1"}]}""",
            await Scalar<string>("SELECT payload::text FROM audit_log WHERE action = 'RoleAssignmentRevoked'"));
        Assert.Equal("""[{"role": "warehouse", "scope": "wh:1"}]""",
            await Scalar<string>("SELECT (payload->'after')::text FROM audit_log WHERE action = 'RoleAssignmentGranted' ORDER BY id LIMIT 1"));
        Assert.Equal(_admin, await Scalar<Guid>($"SELECT granted_by FROM role_assignment WHERE account_id = '{_staff}'"));

        foreach (var privilege in new[] { "UPDATE", "DELETE", "TRUNCATE" })
            Assert.False(await Scalar<bool>($"SELECT has_table_privilege('identity_app', 'audit_log', '{privilege}')"));
        foreach (var table in new[] { "role_assignment", "role_permission" })
            Assert.False(await Scalar<bool>($"SELECT has_table_privilege('identity_app', '{table}', 'TRUNCATE')"));
    }

    [Fact(DisplayName = "ac203_4 Gán phân công qua API thu hồi refresh token của người được gán")]
    public async Task ac203_4_grant_revokes_sessions()
    {
        var session = await IdentityHost.OpenSession(_api, _staff, "pwd", "mfa");
        Assert.Equal(HttpStatusCode.NoContent, await Grant(await Owner(), "warehouse", "wh:1"));
        Assert.False((await IdentityHost.Send<Result<TokenResponse>>(_api, new RefreshSession(session.RefreshToken))).IsSuccess);
    }

    [Fact(DisplayName = "ac203_2 ac203_3 Thiếu quyền quản trị phân công hoặc sai phạm vi kho → 403, đúng kho → 204")]
    public async Task ac203_3_scope_checked()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await Grant(Token(new() { ["*"] = ["report.dashboard.read"] }), "warehouse", "wh:1"));

        var wh1 = Token(await Manager("wh:1", "warehouse"));
        Assert.Equal(HttpStatusCode.Forbidden, await Grant(wh1, "warehouse", "wh:2"));
        Assert.Equal(HttpStatusCode.Forbidden, await Grant(wh1, "warehouse", "*"));
        Assert.Equal(HttpStatusCode.NoContent, await Grant(wh1, "warehouse", "wh:1"));
        Assert.Equal(HttpStatusCode.NoContent, await Grant(await Owner(), "qc", "wh:2"));
        Assert.Equal(HttpStatusCode.Forbidden, await Revoke(wh1, "qc", "wh:2"));
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM audit_log WHERE payload->>'scope' = 'wh:2' AND action = 'RoleAssignmentRevoked'"));
    }

    [Fact(DisplayName = "ac203_3 Chống leo thang: không gán vai trò có quyền mình không có trong phạm vi đó; không tự đổi phân công của mình")]
    public async Task ac203_3_no_privilege_escalation()
    {
        var wh1 = Token(await Manager("wh:1", "warehouse"));
        Assert.Equal(HttpStatusCode.Forbidden, await Grant(wh1, "owner", "wh:1"));
        Assert.Equal(HttpStatusCode.Forbidden, await Grant(wh1, "finance", "wh:1"));
        Assert.Equal(HttpStatusCode.Forbidden, await Grant(wh1, "qc", "wh:1"));
        Assert.Equal(HttpStatusCode.Forbidden, await Grant(await Owner(), "owner", "*", _admin)); // tự gán cho mình
        Assert.Equal(HttpStatusCode.NoContent, await Grant(await Owner(), "owner", "*"));
        Assert.Equal(HttpStatusCode.Forbidden, await Revoke(wh1, "owner", "*"));
        Assert.Equal(1L, await Scalar<long>("SELECT count(*) FROM audit_log"));
    }

    [Fact(DisplayName = "Đổi phân công là thao tác nhạy cảm: MFA quá 5 phút → 401 step_up_required; chưa đăng nhập → 401")]
    public async Task requires_step_up()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await Grant(await Owner(TimeSpan.FromMinutes(10)), "warehouse", "wh:1"));
        var anonymous = _api.CreateClient();
        anonymous.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync($"/admin/accounts/{_staff}/assignments", new AssignmentRequest("qc", "*"), Ct)).StatusCode);
    }

    [Fact(DisplayName = "Phạm vi sai dạng hoặc vai trò khách cho nhân viên → 400; vai trò/tài khoản/phân công không có → 404")]
    public async Task invalid_requests_rejected()
    {
        var token = await Owner();
        Assert.Equal(HttpStatusCode.BadRequest, await Grant(token, "warehouse", "wh:0"));
        Assert.Equal(HttpStatusCode.BadRequest, await Grant(token, "warehouse", "wh:1\n"));
        Assert.Equal(HttpStatusCode.BadRequest, await Grant(token, "customer", "*"));
        Assert.Equal(HttpStatusCode.NotFound, await Grant(token, "khong-co", "*"));
        Assert.Equal(HttpStatusCode.NotFound, await Grant(token, "qc", "*", Guid.CreateVersion7()));
        Assert.Equal(HttpStatusCode.NotFound, await Revoke(token, "qc", "wh:9"));
        Assert.Equal(0L, await Scalar<long>("SELECT count(*) FROM audit_log"));
    }
}
