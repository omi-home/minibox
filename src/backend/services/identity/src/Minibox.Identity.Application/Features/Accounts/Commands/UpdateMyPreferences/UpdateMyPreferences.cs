using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Accounts.Commands.UpdateMyPreferences;

/// <summary>Lệnh đổi locale và múi giờ của người đang đăng nhập (tài khoản lấy từ <c>sub</c>, không từ body).</summary>
/// <param name="Locale">Locale BCP 47; rỗng/<c>null</c> = bỏ chọn.</param>
/// <param name="TimeZone">Mã múi giờ IANA.</param>
public sealed record UpdateMyPreferences(string? Locale, string TimeZone) : ICommand;

/// <summary>Nạp tài khoản, đổi tùy chọn trên aggregate và lưu qua repository.</summary>
/// <param name="user">Người gọi.</param>
/// <param name="accounts">Repository tài khoản.</param>
public sealed class UpdateMyPreferencesHandler(ICurrentUser user, IAccountRepository accounts) : ICommandHandler<UpdateMyPreferences>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="IdentityErrors.Unauthenticated"/>, <see cref="IdentityErrors.InvalidLocale"/> hoặc
    /// <see cref="IdentityErrors.InvalidTimeZone"/>.</returns>
    /// <remarks>Ghi đè theo lần gửi sau cùng (tùy chọn của chính mình, không cần version).</remarks>
    public async ValueTask<Result> Handle(UpdateMyPreferences command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.UserId, out var id) || await accounts.GetForUpdateAsync(id, cancellationToken) is not { } account)
            return IdentityErrors.Unauthenticated;
        var changed = account.ChangePreferences(command.Locale, command.TimeZone);
        if (changed.IsFailure)
            return changed;
        await accounts.UpdateAsync(account, account.Version, cancellationToken);
        return Result.Success();
    }
}
