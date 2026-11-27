using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;

namespace Minibox.Localization.Application.Features.Locales.Queries.ListAllLocales;

/// <summary>Truy vấn mọi locale (kể cả đang tắt) cho màn quản trị ngôn ngữ.</summary>
public sealed record ListAllLocales : IQuery<LocaleAdminDto[]>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => LocalizationPermissions.LocaleManage;
}

/// <summary>Locale đầy đủ cho quản trị.</summary>
/// <param name="Code">Mã BCP 47.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="Enabled">Đang bật.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <c>vi</c>.</param>
/// <param name="Version">Phiên bản gửi lại khi sửa.</param>
public sealed record LocaleAdminDto(string Code, string Name, bool Enabled, int SortOrder, string? FallbackCode, long Version);

/// <summary>Đọc qua <see cref="ILocaleReadService"/>.</summary>
/// <param name="reader">Port đọc locale.</param>
public sealed class ListAllLocalesHandler(ILocaleReadService reader) : IQueryHandler<ListAllLocales, LocaleAdminDto[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<LocaleAdminDto[]>> Handle(ListAllLocales query, CancellationToken cancellationToken) =>
        await reader.ListAllAsync(cancellationToken);
}
