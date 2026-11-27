using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Assignments.Commands.RevokeAssignment;

/// <summary>Lệnh thu hồi một phân công.</summary>
/// <param name="AccountId">Tài khoản bị thu hồi.</param>
/// <param name="Role">Mã vai trò.</param>
/// <param name="Scope">Phạm vi của phân công.</param>
public sealed record RevokeAssignment(Guid AccountId, string Role, string Scope) : ICommand;

/// <summary>Kiểm dạng vai trò và phạm vi trước khi mở transaction; cú pháp phạm vi chi tiết do <c>AssignmentPolicy</c> kiểm trong handler.</summary>
public sealed class RevokeAssignmentValidator : AbstractValidator<RevokeAssignment>
{
    /// <summary>Khai báo luật: <c>Role</c> và <c>Scope</c> bắt buộc, tối đa <see cref="AssignmentAccess.MaxCodeLength"/> ký tự.</summary>
    public RevokeAssignmentValidator()
    {
        RuleFor(x => x.Role).NotEmpty().MaximumLength(AssignmentAccess.MaxCodeLength);
        RuleFor(x => x.Scope).NotEmpty().MaximumLength(AssignmentAccess.MaxCodeLength);
    }
}

/// <summary>Chống leo thang đặc quyền rồi thu hồi qua aggregate <c>Account</c> (khoá dòng); audit trước/sau đi qua Unit of Work.</summary>
/// <param name="user">Người gọi.</param>
/// <param name="authorizer">Bộ kiểm quyền.</param>
/// <param name="accounts">Repository tài khoản.</param>
/// <param name="roles">Danh mục vai trò.</param>
public sealed class RevokeAssignmentHandler(ICurrentUser user, IAuthorizer authorizer, IAccountRepository accounts, IRoleRepository roles)
    : ICommandHandler<RevokeAssignment>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi Forbidden hoặc <see cref="IdentityErrors.AssignmentNotFound"/>.</returns>
    public async ValueTask<Result> Handle(RevokeAssignment command, CancellationToken cancellationToken)
    {
        var role = await roles.GetByCodeAsync(command.Role, cancellationToken);
        var actor = await AssignmentAccess.EnsureAsync(user, authorizer, command.AccountId, role, command.Scope, cancellationToken);
        if (actor.IsFailure)
            return actor.Error;
        var account = await accounts.GetForUpdateAsync(command.AccountId, cancellationToken);
        if (account is null || role is null)
            return IdentityErrors.AssignmentNotFound;

        var revoked = account.Revoke(role, command.Scope, await roles.CodesAsync(cancellationToken));
        return revoked.IsFailure ? revoked : await accounts.UpdateAsync(account, account.Version, cancellationToken);
    }
}
