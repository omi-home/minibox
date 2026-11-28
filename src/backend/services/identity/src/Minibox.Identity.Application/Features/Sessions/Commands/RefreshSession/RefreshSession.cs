using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Sessions.Commands.RefreshSession;

/// <summary>Lệnh đổi refresh token lấy cặp token mới cùng phiên (BFF gọi khi access token sắp hết hạn).</summary>
/// <param name="RefreshToken">Refresh token hiện tại.</param>
public sealed record RefreshSession(string? RefreshToken) : ICommand<TokenResponse>;

/// <summary>Xoay refresh token; token đã xoay bị đưa lại thì thu hồi cả phiên.</summary>
/// <param name="tokens">Repository refresh token.</param>
/// <param name="accounts">Repository tài khoản (loại tài khoản quyết định thời hạn phiên).</param>
/// <param name="sessions">Cấp access token.</param>
/// <param name="time">Đồng hồ.</param>
/// <remarks>Token nạp bằng <c>FOR UPDATE</c>: hai request song song cùng token thì request sau thấy token đã dùng và thu hồi cả phiên.</remarks>
public sealed class RefreshSessionHandler(IRefreshTokenRepository tokens, IAccountRepository accounts, SessionIssuer sessions, TimeProvider time)
    : ICommandHandler<RefreshSession, TokenResponse>
{
    /// <inheritdoc/>
    /// <returns>Cặp token mới; lỗi <see cref="IdentityErrors.InvalidRefreshToken"/> (401) với mọi trường hợp không hợp lệ.</returns>
    public async ValueTask<Result<TokenResponse>> Handle(RefreshSession command, CancellationToken cancellationToken)
    {
        if (command.RefreshToken is not { Length: > 0 and <= RefreshToken.MaxTokenLength } value)
            return IdentityErrors.InvalidRefreshToken;
        var now = time.GetUtcNow();
        var current = await tokens.GetForUpdateAsync(RefreshToken.Hash(value), cancellationToken);
        var account = current is null ? null : await accounts.GetAsync(current.AccountId, cancellationToken);
        if (current is null || account is null)
            return IdentityErrors.InvalidRefreshToken;

        var rotated = current.Rotate(account.Kind, now);
        if (rotated.IsFailure)
        {
            if (rotated.Error != IdentityErrors.RefreshTokenReused)
                return IdentityErrors.InvalidRefreshToken;
            await tokens.RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            account.RecordSessionRevoked(current.FamilyId, "reuse");
            return IdentityErrors.InvalidRefreshToken.WithCommit();
        }

        var (next, nextValue) = rotated.Value;
        await tokens.UpdateAsync(current, current.Version, cancellationToken);
        await tokens.AddAsync(next, cancellationToken);
        return await sessions.TokensAsync(next, nextValue, account.Kind, cancellationToken);
    }
}
