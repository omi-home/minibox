using Microsoft.EntityFrameworkCore;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Application.Features.Currencies.Queries.ListCurrencies;

namespace Minibox.Localization.Infrastructure.Persistence;

/// <summary>Đọc tiền tệ và tỷ giá hiện hành.</summary>
/// <param name="db">DbContext của request.</param>
/// <param name="time">Đồng hồ hệ thống (mốc "hiện hành" của tỷ giá).</param>
/// <remarks>Chưa có aggregate tiền tệ/tỷ giá (chỉ đọc) nên dùng <c>SqlQuery</c> có tham số với LATERAL lấy dòng mới nhất;
/// chuyển sang LINQ khi có entity (màn quản trị tỷ giá).</remarks>
public sealed class CurrencyReadService(LocalizationDbContext db, TimeProvider time) : ICurrencyReadService
{
    /// <inheritdoc/>
    public async Task<CurrencyDto[]> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        return await db.Database.SqlQuery<CurrencyDto>($"""
            SELECT c.code AS "Code", c.name AS "Name", c.symbol AS "Symbol", c.minor_unit::int AS "MinorUnit",
                   CASE WHEN c.code = 'VND' THEN 1 ELSE r.vnd_per_unit END AS "VndPerUnit", r.effective_at AS "RateEffectiveAt"
            FROM localization.currency c
            LEFT JOIN LATERAL (
              SELECT vnd_per_unit, effective_at FROM localization.exchange_rate
              WHERE currency_code = c.code AND effective_at <= {now}
              ORDER BY effective_at DESC LIMIT 1) r ON true
            WHERE c.enabled
            ORDER BY c.sort_order, c.code
            """).ToArrayAsync(cancellationToken);
    }
}
