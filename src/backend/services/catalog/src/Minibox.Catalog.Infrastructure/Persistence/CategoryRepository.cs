using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Categories;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>Repository <see cref="Category"/>; khoá thay đổi cấu trúc cây bằng <c>pg_advisory_xact_lock</c>.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class CategoryRepository(CatalogDbContext db) : EfRepository<Category, Guid>(db), ICategoryRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<CategoryNode>> LockTreeAsync(CancellationToken cancellationToken = default)
    {
        await Db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext('catalog.category'))", cancellationToken);
        return await Set.AsNoTracking().Select(c => new CategoryNode(c.Id, c.ParentId, c.ArchivedAt != null)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>ponytail: kiểm trước khi ghi; hai lệnh song song cùng slug vẫn có thể lọt và một lệnh vỡ unique index (500).
    /// Bắt lỗi unique <c>category_translation_slug</c> thành 409 nếu điều này xảy ra thật.</remarks>
    public async Task<bool> AnySlugTakenAsync(Guid categoryId, IReadOnlyCollection<CategoryText> texts, CancellationToken cancellationToken = default)
    {
        var slugs = texts.Select(t => t.Slug).ToArray();
        var taken = await Set.AsNoTracking().Where(c => c.Id != categoryId).SelectMany(c => c.Translations)
            .Where(t => slugs.Contains(t.Slug)).Select(t => new { t.Locale, t.Slug }).ToListAsync(cancellationToken);
        return taken.Any(t => texts.Any(x => x.Locale == t.Locale && x.Slug == t.Slug));
    }
}
