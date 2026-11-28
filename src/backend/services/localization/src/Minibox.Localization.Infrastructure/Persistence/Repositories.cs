using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Aggregates.Messages;

namespace Minibox.Localization.Infrastructure.Persistence;

/// <summary>Repository <see cref="Locale"/>; khoá đổi fallback bằng <c>pg_advisory_xact_lock</c>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class LocaleRepository(LocalizationDbContext db) : EfRepository<Locale, string>(db), ILocaleRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string?>> LockFallbacksAsync(CancellationToken cancellationToken = default)
    {
        await Db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('localization.locale'))", cancellationToken);
        return await Set.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.FallbackCode, cancellationToken);
    }
}

/// <summary>Repository <see cref="UiMessage"/>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class UiMessageRepository(LocalizationDbContext db) : EfRepository<UiMessage, Guid>(db), IUiMessageRepository
{
    /// <inheritdoc/>
    public Task<UiMessage?> FindAsync(string @namespace, string key, string localeCode, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(m => m.Namespace == @namespace && m.Key == key && m.LocaleCode == localeCode, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UiMessage>> ListPendingAsync(string @namespace, string localeCode, CancellationToken cancellationToken = default) =>
        await Set.Where(m => m.Namespace == @namespace && m.LocaleCode == localeCode && (m.PublishedText == null || m.PublishedText != m.DraftText))
            .OrderBy(m => m.Key).ToListAsync(cancellationToken);
}
