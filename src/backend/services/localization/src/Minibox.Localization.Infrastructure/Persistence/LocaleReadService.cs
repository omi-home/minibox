using Microsoft.EntityFrameworkCore;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Application.Features.Locales.Queries.ListAllLocales;
using Minibox.Localization.Application.Features.Locales.Queries.ListLocales;
using Minibox.Localization.Domain.Aggregates.Locales;

namespace Minibox.Localization.Infrastructure.Persistence;

/// <summary>Đọc locale bằng LINQ <c>AsNoTracking</c> + <c>Select</c> ra DTO (không tải entity).</summary>
/// <param name="db">DbContext của request.</param>
public sealed class LocaleReadService(LocalizationDbContext db) : ILocaleReadService
{
    /// <inheritdoc/>
    public async Task<LocaleDto[]> ListEnabledAsync(CancellationToken cancellationToken = default) =>
        await db.Set<Locale>().AsNoTracking().Where(l => l.Enabled).OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .Select(l => new LocaleDto(l.Id, l.Name, l.FallbackCode)).ToArrayAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<LocaleAdminDto[]> ListAllAsync(CancellationToken cancellationToken = default) =>
        await db.Set<Locale>().AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .Select(l => new LocaleAdminDto(l.Id, l.Name, l.Enabled, l.SortOrder, l.FallbackCode, l.Version)).ToArrayAsync(cancellationToken);
}
