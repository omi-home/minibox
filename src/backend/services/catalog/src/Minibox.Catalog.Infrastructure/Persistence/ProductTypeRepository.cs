using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;
using Minibox.Catalog.Domain.Aggregates.Products;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Repository <see cref="ProductType"/>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class ProductTypeRepository(CatalogDbContext db) : EfRepository<ProductType, Guid>(db), IProductTypeRepository
{
    /// <inheritdoc/>
    public Task<bool> CodeTakenAsync(string code, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().AnyAsync(t => t.Code == code, cancellationToken);

    /// <inheritdoc/>
    public async Task<AttributeUsage[]> UsageAsync(Guid productTypeId, CancellationToken cancellationToken = default)
    {
        var attributeIds = Set.AsNoTracking().Where(t => t.Id == productTypeId).SelectMany(t => t.Attributes).Select(a => a.Id);
        return await Db.Set<Product>().AsNoTracking().SelectMany(p => p.AttributeValues).Where(v => attributeIds.Contains(v.AttributeId))
            .Select(v => new AttributeUsage(v.AttributeId, v.Value)).Distinct().ToArrayAsync(cancellationToken);
    }
}
