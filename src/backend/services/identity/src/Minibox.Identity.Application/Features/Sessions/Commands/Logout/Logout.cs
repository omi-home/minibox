using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;

namespace Minibox.Identity.Application.Features.Sessions.Commands.Logout;

/// <summary>Lệnh đăng xuất: thu hồi cả phiên chứa refresh token.</summary>
/// <param name="RefreshToken">Refresh token hiện tại; thiếu hoặc không tồn tại thì không làm gì.</param>
public sealed record Logout(string? RefreshToken) : ICommand;

/// <summary>Thu hồi phiên và ghi audit.</summary>
/// <param name="tokens">Repository refresh token.</param>
/// <param name="accounts">Repository tài khoản (nhận sự kiện audit).</param>
/// <param name="time">Đồng hồ.</param>
public sealed class LogoutHandler(IRefreshTokenRepository tokens, IAccountRepository accounts, TimeProvider time) : ICommandHandler<Logout>
{
    /// <inheritdoc/>
    /// <returns>Luôn thành công (không lộ token có tồn tại hay không).</returns>
    public async ValueTask<Result> Handle(Logout command, CancellationToken cancellationToken)
    {
        if (command.RefreshToken is not { Length: > 0 and <= RefreshToken.MaxTokenLength } value
            || await tokens.GetAsync(RefreshToken.Hash(value), cancellationToken) is not { RevokedAt: null } token)
            return Result.Success();
        await tokens.RevokeFamilyAsync(token.FamilyId, time.GetUtcNow(), cancellationToken);
        (await accounts.GetAsync(token.AccountId, cancellationToken))?.RecordSessionRevoked(token.FamilyId, "logout");
        return Result.Success();
    }
}
