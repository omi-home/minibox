using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Application.Abstractions;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Application.Features.Accounts.Queries.GetMyPreferences;

/// <summary>Truy vấn tùy chọn hiển thị của người đang đăng nhập.</summary>
public sealed record GetMyPreferences : IQuery<PreferencesDto>;

/// <summary>Tùy chọn hiển thị của tài khoản.</summary>
/// <param name="Locale">Locale BCP 47 đã chọn; <c>null</c> = chưa chọn (BFF theo cookie/<c>Accept-Language</c>).</param>
/// <param name="TimeZone">Múi giờ IANA.</param>
public sealed record PreferencesDto(string? Locale, string TimeZone);

/// <summary>Đọc qua <see cref="IAccountPreferencesReadService"/> theo <c>sub</c> của phiên.</summary>
/// <param name="user">Người gọi.</param>
/// <param name="reader">Port đọc tùy chọn.</param>
public sealed class GetMyPreferencesHandler(ICurrentUser user, IAccountPreferencesReadService reader) : IQueryHandler<GetMyPreferences, PreferencesDto>
{
    /// <inheritdoc/>
    /// <returns>Tùy chọn; lỗi <see cref="IdentityErrors.Unauthenticated"/> khi không có phiên hoặc tài khoản không còn.</returns>
    public async ValueTask<Result<PreferencesDto>> Handle(GetMyPreferences query, CancellationToken cancellationToken) =>
        Guid.TryParse(user.UserId, out var id) && await reader.GetAsync(id, cancellationToken) is { } dto
            ? dto
            : IdentityErrors.Unauthenticated;
}
