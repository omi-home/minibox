using System.Security.Cryptography;
using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Sessions.Commands.Login;

/// <summary>Lệnh đăng nhập bằng email/username + mật khẩu (BFF gọi), có giới hạn đăng nhập sai theo định danh và theo IP.</summary>
/// <param name="Identifier">Email hoặc username (không phân biệt hoa thường, bỏ khoảng trắng hai đầu).</param>
/// <param name="Password">Mật khẩu.</param>
/// <param name="Ip">IP người dùng cuối mà BFF thấy.</param>
public sealed record Login(string? Identifier, string? Password, string? Ip) : ICommand<TokenResponse>;

/// <summary>Kiểm dạng IP do BFF gửi (khoá bộ đếm sai theo IP). Định danh và mật khẩu cố ý không kiểm ở đây: sai dạng vẫn trả
/// <see cref="IdentityErrors.InvalidCredentials"/> như sai giá trị để không lộ gì.</summary>
public sealed class LoginValidator : AbstractValidator<Login>
{
    /// <summary>Khai báo luật: <c>Ip</c> bắt buộc, là địa chỉ IPv4/IPv6 hợp lệ.</summary>
    public LoginValidator() => RuleFor(x => x.Ip).NotEmpty().Must(ip => System.Net.IPAddress.TryParse(ip, out _)).WithMessage("IP không hợp lệ.");
}

/// <summary>Kiểm mật khẩu và mở phiên <c>amr = [pwd]</c>.</summary>
/// <param name="accounts">Repository tài khoản.</param>
/// <param name="throttles">Repository bộ đếm sai.</param>
/// <param name="hasher">Băm/kiểm mật khẩu.</param>
/// <param name="sessions">Mở phiên.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Tài khoản không tồn tại hoặc không có mật khẩu vẫn chạy Verify với bản băm giả để thời gian phản hồi không lộ tài khoản.
/// Sai mật khẩu trả lỗi <c>WithCommit</c> để bộ đếm và audit còn lại. Không log định danh, mật khẩu hay token.
/// ponytail: khoá advisory theo IP giữ suốt lần Verify (~50 ms) nên đăng nhập cùng IP chạy tuần tự; tách khi có NAT lớn.</remarks>
public sealed class LoginHandler(IAccountRepository accounts, ILoginThrottleRepository throttles, IPasswordHasher hasher, SessionIssuer sessions,
    TimeProvider time) : ICommandHandler<Login, TokenResponse>
{
    static string? _dummyHash;

    /// <inheritdoc/>
    /// <returns>Cặp token; lỗi <see cref="IdentityErrors.InvalidCredentials"/> (cùng lỗi dù tài khoản có hay không) hoặc
    /// <see cref="IdentityErrors.LoginThrottled"/> khi đang bị khoá tạm.</returns>
    public async ValueTask<Result<TokenResponse>> Handle(Login command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var login = Account.NormalizeLogin(command.Identifier);
        var (accountKey, ipKey) = (LoginThrottle.AccountKey(login), LoginThrottle.IpKey(command.Ip ?? ""));
        var accountThrottle = await throttles.LockAsync(accountKey, cancellationToken);
        var ipThrottle = await throttles.LockAsync(ipKey, cancellationToken);
        if (accountThrottle?.IsLocked(now) == true || ipThrottle?.IsLocked(now) == true)
            return IdentityErrors.LoginThrottled;

        var account = login.Length == 0 ? null : await accounts.FindByLoginAsync(login, cancellationToken);
        var hash = account?.PasswordHash;
        // Mật khẩu rỗng/quá dài bị từ chối không băm (chống DoS); không phụ thuộc tài khoản nên không lộ gì qua thời gian.
        var password = command.Password;
        var verification = password is { Length: > 0 and <= PasswordPolicy.MaxLength }
            ? hasher.Verify(hash ?? (_dummyHash ??= hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)))), password)
            : PasswordVerification.Failed;
        if (account is null || hash is null || verification == PasswordVerification.Failed)
        {
            await throttles.RecordFailureAsync(accountThrottle, accountKey, LoginThrottle.AccountThreshold, now, cancellationToken);
            await throttles.RecordFailureAsync(ipThrottle, ipKey, LoginThrottle.IpThreshold, now, cancellationToken);
            account?.RecordLoginFailure();
            return IdentityErrors.InvalidCredentials.WithCommit();
        }

        // Bộ đếm IP giữ nguyên: đăng nhập đúng một tài khoản không xoá dấu dò tài khoản khác.
        await throttles.ResetAsync(accountThrottle, cancellationToken);
        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            account.Rehash(hasher.Hash(password!));
            await accounts.UpdateAsync(account, account.Version, cancellationToken);
        }
        return await sessions.OpenAsync(account, ["pwd"], cancellationToken);
    }
}
