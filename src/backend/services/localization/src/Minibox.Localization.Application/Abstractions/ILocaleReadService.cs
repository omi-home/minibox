using Minibox.Localization.Application.Features.Locales.Queries.ListLocales;

namespace Minibox.Localization.Application.Abstractions;

/// <summary>Port đọc locale (phía query của CQRS): Infrastructure chiếu thẳng ra DTO, không qua repository.</summary>
public interface ILocaleReadService
{
    /// <summary>Liệt kê locale đang bật.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Locale đang bật, sắp theo thứ tự hiển thị rồi theo mã.</returns>
    Task<LocaleDto[]> ListEnabledAsync(CancellationToken cancellationToken = default);
}
