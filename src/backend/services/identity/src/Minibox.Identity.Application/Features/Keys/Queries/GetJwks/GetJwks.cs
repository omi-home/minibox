using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;

namespace Minibox.Identity.Application.Features.Keys.Queries.GetJwks;

/// <summary>Truy vấn JWKS công khai cho service khác tự kiểm chữ ký access token (chỉ trong mạng nội bộ).</summary>
public sealed record GetJwks : IQuery<object>;

/// <summary>Đọc khoá đang công bố.</summary>
/// <param name="keys">Key ring.</param>
public sealed class GetJwksHandler(ISigningKeys keys) : IQueryHandler<GetJwks, object>
{
    /// <inheritdoc/>
    public ValueTask<Result<object>> Handle(GetJwks query, CancellationToken cancellationToken) => ValueTask.FromResult<Result<object>>(keys.Jwks());
}
