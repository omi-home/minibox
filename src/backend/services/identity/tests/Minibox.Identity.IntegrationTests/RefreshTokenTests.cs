using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Application.Features.Sessions.Commands.Logout;
using Minibox.Identity.Application.Features.Sessions.Commands.RefreshSession;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Refresh token xoay vòng qua pipeline Mediator thật trên PostgreSQL tạm (spec §3.2).</summary>
public sealed class RefreshTokenTests : IAsyncLifetime
{
    readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero));
    IdentityHost _host = null!;
    WebApplicationFactory<Program> _api = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await IdentityHost.StartAsync();
        _api = _host.CreateApi(s => s.AddSingleton<TimeProvider>(_time));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    async Task<TokenResponse> Login(AccountKind kind)
    {
        var id = Guid.CreateVersion7();
        await _host.Exec($"INSERT INTO account (id, kind, username) VALUES ('{id}', '{kind.ToCode()}', 'u{id:N}')");
        return await IdentityHost.OpenSession(_api, id);
    }

    Task<Result<TokenResponse>> Rotate(string token) => IdentityHost.Send<Result<TokenResponse>>(_api, new RefreshSession(token));

    [Fact(DisplayName = "Xoay vòng: token mới khác token cũ, giữ amr; DB chỉ lưu bản băm")]
    public async Task rotate_returns_new_token_and_stores_hash_only()
    {
        var first = await Login(AccountKind.Customer);
        _time.Now += TimeSpan.FromMinutes(1);
        var second = await Rotate(first.RefreshToken);

        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.RefreshToken, second.Value.RefreshToken);
        Assert.Equal(["pwd"], second.Value.AuthMethods);
        Assert.Equal(_time.Now + TimeSpan.FromDays(30), second.Value.RefreshTokenExpiresAt); // khách: 30 ngày trượt
        await using var cmd = _host.Db.CreateCommand("SELECT count(*) FROM refresh_token WHERE token_hash = sha256(convert_to($1, 'UTF8'))");
        cmd.Parameters.Add(new() { Value = first.RefreshToken });
        Assert.Equal(1L, await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "ac195_5 Dùng lại refresh token đã xoay thu hồi cả chuỗi token (kể cả khi token cũ đã hết hạn) và ghi audit")]
    public async Task ac195_5_reuse_revokes_whole_chain()
    {
        var first = await Login(AccountKind.Staff);
        var second = (await Rotate(first.RefreshToken)).Value;

        _time.Now += TimeSpan.FromMinutes(31); // token cũ đã hết hạn vẫn bị coi là dùng lại (173.54)
        var reuse = await Rotate(first.RefreshToken);
        Assert.Equal(IdentityErrors.InvalidRefreshToken.Code, reuse.Error.Code);
        Assert.Equal(1L, await _host.Scalar<long>("SELECT count(*) FROM audit_log WHERE action = 'SessionRevoked' AND payload->>'Reason' = 'reuse'"));
        Assert.Equal(0L, await _host.Scalar<long>("SELECT count(*) FROM refresh_token WHERE revoked_at IS NULL"));
        Assert.Equal(IdentityErrors.InvalidRefreshToken.Code, (await Rotate(second.RefreshToken)).Error.Code);
    }

    [Fact(DisplayName = "ac195_6 Phiên nhân viên hết hạn sau 30 phút không hoạt động")]
    public async Task ac195_6_staff_idle_timeout_30_minutes()
    {
        var token = (await Login(AccountKind.Staff)).RefreshToken;
        _time.Now += TimeSpan.FromMinutes(29);
        token = (await Rotate(token)).Value.RefreshToken; // còn hoạt động → gia hạn
        _time.Now += TimeSpan.FromMinutes(29);
        token = (await Rotate(token)).Value.RefreshToken;

        _time.Now += TimeSpan.FromMinutes(30);
        Assert.Equal(IdentityErrors.InvalidRefreshToken.Code, (await Rotate(token)).Error.Code);
    }

    [Fact(DisplayName = "ac195_6 Phiên nhân viên tối đa 8 giờ dù vẫn hoạt động")]
    public async Task ac195_6_staff_max_8_hours()
    {
        var start = _time.Now;
        var token = (await Login(AccountKind.Staff)).RefreshToken;
        for (var i = 0; i < 16; i++) // 16 × 29 phút = 7 giờ 44 phút
        {
            _time.Now += TimeSpan.FromMinutes(29);
            var next = (await Rotate(token)).Value;
            Assert.True(next.RefreshTokenExpiresAt <= start + TimeSpan.FromHours(8));
            token = next.RefreshToken;
        }
        _time.Now = start + TimeSpan.FromHours(8);
        Assert.Equal(IdentityErrors.InvalidRefreshToken.Code, (await Rotate(token)).Error.Code);
    }

    [Fact(DisplayName = "ac195_7 Đăng xuất thu hồi refresh token")]
    public async Task ac195_7_logout_revokes_refresh_token()
    {
        var other = await Login(AccountKind.Staff);
        var token = (await Rotate((await Login(AccountKind.Staff)).RefreshToken)).Value.RefreshToken;

        Assert.True((await IdentityHost.Send<Result>(_api, new Logout(token))).IsSuccess);
        Assert.Equal(IdentityErrors.InvalidRefreshToken.Code, (await Rotate(token)).Error.Code);
        Assert.True((await Rotate(other.RefreshToken)).IsSuccess); // phiên khác không bị ảnh hưởng
        Assert.True((await IdentityHost.Send<Result>(_api, new Logout(null))).IsSuccess);
    }

    [Theory(DisplayName = "Token không tồn tại, rỗng hoặc quá dài trả cùng lỗi chung (173.53)")]
    [InlineData("khong-ton-tai")]
    [InlineData("")]
    [InlineData(null)]
    public async Task unknown_token_invalid(string? token) =>
        Assert.Equal(IdentityErrors.InvalidRefreshToken.Code, (await Rotate(token ?? new string('a', 1000))).Error.Code);
}
