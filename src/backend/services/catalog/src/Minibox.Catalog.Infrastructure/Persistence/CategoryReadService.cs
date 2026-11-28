using Microsoft.EntityFrameworkCore;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Application.Features.Categories;
using Minibox.Catalog.Domain.Aggregates.Categories;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Read service danh mục: LINQ <c>AsNoTracking</c> + <c>Select</c> chiếu thẳng ra DTO.</summary>
/// <param name="db">DbContext của request.</param>
public sealed class CategoryReadService(CatalogDbContext db) : ICategoryReadService
{
    /// <inheritdoc/>
    public async Task<CategoryAdminRow[]> ListAllAsync(CancellationToken cancellationToken = default) =>
        await db.Set<Category>().AsNoTracking()
            .Select(c => new CategoryAdminRow(c.Id, c.ParentId, c.SortOrder, c.ArchivedAt != null, c.Version,
                c.Translations.OrderBy(t => t.Locale).Select(t => new CategoryTranslationDto(t.Locale, t.Name, t.Description, t.Slug)).ToArray()))
            .ToArrayAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<CategoryTextRow[]> ListActiveAsync(IReadOnlyList<string> locales, CancellationToken cancellationToken = default) =>
        await db.Set<Category>().AsNoTracking().Where(c => c.ArchivedAt == null)
            .SelectMany(c => c.Translations.Where(t => locales.Contains(t.Locale)),
                (c, t) => new CategoryTextRow(c.Id, c.ParentId, c.SortOrder, t.Locale, t.Name, t.Description, t.Slug))
            .ToArrayAsync(cancellationToken);
}
