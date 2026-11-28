using Microsoft.EntityFrameworkCore;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.Taxa;
using Minibox.Catalog.Domain.Aggregates.Taxa;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Read service bộ sưu tập/thẻ: LINQ <c>AsNoTracking</c> + <c>Select</c> chiếu thẳng ra DTO.</summary>
/// <param name="db">DbContext của request.</param>
public sealed class TaxonReadService(CatalogDbContext db) : ITaxonReadService
{
    /// <inheritdoc/>
    public async Task<TaxonAdminRow[]> ListAllAsync(string kind, CancellationToken cancellationToken = default) =>
        await db.Set<Taxon>().AsNoTracking().Where(x => x.Kind == kind)
            .Select(x => new TaxonAdminRow(x.Id, x.SortOrder, x.ArchivedAt != null, x.Version,
                x.Translations.OrderBy(t => t.Locale).Select(t => new TaxonTranslationDto(t.Locale, t.Name, t.Description, t.Slug)).ToArray()))
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<TaxonTextRow[]> ListActiveAsync(string kind, IReadOnlyList<string> locales, CancellationToken cancellationToken = default) =>
        await db.Set<Taxon>().AsNoTracking().Where(x => x.Kind == kind && x.ArchivedAt == null)
            .SelectMany(x => x.Translations.Where(t => locales.Contains(t.Locale)),
                (x, t) => new TaxonTextRow(x.Id, x.SortOrder, t.Locale, t.Name, t.Description, t.Slug))
            .ToArrayAsync(cancellationToken);
}
