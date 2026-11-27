using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Application.Behaviors;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Features.Assignments.Commands.GrantAssignment;
using Minibox.Identity.Application.Features.Assignments.Commands.RevokeAssignment;
using Minibox.Identity.Application.Features.Mfa.Commands.ConfirmMfaEnrollment;
using Minibox.Identity.Application.Features.Mfa.Commands.VerifyMfa;
using Minibox.Identity.Application.Features.Sessions;
using Minibox.Identity.Application.Features.Sessions.Commands.Login;

namespace Minibox.Identity.IntegrationTests;

/// <summary>Validator FluentValidation chạy trong pipeline Mediator (spec §5.4): dữ liệu sai dạng bị chặn trước transaction, không ghi gì.
/// Mật khẩu và refresh token cố ý không có validator: sai dạng vẫn trả lỗi chung như sai giá trị (ac195_2, 173.53).</summary>
public sealed class ValidationTests : IAsyncLifetime
{
    readonly TestUser _user = new() { UserId = Guid.CreateVersion7().ToString() };
    IdentityHost _host = null!;
    WebApplicationFactory<Program> _api = null!;

    public async ValueTask InitializeAsync()
    {
        _host = await IdentityHost.StartAsync();
        _api = _host.CreateApi(s => s.AddSingleton<ICurrentUser>(_user));
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    async Task AssertInvalid<T>(Mediator.IMessage command) where T : Result
    {
        var result = await IdentityHost.Send<T>(_api, command);
        Assert.Equal(ValidationBehavior<Login, Result>.ErrorCode, result.Error.Code);
        Assert.Equal(0L, await _host.Scalar<long>("SELECT count(*) FROM login_throttle"));
    }

    [Theory(DisplayName = "Đăng nhập thiếu IP hoặc IP sai dạng bị validator chặn, không đếm sai")]
    [InlineData("")]
    [InlineData("khong-phai-ip")]
    public Task login_requires_valid_ip(string ip) =>
        AssertInvalid<Result<TokenResponse>>(new Login("an@minibox.vn", "sai-mat-khau-123", ip));

    [Fact(DisplayName = "Mã MFA rỗng hoặc quá dài bị validator chặn, không đếm sai")]
    public async Task mfa_code_shape_validated()
    {
        await AssertInvalid<Result<TokenResponse>>(new VerifyMfa("", null));
        await AssertInvalid<Result<TokenResponse>>(new VerifyMfa(new string('1', 1000), null));
        await AssertInvalid<Result<IReadOnlyList<string>>>(new ConfirmMfaEnrollment(null));
    }

    [Fact(DisplayName = "Gán/thu hồi phân công thiếu vai trò hoặc phạm vi bị validator chặn")]
    public async Task assignment_requires_role_and_scope()
    {
        await AssertInvalid<Result>(new GrantAssignment(Guid.CreateVersion7(), "", "wh:1"));
        await AssertInvalid<Result>(new GrantAssignment(Guid.CreateVersion7(), "warehouse", new string('x', 1000)));
        await AssertInvalid<Result>(new RevokeAssignment(Guid.CreateVersion7(), "warehouse", ""));
    }
}
