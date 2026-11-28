using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.UnitTests;

/// <summary>Tài khoản và chính sách mật khẩu (spec §3.1).</summary>
public class DomainTests
{
    [Theory(DisplayName = "Mật khẩu dưới 10 ký tự bị từ chối")]
    [InlineData("")]
    [InlineData("Ab1!xyz")]
    [InlineData("9chars!!X")]
    public void short_password_rejected(string password) =>
        Assert.Equal(IdentityErrors.PasswordTooShort.Code, PasswordPolicy.Validate(password).Error.Code);

    [Theory(DisplayName = "Mật khẩu nằm trong danh sách phổ biến (offline, không phân biệt hoa thường) bị từ chối")]
    [InlineData("1234567890")]
    [InlineData("Password123")]
    [InlineData("QWERTYUIOP")]
    [InlineData("anhyeuem123")]
    public void common_password_rejected(string password) =>
        Assert.Equal(IdentityErrors.PasswordTooCommon.Code, PasswordPolicy.Validate(password).Error.Code);

    [Fact(DisplayName = "Mật khẩu đủ dài và không phổ biến được chấp nhận")]
    public void strong_password_accepted() => Assert.True(PasswordPolicy.Validate("tra-sua-tran-chau-42").IsSuccess);

    [Fact(DisplayName = "Đăng ký chuẩn hoá email/username về chữ thường, sinh UUIDv7, version 0")]
    public void register_normalizes_login()
    {
        var account = Account.Register(AccountKind.Staff, " An@Minibox.VN ", " Kho.An ", "$argon2id$hash").Value;
        Assert.Equal(AccountKind.Staff, account.Kind);
        Assert.Equal("an@minibox.vn", account.Email);
        Assert.Equal("kho.an", account.Username);
        Assert.Equal(7, account.Id.Version);
        Assert.Equal(0, account.Version);
    }

    [Fact(DisplayName = "Tài khoản phải có email hoặc username")]
    public void register_requires_login() =>
        Assert.Equal(IdentityErrors.LoginRequired.Code, Account.Register(AccountKind.Customer, null, " ", "$argon2id$hash").Error.Code);

    [Theory(DisplayName = "Email hoặc username sai định dạng bị từ chối")]
    [InlineData("khong-co-a-cong", null)]
    [InlineData(null, "ab")]
    [InlineData(null, "co khoang trang")]
    [InlineData(null, "an@minibox.vn")]
    public void register_rejects_invalid_login(string? email, string? username) =>
        Assert.True(Account.Register(AccountKind.Customer, email, username, "$argon2id$hash").IsFailure);
}
