using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Repository <see cref="ProductType"/>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class ProductTypeRepository(CatalogDbContext db) : EfRepository<ProductType, Guid>(db), IProductTypeRepository
{
    /// <inheritdoc/>
    public Task<bool> CodeTakenAsync(string code, CancellationToken cancellationToken = default) =>
        Set.AsNoTracking().AnyAsync(t => t.Code == code, cancellationToken);
}
