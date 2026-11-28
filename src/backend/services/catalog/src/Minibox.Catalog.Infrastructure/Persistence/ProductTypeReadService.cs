using Microsoft.EntityFrameworkCore;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.ProductTypes;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Read service nhóm sản phẩm: LINQ <c>AsNoTracking</c> + <c>Select</c> chiếu thẳng ra DTO.</summary>
/// <param name="db">DbContext của request.</param>
public sealed class ProductTypeReadService(CatalogDbContext db) : IProductTypeReadService
{
    /// <inheritdoc/>
    public async Task<ProductTypeAdminRow[]> ListAllAsync(CancellationToken cancellationToken = default) =>
        await db.Set<ProductType>().AsNoTracking().AsSplitQuery()
            .Select(x => new ProductTypeAdminRow(x.Id, x.Code, x.SortOrder, x.ArchivedAt != null, x.Version,
                x.Translations.OrderBy(t => t.Locale).Select(t => new LabelDto(t.Locale, t.Name)).ToArray(),
                x.Attributes.OrderBy(a => a.SortOrder).Select(a => new ProductTypeAttributeRow(a.Id, a.Code, a.DataType, a.Unit, a.Required, a.Variant,
                    a.Translations.OrderBy(t => t.Locale).Select(t => new LabelDto(t.Locale, t.Label)).ToArray(),
                    a.Values.OrderBy(v => v.SortOrder).Select(v => new ProductTypeValueRow(v.Id, v.Code,
                        v.Translations.OrderBy(t => t.Locale).Select(t => new LabelDto(t.Locale, t.Label)).ToArray())).ToArray())).ToArray()))
            .ToArrayAsync(cancellationToken);
}
