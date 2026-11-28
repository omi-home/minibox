using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;

namespace Minibox.Localization.Application.Features.Currencies.Queries.ListCurrencies;

/// <summary>Truy vấn tiền tệ hiển thị đang bật kèm tỷ giá hiện hành; dữ liệu công khai (shop, admin), không đòi quyền.</summary>
/// <remarks>Chỉ để hiển thị giá quy đổi ước tính; giao dịch luôn tính VND (spec §6.6).</remarks>
public sealed record ListCurrencies : IQuery<CurrencyDto[]>;

/// <summary>Tiền tệ hiển thị trả cho client.</summary>
/// <param name="Code">Mã ISO 4217.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Symbol">Ký hiệu.</param>
/// <param name="MinorUnit">Số chữ số thập phân khi hiển thị.</param>
/// <param name="VndPerUnit">Số đồng cho 1 đơn vị (VND = 1); <c>null</c> khi chưa có tỷ giá hiệu lực, client không hiện quy đổi.</param>
/// <param name="RateEffectiveAt">Thời điểm tỷ giá có hiệu lực; <c>null</c> với VND hoặc khi chưa có tỷ giá.</param>
public sealed record CurrencyDto(string Code, string Name, string Symbol, int MinorUnit, decimal? VndPerUnit, DateTimeOffset? RateEffectiveAt);

/// <summary>Đọc qua <see cref="ICurrencyReadService"/>.</summary>
/// <param name="reader">Port đọc tiền tệ.</param>
public sealed class ListCurrenciesHandler(ICurrencyReadService reader) : IQueryHandler<ListCurrencies, CurrencyDto[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<CurrencyDto[]>> Handle(ListCurrencies query, CancellationToken cancellationToken) =>
        await reader.ListEnabledAsync(cancellationToken);
}
