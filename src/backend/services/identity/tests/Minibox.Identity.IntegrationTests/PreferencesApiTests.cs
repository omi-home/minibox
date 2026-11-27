using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Minibox.Identity.Application.Features.Accounts.Queries.GetMyPreferences;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Tùy chọn hiển thị của tài khoản (locale, múi giờ) qua <c>/me/preferences</c> trên Api thật + PostgreSQL tạm.</summary>
public sealed class PreferencesApiTests : IAsyncLifetime
{
    readonly Guid _customer = Guid.CreateVersion7();
    IdentityHost _host = null!;
    WebApplicationFactory<Program> _api = null!;

    static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await IdentityHost.StartAsync();
        await _host.Exec($"INSERT INTO account (id, kind, email) VALUES ('{_customer}', 'customer', 'khach@minibox.vn')");
        _api = _host.CreateApi();
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    async Task<HttpClient> Client(Guid? account)
    {
        var client = _api.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (account is { } id)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await IdentityHost.OpenSession(_api, id, "pwd")).AccessToken);
        return client;
    }

    [Fact(DisplayName = "Mặc định múi giờ Asia/Ho_Chi_Minh, chưa chọn locale; PUT lưu locale + múi giờ IANA, ghi audit, GET đọc lại")]
    public async Task preferences_round_trip()
    {
        var client = await Client(_customer);
        Assert.Equal(new PreferencesDto(null, "Asia/Ho_Chi_Minh"), await client.GetFromJsonAsync<PreferencesDto>("/me/preferences", Ct));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/me/preferences", new { locale = "EN", timeZone = "Europe/Berlin" }, Ct)).StatusCode);
        Assert.Equal(new PreferencesDto("en", "Europe/Berlin"), await client.GetFromJsonAsync<PreferencesDto>("/me/preferences", Ct));
        Assert.Equal(1L, await _host.Scalar<long>($"SELECT count(*) FROM audit_log WHERE action = 'AccountPreferencesChanged' AND aggregate_id = '{_customer}'"));

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/me/preferences", new { locale = (string?)null, timeZone = "Asia/Ho_Chi_Minh" }, Ct)).StatusCode);
        Assert.Equal(new PreferencesDto(null, "Asia/Ho_Chi_Minh"), await client.GetFromJsonAsync<PreferencesDto>("/me/preferences", Ct));
    }

    [Theory(DisplayName = "Từ chối locale sai dạng BCP 47 và múi giờ không phải IANA")]
    [InlineData("vi_VN", "Asia/Ho_Chi_Minh")]
    [InlineData("vi", "GMT+7")]
    [InlineData("vi", "Mars/Olympus")]
    [InlineData("vi", "")]
    public async Task invalid_preferences_rejected(string locale, string timeZone)
    {
        var client = await Client(_customer);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/me/preferences", new { locale, timeZone }, Ct)).StatusCode);
        Assert.Equal("Asia/Ho_Chi_Minh", await _host.Scalar<string>($"SELECT time_zone FROM account WHERE id = '{_customer}'"));
    }

    [Fact(DisplayName = "Chưa đăng nhập bị 401")]
    public async Task anonymous_rejected()
    {
        var client = await Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/me/preferences", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/me/preferences", new { locale = "en", timeZone = "UTC" }, Ct)).StatusCode);
    }

    [Fact(DisplayName = "DB chặn locale/múi giờ sai dạng ghi thẳng")]
    public async Task db_constraints()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => _host.Exec($"UPDATE account SET locale = 'VI' WHERE id = '{_customer}'"));
        await Assert.ThrowsAnyAsync<Exception>(() => _host.Exec($"UPDATE account SET time_zone = '' WHERE id = '{_customer}'"));
    }
}
