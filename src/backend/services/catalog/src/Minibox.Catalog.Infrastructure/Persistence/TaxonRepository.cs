using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Taxa;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Repository <see cref="Taxon"/>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class TaxonRepository(CatalogDbContext db) : EfRepository<Taxon, Guid>(db), ITaxonRepository
{
    /// <inheritdoc/>
    /// <remarks>ponytail: kiểm trước khi ghi như <c>CategoryRepository</c>; hai lệnh song song cùng slug vẫn có thể vỡ unique index (500).</remarks>
    public async Task<bool> AnySlugTakenAsync(Guid taxonId, IReadOnlyCollection<TaxonText> texts, CancellationToken cancellationToken = default)
    {
        var slugs = texts.Select(t => t.Slug).ToArray();
        var taken = await Set.AsNoTracking().Where(c => c.Id != taxonId).SelectMany(c => c.Translations)
            .Where(t => slugs.Contains(t.Slug)).Select(t => new { t.Locale, t.Slug }).ToListAsync(cancellationToken);
        return taken.Any(t => texts.Any(x => x.Locale == t.Locale && x.Slug == t.Slug));
    }

    /// <inheritdoc/>
    public Task<int> CountActiveAsync(string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().CountAsync(t => ids.Contains(t.Id) && t.Kind == kind && t.ArchivedAt == null, cancellationToken);

    /// <inheritdoc/>
    public Task<Guid[]> OfKindAsync(string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().Where(t => ids.Contains(t.Id) && t.Kind == kind).Select(t => t.Id).ToArrayAsync(cancellationToken);
}
