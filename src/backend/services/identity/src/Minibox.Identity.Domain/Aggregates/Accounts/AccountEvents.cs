using System.Text.Json.Serialization;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.Identity.Domain.Aggregates.Accounts;

/// <summary>Tài khoản vừa được tạo; Unit of Work ghi <c>audit_log</c> và phát <c>identity.AccountRegistered.v1</c> qua outbox.</summary>
/// <param name="AccountId">Id tài khoản.</param>
/// <param name="Kind">Loại tài khoản.</param>
/// <remarks>Không mang email/username: payload vào audit_log và message liên service không chứa PII.</remarks>
public sealed record AccountRegistered(Guid AccountId, AccountKind Kind) : IDomainEvent;

/// <summary>Đăng nhập sai mật khẩu vào tài khoản có thật (chỉ audit, không phát ra ngoài).</summary>
/// <param name="AccountId">Id tài khoản.</param>
public sealed record LoginFailed(Guid AccountId) : IDomainEvent;

/// <summary>MFA vừa được bật (audit).</summary>
/// <param name="AccountId">Id tài khoản.</param>
public sealed record MfaEnabled(Guid AccountId) : IDomainEvent;

/// <summary>Một phiên đăng nhập bị thu hồi (audit).</summary>
/// <param name="AccountId">Id tài khoản.</param>
/// <param name="FamilyId">Phiên (family refresh token).</param>
/// <param name="Reason"><c>logout</c>, <c>reuse</c> hoặc <c>mfa</c>.</param>
public sealed record SessionRevoked(Guid AccountId, Guid FamilyId, string Reason) : IDomainEvent;

/// <summary>Một dòng của ảnh chụp phân công.</summary>
/// <param name="Role">Mã vai trò.</param>
/// <param name="Scope">Phạm vi.</param>
public sealed record AssignmentItem([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("scope")] string Scope);

/// <summary>Vai trò vừa được gán (audit: phân công trước/sau, spec §3.6).</summary>
/// <param name="Role">Mã vai trò.</param>
/// <param name="Scope">Phạm vi.</param>
/// <param name="Before">Phân công trước thay đổi.</param>
/// <param name="After">Phân công sau thay đổi.</param>
public sealed record RoleAssignmentGranted(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("before")] IReadOnlyList<AssignmentItem> Before,
    [property: JsonPropertyName("after")] IReadOnlyList<AssignmentItem> After) : IDomainEvent;

/// <summary>Phân công vừa bị thu hồi (audit: phân công trước/sau, spec §3.6).</summary>
/// <param name="Role">Mã vai trò.</param>
/// <param name="Scope">Phạm vi.</param>
/// <param name="Before">Phân công trước thay đổi.</param>
/// <param name="After">Phân công sau thay đổi.</param>
public sealed record RoleAssignmentRevoked(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("before")] IReadOnlyList<AssignmentItem> Before,
    [property: JsonPropertyName("after")] IReadOnlyList<AssignmentItem> After) : IDomainEvent;
