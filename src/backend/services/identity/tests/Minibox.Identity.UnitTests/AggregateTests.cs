using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;
using Minibox.Identity.Domain.Errors;
using Minibox.Identity.Domain.Services;

namespace Minibox.Identity.UnitTests;

/// <summary>Luật nghiệp vụ trong aggregate/domain service của identity (bug #456: chuyển từ lớp Postgres* về Domain).</summary>
public class AggregateTests
{
    static readonly DateTimeOffset T0 = new(2026, 11, 26, 3, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "LoginThrottle: tới ngưỡng thì khoá; quá cửa sổ 1 giờ không sai thì đếm lại từ 1; Reset xoá khoá")]
    public void login_throttle_counts_within_window()
    {
        var t = LoginThrottle.Create(LoginThrottle.IpKey("10.0.0.1"));
        for (var i = 0; i < LoginThrottle.AccountThreshold; i++) t.RecordFailure(LoginThrottle.AccountThreshold, T0);
        Assert.True(t.IsLocked(T0));
        Assert.False(t.IsLocked(T0 + LoginThrottle.BaseLock));

        t.RecordFailure(LoginThrottle.AccountThreshold, T0 + LoginThrottle.Window + TimeSpan.FromSeconds(1));
        Assert.Equal(1, t.Failures);

        t.Reset();
        Assert.Equal(0, t.Failures);
        Assert.Null(t.LockedUntil);
    }

    [Fact(DisplayName = "LoginThrottle: khoá theo định danh lưu SHA-256, không chứa định danh thô")]
    public void account_key_is_hashed() => Assert.DoesNotContain("minibox", LoginThrottle.AccountKey("an@minibox.vn"));

    [Fact(DisplayName = "RefreshToken: xoay giữ auth_time/amr, token cũ thành đã dùng; đưa lại token đã dùng (kể cả hết hạn) là RefreshTokenReused")]
    public void refresh_token_rotation_and_reuse()
    {
        var (first, value) = RefreshToken.Open(Guid.CreateVersion7(), AccountKind.Staff, ["pwd"], T0);
        Assert.Equal(RefreshToken.Hash(value), first.Id);
        Assert.Equal(T0 + TimeSpan.FromMinutes(30), first.ExpiresAt);

        var (next, nextValue) = first.Rotate(AccountKind.Staff, T0 + TimeSpan.FromMinutes(10)).Value;
        Assert.NotEqual(value, nextValue);
        Assert.Equal(first.FamilyId, next.FamilyId);
        Assert.Equal(T0, next.AuthTime);
        Assert.Equal(["pwd"], next.Amr);
        Assert.Equal(IdentityErrors.RefreshTokenReused, first.Rotate(AccountKind.Staff, T0 + TimeSpan.FromHours(2)).Error);
        Assert.Equal(IdentityErrors.InvalidRefreshToken, next.Rotate(AccountKind.Staff, T0 + TimeSpan.FromHours(2)).Error); // hết hạn
    }

    [Fact(DisplayName = "RefreshToken: phiên nhân viên không vượt 8 giờ kể từ đăng nhập; khách trượt 30 ngày")]
    public void refresh_token_lifetime()
    {
        var (token, _) = RefreshToken.Open(Guid.CreateVersion7(), AccountKind.Staff, ["pwd"], T0);
        for (var i = 1; i <= 16; i++) // 16 × 29 phút = 7 giờ 44 phút
            token = token.Rotate(AccountKind.Staff, T0 + TimeSpan.FromMinutes(29 * i)).Value.Token;
        Assert.Equal(T0 + TimeSpan.FromHours(8), token.ExpiresAt);
        Assert.Equal(T0 + TimeSpan.FromDays(30), RefreshToken.Open(Guid.CreateVersion7(), AccountKind.Customer, ["pwd"], T0).Token.ExpiresAt);
    }

    [Fact(DisplayName = "AssignmentPolicy: thiếu một quyền của vai trò trong phạm vi hoặc tự đổi phân công của mình → Forbidden")]
    public void assignment_policy_blocks_escalation()
    {
        var (actor, target) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var required = AssignmentPolicy.RequiredPermissions(["inventory.adjustment.create", "inventory.adjustment.create"]);
        Assert.Equal(["inventory.adjustment.create", AssignmentPolicy.ManagePermission], required);

        Assert.True(AssignmentPolicy.CanChange(actor, target, required, required.ToHashSet()).IsSuccess);
        Assert.Equal(IdentityErrors.AssignmentForbidden, AssignmentPolicy.CanChange(actor, target, required, new HashSet<string> { AssignmentPolicy.ManagePermission }).Error);
        Assert.Equal(IdentityErrors.AssignmentForbidden, AssignmentPolicy.CanChange(actor, actor, required, required.ToHashSet()).Error);
    }

    [Theory(DisplayName = "AssignmentPolicy: phạm vi chỉ *, wh:n, farm:n (n nguyên dương)")]
    [InlineData("*", true)]
    [InlineData("wh:1", true)]
    [InlineData("farm:30", true)]
    [InlineData("wh:0", false)]
    [InlineData("wh:1\n", false)]
    [InlineData("kho:1", false)]
    [InlineData(null, false)]
    public void assignment_scope_format(string? scope, bool valid) => Assert.Equal(valid, AssignmentPolicy.IsValidScope(scope));

    [Fact(DisplayName = "Account: xác nhận MFA cấp 10 mã khôi phục, mỗi mã dùng một lần; TOTP không phát lại bước đã dùng")]
    public void account_mfa_rules()
    {
        var account = Account.Register(AccountKind.Staff, "an@minibox.vn", null, null).Value;
        var secret = MfaPolicy.NewSecret();
        Assert.Equal(IdentityErrors.MfaNotEnrolled, account.VerifyMfa(secret, "123456", T0).Error);
        Assert.True(account.BeginMfaEnrollment([1, 2, 3]).IsSuccess);

        var codes = account.ConfirmMfa(secret, Totp.Code(secret, Totp.StepAt(T0)), T0).Value;
        Assert.Equal(MfaPolicy.RecoveryCodeCount, codes.Distinct().Count());
        Assert.True(account.HasMfa);
        Assert.Equal(IdentityErrors.MfaAlreadyEnrolled, account.BeginMfaEnrollment([4]).Error);
        Assert.Contains(account.DomainEvents, e => e is MfaEnabled);

        Assert.Equal(IdentityErrors.InvalidMfaCode, account.VerifyMfa(secret, Totp.Code(secret, Totp.StepAt(T0)), T0).Error); // bước đã dùng
        var later = T0 + Totp.Step;
        Assert.True(account.VerifyMfa(secret, Totp.Code(secret, Totp.StepAt(later)), later).IsSuccess);
        Assert.True(account.VerifyMfa(secret, codes[0], later).IsSuccess);
        Assert.Equal(IdentityErrors.InvalidMfaCode, account.VerifyMfa(secret, codes[0], later).Error);
    }
}
