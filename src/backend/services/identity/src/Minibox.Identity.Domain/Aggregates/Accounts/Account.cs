using System.Net.Mail;
using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Aggregates.Roles;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Domain.Aggregates.Accounts;

/// <summary>Tài khoản khách hoặc nhân viên (spec §3.1), gồm MFA TOTP, mã khôi phục và phân công vai trò (spec §3.5, §3.6).</summary>
/// <remarks>Thay đổi MFA/phân công phải nạp tài khoản bằng <c>GetForUpdateAsync</c> (khoá dòng) để hai request song song không chồng nhau.</remarks>
public sealed partial class Account : AggregateRoot<Guid>
{
    readonly List<RecoveryCode> _recoveryCodes = [];
    readonly List<RoleAssignment> _assignments = [];

    Account(Guid id, AccountKind kind, string? email, string? username, string? passwordHash) : base(id)
    {
        Kind = kind;
        Email = email;
        Username = username;
        PasswordHash = passwordHash;
    }

    /// <summary>Loại tài khoản.</summary>
    public AccountKind Kind { get; private set; }

    /// <summary>Email đã chuẩn hoá chữ thường, hoặc <c>null</c>.</summary>
    public string? Email { get; private set; }

    /// <summary>Username đã chuẩn hoá chữ thường, hoặc <c>null</c>.</summary>
    public string? Username { get; private set; }

    /// <summary>Chuỗi PHC Argon2id; <c>null</c> khi tài khoản chỉ đăng nhập xã hội.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Phiên bản lúc nạp (optimistic concurrency).</summary>
    public long Version { get; private set; }

    /// <summary>Secret TOTP (đang chờ xác nhận hoặc đã bật), hoặc <c>null</c> nếu chưa đăng ký.</summary>
    public MfaTotp? Mfa { get; private set; }

    /// <summary>Mã khôi phục của lần bật MFA gần nhất (chỉ bản băm).</summary>
    public IReadOnlyList<RecoveryCode> RecoveryCodes => _recoveryCodes;

    /// <summary>Phân công vai trò theo phạm vi.</summary>
    public IReadOnlyList<RoleAssignment> Assignments => _assignments;

    /// <summary>Nhãn hiển thị trong authenticator: email, không có thì username.</summary>
    public string Label => Email ?? Username ?? Id.ToString();

    /// <summary>MFA đã xác nhận.</summary>
    public bool HasMfa => Mfa is { ConfirmedAt: not null };

    /// <summary>Đã bắt đầu đăng ký MFA nhưng chưa xác nhận.</summary>
    public bool HasPendingMfa => Mfa is { ConfirmedAt: null };

    /// <summary>Tạo tài khoản mới với Id UUIDv7.</summary>
    /// <param name="kind">Khách hay nhân viên.</param>
    /// <param name="email">Email (bỏ khoảng trắng hai đầu, chữ thường); rỗng coi như không có.</param>
    /// <param name="username">Username 3–50 ký tự <c>a-z 0-9 . _ -</c> (không phân biệt hoa thường); rỗng coi như không có.</param>
    /// <param name="passwordHash">Bản băm do <c>IPasswordHasher</c> sinh (mật khẩu đã qua <see cref="PasswordPolicy"/>), hoặc <c>null</c>.</param>
    /// <returns>Tài khoản kèm domain event <see cref="AccountRegistered"/>; lỗi <see cref="IdentityErrors.LoginRequired"/>,
    /// <see cref="IdentityErrors.InvalidEmail"/> hoặc <see cref="IdentityErrors.InvalidUsername"/>.</returns>
    /// <remarks>Trùng email/username do ràng buộc UNIQUE của DB chặn.</remarks>
    public static Result<Account> Register(AccountKind kind, string? email, string? username, string? passwordHash)
    {
        email = Normalize(email);
        username = Normalize(username);
        if (email is null && username is null)
            return IdentityErrors.LoginRequired;
        if (email is not null && !(MailAddress.TryCreate(email, out var mail) && mail.Address == email))
            return IdentityErrors.InvalidEmail;
        if (username is not null && !UsernamePattern().IsMatch(username))
            return IdentityErrors.InvalidUsername;
        var account = new Account(Guid.CreateVersion7(), kind, email, username, passwordHash);
        account.Raise(new AccountRegistered(account.Id, kind));
        return account;
    }

    /// <summary>Chuẩn hoá định danh đăng nhập như lúc lưu: bỏ khoảng trắng hai đầu, chữ thường.</summary>
    /// <param name="identifier">Email hoặc username người dùng nhập.</param>
    /// <returns>Định danh đã chuẩn hoá; rỗng nếu không có.</returns>
    public static string NormalizeLogin(string? identifier) => (identifier ?? "").Trim().ToLowerInvariant();

    /// <summary>Thay bản băm mật khẩu bằng bản băm tham số mới (băm lại khi đăng nhập đúng).</summary>
    /// <param name="passwordHash">Bản băm mới của cùng mật khẩu.</param>
    public void Rehash(string passwordHash) => PasswordHash = passwordHash;

    /// <summary>Ghi nhận một lần đăng nhập sai mật khẩu (audit, spec BRD).</summary>
    public void RecordLoginFailure() => Raise(new LoginFailed(Id));

    /// <summary>Ghi nhận phiên đăng nhập bị thu hồi (audit).</summary>
    /// <param name="familyId">Phiên (family refresh token).</param>
    /// <param name="reason"><c>logout</c>, <c>reuse</c> (token đã xoay bị đưa lại) hoặc <c>mfa</c> (thay bằng phiên có MFA).</param>
    public void RecordSessionRevoked(Guid familyId, string reason) => Raise(new SessionRevoked(Id, familyId, reason));

    /// <summary>Bắt đầu (hoặc làm lại) đăng ký MFA với secret mới đã mã hoá.</summary>
    /// <param name="secretEnc">Secret TOTP đã mã hoá.</param>
    /// <returns>Thành công; lỗi <see cref="IdentityErrors.MfaAlreadyEnrolled"/> nếu MFA đã bật.</returns>
    public Result BeginMfaEnrollment(byte[] secretEnc)
    {
        if (HasMfa)
            return IdentityErrors.MfaAlreadyEnrolled;
        if (Mfa is null)
            Mfa = new MfaTotp(secretEnc);
        else
            Mfa.Restart(secretEnc);
        return Result.Success();
    }

    /// <summary>Bật MFA khi mã khớp secret đang chờ; cấp bộ mã khôi phục mới (thay bộ cũ).</summary>
    /// <param name="secret">Secret đang chờ đã giải mã.</param>
    /// <param name="code">Mã 6 chữ số người dùng nhập.</param>
    /// <param name="now">Thời điểm kiểm.</param>
    /// <returns><see cref="MfaPolicy.RecoveryCodeCount"/> mã khôi phục (chỉ lộ một lần); lỗi <see cref="IdentityErrors.MfaNotEnrolled"/>
    /// nếu chưa bắt đầu đăng ký, <see cref="IdentityErrors.InvalidMfaCode"/> nếu sai mã.</returns>
    public Result<IReadOnlyList<string>> ConfirmMfa(byte[] secret, string? code, DateTimeOffset now)
    {
        if (Mfa is not { ConfirmedAt: null } pending)
            return IdentityErrors.MfaNotEnrolled;
        if (Totp.Match(secret, code ?? "", now, null) is not { } step)
            return IdentityErrors.InvalidMfaCode;
        pending.Confirm(now, step);
        string[] codes = [.. Enumerable.Range(0, MfaPolicy.RecoveryCodeCount).Select(_ => MfaPolicy.NewRecoveryCode())];
        _recoveryCodes.Clear();
        _recoveryCodes.AddRange(codes.Select(c => new RecoveryCode(MfaPolicy.HashRecoveryCode(MfaPolicy.NormalizeRecoveryCode(c)!))));
        Raise(new MfaEnabled(Id));
        return codes;
    }

    /// <summary>Kiểm mã TOTP (chống phát lại bước đã dùng) hoặc mã khôi phục (mỗi mã dùng một lần).</summary>
    /// <param name="secret">Secret đã giải mã.</param>
    /// <param name="code">Mã 6 chữ số, hoặc mã khôi phục (không phân biệt hoa thường, bỏ gạch nối).</param>
    /// <param name="now">Thời điểm kiểm.</param>
    /// <returns>Thành công; lỗi <see cref="IdentityErrors.MfaNotEnrolled"/> hoặc <see cref="IdentityErrors.InvalidMfaCode"/>.</returns>
    public Result VerifyMfa(byte[] secret, string? code, DateTimeOffset now)
    {
        if (Mfa is not { ConfirmedAt: not null } mfa)
            return IdentityErrors.MfaNotEnrolled;
        var input = (code ?? "").Trim();
        if (input.Length == 6)
        {
            if (Totp.Match(secret, input, now, mfa.LastUsedStep) is not { } step)
                return IdentityErrors.InvalidMfaCode;
            mfa.Use(step);
            return Result.Success();
        }
        var hash = MfaPolicy.NormalizeRecoveryCode(input) is { } normalized ? MfaPolicy.HashRecoveryCode(normalized) : null;
        var match = hash is null ? null : _recoveryCodes.FirstOrDefault(c => c.UsedAt is null && c.CodeHash.AsSpan().SequenceEqual(hash));
        if (match is null)
            return IdentityErrors.InvalidMfaCode;
        match.Use(now);
        return Result.Success();
    }

    /// <summary>Gán vai trò trong phạm vi; ghi ảnh chụp phân công trước/sau vào sự kiện (audit).</summary>
    /// <param name="role">Vai trò.</param>
    /// <param name="scope">Phạm vi đã kiểm dạng (<see cref="Services.AssignmentPolicy.IsValidScope"/>).</param>
    /// <param name="actor">Người thực hiện.</param>
    /// <param name="roleCodes">Id → mã của mọi vai trò, để ảnh chụp dùng mã vai trò.</param>
    /// <returns><c>true</c> nếu đã gán, <c>false</c> nếu đã có (không đổi gì, không audit); lỗi <see cref="IdentityErrors.AssignmentKindMismatch"/>.</returns>
    public Result<bool> Grant(Role role, string scope, Guid actor, IReadOnlyDictionary<Guid, string> roleCodes)
    {
        if (role.Kind != Kind)
            return IdentityErrors.AssignmentKindMismatch;
        if (_assignments.Any(a => a.RoleId == role.Id && a.Scope == scope))
            return false;
        var before = Snapshot(roleCodes);
        _assignments.Add(new RoleAssignment(role.Id, scope, actor));
        Raise(new RoleAssignmentGranted(role.Code, scope, before, Snapshot(roleCodes)));
        return true;
    }

    /// <summary>Thu hồi một phân công; ghi ảnh chụp trước/sau vào sự kiện (audit).</summary>
    /// <param name="role">Vai trò.</param>
    /// <param name="scope">Phạm vi của phân công.</param>
    /// <param name="roleCodes">Id → mã của mọi vai trò.</param>
    /// <returns>Thành công; lỗi <see cref="IdentityErrors.AssignmentNotFound"/> khi không có phân công đó.</returns>
    public Result Revoke(Role role, string scope, IReadOnlyDictionary<Guid, string> roleCodes)
    {
        var assignment = _assignments.FirstOrDefault(a => a.RoleId == role.Id && a.Scope == scope);
        if (assignment is null)
            return IdentityErrors.AssignmentNotFound;
        var before = Snapshot(roleCodes);
        _assignments.Remove(assignment);
        Raise(new RoleAssignmentRevoked(role.Code, scope, before, Snapshot(roleCodes)));
        return Result.Success();
    }

    /// <summary>Mô tả an toàn để log: không chứa bản băm mật khẩu.</summary>
    /// <returns>Loại và Id tài khoản.</returns>
    public override string ToString() => $"Account {Kind} {Id}";

    // Thứ tự ordinal (như COLLATE "C") để ảnh chụp trước/sau ổn định.
    List<AssignmentItem> Snapshot(IReadOnlyDictionary<Guid, string> roleCodes) =>
    [
        .. _assignments.Select(a => new AssignmentItem(roleCodes.GetValueOrDefault(a.RoleId, a.RoleId.ToString()), a.Scope))
            .OrderBy(i => i.Role, StringComparer.Ordinal).ThenBy(i => i.Scope, StringComparer.Ordinal),
    ];

    static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    [GeneratedRegex("^[a-z0-9._-]{3,50}$")]
    private static partial Regex UsernamePattern();
}
