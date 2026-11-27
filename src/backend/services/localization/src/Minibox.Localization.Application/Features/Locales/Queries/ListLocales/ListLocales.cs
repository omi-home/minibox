using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;

namespace Minibox.Localization.Application.Features.Locales.Queries.ListLocales;

/// <summary>Truy vấn các locale đang bật theo thứ tự hiển thị; dữ liệu công khai (shop, admin, BFF đều cần), không đòi quyền.</summary>
public sealed record ListLocales : IQuery<LocaleDto[]>;

/// <summary>Locale đang bật trả cho client.</summary>
/// <param name="Code">Mã BCP 47.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="FallbackCode">Locale rơi về khi thiếu bản dịch; <c>null</c> = <c>vi</c>.</param>
public sealed record LocaleDto(string Code, string Name, string? FallbackCode);

/// <summary>Đọc qua <see cref="ILocaleReadService"/> (không qua repository, không mở transaction).</summary>
/// <param name="reader">Port đọc locale.</param>
public sealed class ListLocalesHandler(ILocaleReadService reader) : IQueryHandler<ListLocales, LocaleDto[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<LocaleDto[]>> Handle(ListLocales query, CancellationToken cancellationToken) =>
        await reader.ListEnabledAsync(cancellationToken);
}
