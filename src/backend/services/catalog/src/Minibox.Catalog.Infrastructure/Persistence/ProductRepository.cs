using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Products;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Repository <see cref="Product"/>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class ProductRepository(CatalogDbContext db) : EfRepository<Product, Guid>(db), IProductRepository
{
    /// <inheritdoc/>
    /// <remarks>ponytail: kiểm trước khi ghi như <c>TaxonRepository</c>; hai lệnh song song cùng slug vẫn có thể vỡ unique index (500).</remarks>
    public async Task<bool> AnySlugTakenAsync(Guid productId, IReadOnlyCollection<ProductText> texts, CancellationToken cancellationToken = default)
    {
        var slugs = texts.Select(t => t.Slug).ToArray();
        var taken = await Set.AsNoTracking().Where(p => p.Id != productId).SelectMany(p => p.Translations)
            .Where(t => slugs.Contains(t.Slug)).Select(t => new { t.Locale, t.Slug }).ToListAsync(cancellationToken);
        return taken.Any(t => texts.Any(x => x.Locale == t.Locale && x.Slug == t.Slug));
    }
}
