using Minibox.Localization.Application.Features.Currencies.Queries.ListCurrencies;

namespace Minibox.Localization.Application.Abstractions;

/// <summary>Port đọc tiền tệ hiển thị và tỷ giá (phía query của CQRS).</summary>
public interface ICurrencyReadService
{
    /// <summary>Liệt kê tiền tệ đang bật kèm tỷ giá hiện hành (dòng <c>effective_at</c> mới nhất không ở tương lai).</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Tiền tệ đang bật, sắp theo thứ tự hiển thị rồi theo mã.</returns>
    Task<CurrencyDto[]> ListEnabledAsync(CancellationToken cancellationToken = default);
}
