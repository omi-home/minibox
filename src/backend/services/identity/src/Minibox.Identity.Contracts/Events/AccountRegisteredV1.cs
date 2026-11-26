namespace Minibox.Identity.Contracts.Events;

/// <summary>Integration event <c>identity.AccountRegistered.v1</c>: tài khoản vừa được tạo.</summary>
/// <param name="AccountId">Id tài khoản (UUIDv7).</param>
/// <param name="Kind">Loại tài khoản: <c>customer</c> hoặc <c>staff</c>.</param>
/// <remarks>Hợp đồng là <c>contracts/events/identity.AccountRegistered.v1.json</c>; đổi phá vỡ thì tạo <c>V2</c> và schema mới, không sửa V1.
/// Không mang PII.</remarks>
public sealed record AccountRegisteredV1(Guid AccountId, string Kind);
