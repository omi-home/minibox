using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Errors;
using Minibox.Identity.Domain.Services;

namespace Minibox.Identity.Application.Features.Assignments.Commands.GrantAssignment;

/// <summary>Lệnh gán vai trò trong phạm vi cho tài khoản.</summary>
/// <param name="AccountId">Tài khoản được gán.</param>
/// <param name="Role">Mã vai trò.</param>
/// <param name="Scope">Phạm vi <c>*</c>, <c>wh:n</c> hoặc <c>farm:n</c>.</param>
public sealed record GrantAssignment(Guid AccountId, string Role, string Scope) : ICommand;

/// <summary>Chống leo thang đặc quyền rồi gán qua aggregate <c>Account</c> (khoá dòng); audit trước/sau đi qua Unit of Work,
/// trigger DB thu hồi refresh token của người được gán.</summary>
/// <param name="user">Người gọi.</param>
/// <param name="authorizer">Bộ kiểm quyền.</param>
/// <param name="accounts">Repository tài khoản.</param>
/// <param name="roles">Danh mục vai trò.</param>
public sealed class GrantAssignmentHandler(ICurrentUser user, IAuthorizer authorizer, IAccountRepository accounts, IRoleRepository roles)
    : ICommandHandler<GrantAssignment>
{
    /// <inheritdoc/>
    /// <returns>Thành công (kể cả khi đã có phân công); lỗi Forbidden, <see cref="IdentityErrors.AssignmentInvalidScope"/>,
    /// <see cref="IdentityErrors.AssignmentKindMismatch"/> hoặc <see cref="IdentityErrors.AssignmentNotFound"/>.</returns>
    public async ValueTask<Result> Handle(GrantAssignment command, CancellationToken cancellationToken)
    {
        var role = await roles.GetByCodeAsync(command.Role, cancellationToken);
        var actor = await AssignmentAccess.EnsureAsync(user, authorizer, command.AccountId, role, command.Scope, cancellationToken);
        if (actor.IsFailure)
            return actor.Error;
        if (!AssignmentPolicy.IsValidScope(command.Scope))
            return IdentityErrors.AssignmentInvalidScope;
        var account = await accounts.GetForUpdateAsync(command.AccountId, cancellationToken);
        if (account is null || role is null)
            return IdentityErrors.AssignmentNotFound;

        var granted = account.Grant(role, command.Scope, actor.Value, await roles.CodesAsync(cancellationToken));
        if (granted.IsFailure)
            return granted.Error;
        return granted.Value ? await accounts.UpdateAsync(account, account.Version, cancellationToken) : Result.Success();
    }
}
