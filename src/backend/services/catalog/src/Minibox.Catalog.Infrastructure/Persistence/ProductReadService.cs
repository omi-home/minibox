using Microsoft.EntityFrameworkCore;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.Products;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;
using Minibox.Catalog.Domain.Aggregates.Products;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Read service sản phẩm: LINQ <c>AsNoTracking</c> + <c>Select</c> chiếu thẳng ra DTO.</summary>
/// <param name="db">DbContext của request.</param>
public sealed class ProductReadService(CatalogDbContext db) : IProductReadService
{
    /// <inheritdoc/>
    public async Task<ProductAdminDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await db.Set<Product>().AsNoTracking().AsSplitQuery().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.ProductTypeId, x.CategoryId, x.Status, x.Version,
                Translations = x.Translations.OrderBy(t => t.Locale)
                    .Select(t => new ProductTranslationDto(t.Locale, t.Name, t.Description, t.Slug, t.SeoTitle, t.SeoDescription)).ToArray(),
                Values = x.AttributeValues.Select(v => new { v.AttributeId, v.Value }).ToArray(),
                TaxonIds = x.Taxa.Select(t => t.TaxonId).ToArray(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (p is null)
            return null;
        // Mã và thứ tự thuộc tính lấy từ nhóm hiện tại của sản phẩm (giá trị chỉ thuộc nhóm này).
        var attributes = await db.Set<ProductType>().AsNoTracking().Where(t => t.Id == p.ProductTypeId).SelectMany(t => t.Attributes)
            .Select(a => new { a.Id, a.Code, a.SortOrder }).ToArrayAsync(cancellationToken);
        var values = attributes.OrderBy(a => a.SortOrder)
            .Select(a => new ProductAttributeDto(a.Code, p.Values.Where(v => v.AttributeId == a.Id).Select(v => v.Value).Order(StringComparer.Ordinal).ToArray()))
            .Where(a => a.Values.Length > 0).ToArray();
        return new ProductAdminDetail(p.Id, p.ProductTypeId, p.CategoryId, p.Status, p.Version, p.Translations, values, p.TaxonIds);
    }
}
