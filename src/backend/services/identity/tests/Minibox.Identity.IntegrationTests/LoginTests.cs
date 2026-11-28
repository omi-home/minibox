using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Application.Features.Sessions.Commands.Login;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Errors;
using Minibox.Identity.Infrastructure.Security;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Đăng nhập mật khẩu và giới hạn đăng nhập sai qua pipeline Mediator thật trên PostgreSQL tạm (spec §3.1).</summary>
public sealed class LoginTests : IAsyncLifetime
{
    const string Password = "tra-sua-tran-chau-42";
    readonly FakeTime _time = new(new DateTimeOffset(2026, 11, 26, 3, 0, 0, TimeSpan.Zero));
    readonly Guid _staff = Guid.CreateVersion7();
    IdentityHost _host = null!;
    WebApplicationFactory<Program> _api = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await IdentityHost.StartAsync();
        await _host.Exec($"INSERT INTO account (id, kind, email, username, password_hash) VALUES ('{_staff}', 'staff', 'an@minibox.vn', 'kho.an', '{new Argon2idPasswordHasher().Hash(Password)}')");
        _api = _host.CreateApi(s => s.AddSingleton<TimeProvider>(_time));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    Task<Result<TokenResponse>> Login(string identifier, string password, string ip = "10.0.0.1") =>
        IdentityHost.Send<Result<TokenResponse>>(_api, new Login(identifier, password, ip));

    [Fact(DisplayName = "Đúng mật khẩu (email hoặc username, không phân biệt hoa thường) mở phiên amr=pwd")]
    public async Task correct_password_opens_session()
    {
        var byEmail = await Login(" An@Minibox.VN ", Password);
        Assert.True(byEmail.IsSuccess);
        Assert.Equal(["pwd"], byEmail.Value.AuthMethods);
        Assert.True((await Login("kho.an", Password)).IsSuccess);
    }

    [Fact(DisplayName = "ac195_2 Sai mật khẩu và tài khoản không tồn tại trả cùng một thông báo lỗi")]
    public async Task ac195_2_same_error_for_wrong_password_and_unknown_account()
    {
        var wrong = await Login("an@minibox.vn", "sai-mat-khau-123");
        var unknown = await Login("khong-co@minibox.vn", Password);
        var tooLong = await Login("an@minibox.vn", new string('a', PasswordPolicy.MaxLength + 1));
        Assert.Equal(IdentityErrors.InvalidCredentials.Code, wrong.Error.Code);
        Assert.Equal(wrong.Error, unknown.Error);
        Assert.Equal(wrong.Error, tooLong.Error);
    }

    [Fact(DisplayName = "ac195_3 Sai liên tiếp theo tài khoản bị khóa tạm, khóa tăng dần; đúng mật khẩu khi hết khóa thì vào được")]
    public async Task ac195_3_account_lockout_escalates()
    {
        for (var i = 0; i < LoginThrottle.AccountThreshold; i++) // mỗi lần một IP khác: khóa theo tài khoản, không theo IP
            Assert.Equal(IdentityErrors.InvalidCredentials.Code, (await Login("an@minibox.vn", "sai-mat-khau-123", $"10.0.1.{i}")).Error.Code);

        Assert.Equal(IdentityErrors.LoginThrottled.Code, (await Login("an@minibox.vn", Password, "10.0.2.1")).Error.Code);

        _time.Now += LoginThrottle.BaseLock;
        Assert.Equal(IdentityErrors.InvalidCredentials.Code, (await Login("an@minibox.vn", "sai-mat-khau-123", "10.0.2.2")).Error.Code);
        _time.Now += LoginThrottle.BaseLock; // lần vượt thứ hai khóa 2 phút
        Assert.Equal(IdentityErrors.LoginThrottled.Code, (await Login("an@minibox.vn", Password, "10.0.2.3")).Error.Code);
        _time.Now += LoginThrottle.BaseLock;
        Assert.True((await Login("an@minibox.vn", Password, "10.0.2.4")).IsSuccess);

        Assert.Equal(IdentityErrors.InvalidCredentials.Code, (await Login("an@minibox.vn", "sai-mat-khau-123")).Error.Code); // đúng mật khẩu đã xoá bộ đếm
    }

    [Fact(DisplayName = "ac195_3 Sai liên tiếp từ một IP (nhiều định danh) bị khóa IP; IP khác vẫn đăng nhập được")]
    public async Task ac195_3_ip_lockout()
    {
        for (var i = 0; i < LoginThrottle.IpThreshold; i++)
            await Login($"do-tim-{i}@minibox.vn", "sai-mat-khau-123", "10.9.9.9");

        Assert.Equal(IdentityErrors.LoginThrottled.Code, (await Login("an@minibox.vn", Password, "10.9.9.9")).Error.Code);
        Assert.True((await Login("an@minibox.vn", Password, "10.0.0.2")).IsSuccess);
    }

    [Fact(DisplayName = "ac173_58 Đăng nhập sai song song cùng định danh vẫn đếm đủ (khoá advisory, không mất lần sai, không lỗi trùng khoá)")]
    public async Task ac173_58_concurrent_failures_counted()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, LoginThrottle.AccountThreshold).Select(i => Login("an@minibox.vn", "sai-mat-khau-123", $"10.0.3.{i}")));
        Assert.All(results, r => Assert.Equal(IdentityErrors.InvalidCredentials.Code, r.Error.Code));
        Assert.Equal(IdentityErrors.LoginThrottled.Code, (await Login("an@minibox.vn", Password, "10.0.3.9")).Error.Code);
    }

    [Fact(DisplayName = "Bộ đếm lưu bản băm định danh, không lưu email thô; sai mật khẩu ghi audit LoginFailed")]
    public async Task throttle_does_not_store_identifier()
    {
        await Login("an@minibox.vn", "sai-mat-khau-123");
        Assert.Equal(0L, await _host.Scalar<long>("SELECT count(*) FROM login_throttle WHERE key LIKE '%minibox%'"));
        Assert.Equal(1L, await _host.Scalar<long>($"SELECT count(*) FROM audit_log WHERE action = 'LoginFailed' AND aggregate_id = '{_staff}'"));
    }
}
