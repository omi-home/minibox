using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Domain.Aggregates.Categories;

namespace Minibox.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="Category"/> ↔ <c>category</c>, bản dịch ↔ <c>category_translation</c>, lịch sử slug ↔ <c>category_slug_history</c>.</summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.IsAggregateRoot();
        builder.ToTable("category");
        builder.Ignore(x => x.IsArchived);
        builder.OwnsMany(x => x.Translations, t =>
        {
            t.ToTable("category_translation");
            t.WithOwner().HasForeignKey("CategoryId");
            t.HasKey("CategoryId", nameof(CategoryTranslation.Locale));
        });
        builder.Navigation(x => x.Translations).HasField("_translations");
        builder.OwnsMany(x => x.SlugHistory, h =>
        {
            h.ToTable("category_slug_history");
            h.WithOwner().HasForeignKey("CategoryId");
            h.HasKey("CategoryId", nameof(CategorySlugRedirect.Locale), nameof(CategorySlugRedirect.Slug));
        });
        builder.Navigation(x => x.SlugHistory).HasField("_slugHistory");
    }
}
