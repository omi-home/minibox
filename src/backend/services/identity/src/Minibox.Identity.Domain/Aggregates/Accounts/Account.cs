using System.Net.Mail;
using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.Identity.Domain.Accounts;

/// <summary>Loại tài khoản (cột <c>kind</c>).</summary>
public enum AccountKind
{
    /// <summary>Khách mua hàng.</summary>
    Customer,
    /// <summary>Nhân viên vận hành.</summary>
    Staff,
}

/// <summary>Sự kiện tài khoản vừa được tạo; Unit of Work ghi <c>audit_log</c> và phát <c>identity.AccountRegistered.v1</c> qua outbox.</summary>
/// <param name="AccountId">Id tài khoản.</param>
/// <param name="Kind">Loại tài khoản.</param>
/// <remarks>Không mang email/username: payload vào audit_log và message liên service không chứa PII.</remarks>
public sealed record AccountRegistered(Guid AccountId, AccountKind Kind) : IDomainEvent;

/// <summary>Tài khoản khách hoặc nhân viên (spec §3.1); đăng nhập bằng email hoặc username.</summary>
public sealed partial class Account : AggregateRoot<Guid>
{
    /// <summary>Mã lỗi khi thiếu cả email lẫn username.</summary>
    public const string LoginRequiredCode = "Identity.LoginRequired";

    /// <summary>Mã lỗi khi email sai định dạng.</summary>
    public const string InvalidEmailCode = "Identity.InvalidEmail";

    /// <summary>Mã lỗi khi username sai định dạng.</summary>
    public const string InvalidUsernameCode = "Identity.InvalidUsername";

    Account(Guid id, AccountKind kind, string? email, string? username, string? passwordHash, long version) : base(id)
    {
        Kind = kind;
        Email = email;
        Username = username;
        PasswordHash = passwordHash;
        Version = version;
    }

    /// <summary>Loại tài khoản.</summary>
    public AccountKind Kind { get; }

    /// <summary>Email đã chuẩn hoá chữ thường, hoặc <c>null</c>.</summary>
    public string? Email { get; }

    /// <summary>Username đã chuẩn hoá chữ thường, hoặc <c>null</c>.</summary>
    public string? Username { get; }

    /// <summary>Chuỗi PHC Argon2id; <c>null</c> khi tài khoản chỉ đăng nhập xã hội.</summary>
    public string? PasswordHash { get; }

    /// <summary>Phiên bản lúc nạp (optimistic concurrency).</summary>
    public long Version { get; }

    /// <summary>Tạo tài khoản mới với Id UUIDv7.</summary>
    /// <param name="kind">Khách hay nhân viên.</param>
    /// <param name="email">Email (bỏ khoảng trắng hai đầu, chữ thường); rỗng coi như không có.</param>
    /// <param name="username">Username 3–50 ký tự <c>a-z 0-9 . _ -</c> (không phân biệt hoa thường); rỗng coi như không có.</param>
    /// <param name="passwordHash">Bản băm do <c>IPasswordHasher</c> sinh (mật khẩu đã qua <see cref="PasswordPolicy"/>), hoặc <c>null</c>.</param>
    /// <returns>Tài khoản kèm domain event <see cref="AccountRegistered"/>; lỗi <see cref="LoginRequiredCode"/>, <see cref="InvalidEmailCode"/> hoặc <see cref="InvalidUsernameCode"/>.</returns>
    /// <remarks>Trùng email/username do ràng buộc UNIQUE của DB chặn.</remarks>
    public static Result<Account> Register(AccountKind kind, string? email, string? username, string? passwordHash)
    {
        email = Normalize(email);
        username = Normalize(username);
        if (email is null && username is null)
            return Error.Validation(LoginRequiredCode, "Cần email hoặc username.");
        if (email is not null && !(MailAddress.TryCreate(email, out var mail) && mail.Address == email))
            return Error.Validation(InvalidEmailCode, "Email không hợp lệ.");
        if (username is not null && !UsernamePattern().IsMatch(username))
            return Error.Validation(InvalidUsernameCode, "Username gồm 3–50 ký tự chữ, số, dấu chấm, gạch dưới, gạch ngang.");
        var account = new Account(Guid.CreateVersion7(), kind, email, username, passwordHash, 0);
        account.Raise(new AccountRegistered(account.Id, kind));
        return account;
    }

    /// <summary>Dựng lại tài khoản đã lưu (dùng trong repository); không phát domain event.</summary>
    /// <param name="id">Id tài khoản.</param>
    /// <param name="kind">Loại tài khoản.</param>
    /// <param name="email">Email đã chuẩn hoá, hoặc <c>null</c>.</param>
    /// <param name="username">Username đã chuẩn hoá, hoặc <c>null</c>.</param>
    /// <param name="passwordHash">Chuỗi PHC Argon2id, hoặc <c>null</c>.</param>
    /// <param name="version">Phiên bản đang lưu.</param>
    /// <returns>Tài khoản.</returns>
    public static Account Load(Guid id, AccountKind kind, string? email, string? username, string? passwordHash, long version) =>
        new(id, kind, email, username, passwordHash, version);

    /// <summary>Mô tả an toàn để log: không chứa bản băm mật khẩu.</summary>
    /// <returns>Loại và Id tài khoản.</returns>
    public override string ToString() => $"Account {Kind} {Id}";

    static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    [GeneratedRegex("^[a-z0-9._-]{3,50}$")]
    private static partial Regex UsernamePattern();
}
