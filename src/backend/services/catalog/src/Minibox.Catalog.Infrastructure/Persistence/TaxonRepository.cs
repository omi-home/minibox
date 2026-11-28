using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Taxa;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Repository <see cref="Taxon"/>; liên kết sản phẩm ghi thẳng <c>product_taxon</c> (chưa có aggregate Product).</summary>
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
    public async Task ReplaceProductTaxaAsync(Guid productId, string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        // product_taxon không phải entity của aggregate nào nên LINQ không biểu diễn được; SQL có tham số.
        var key = $"catalog.product_taxon:{productId}";
        var array = ids.ToArray();
        await Db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
        await Db.Database.ExecuteSqlAsync(
            $"DELETE FROM product_taxon p USING taxon t WHERE p.taxon_id = t.id AND t.kind = {kind} AND p.product_id = {productId}", cancellationToken);
        await Db.Database.ExecuteSqlAsync(
            $"INSERT INTO product_taxon (product_id, taxon_id) SELECT {productId}, unnest({array})", cancellationToken);
    }
}
