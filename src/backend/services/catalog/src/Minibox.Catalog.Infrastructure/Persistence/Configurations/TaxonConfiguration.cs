using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Domain.Aggregates.Taxa;

namespace Minibox.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="Taxon"/> ↔ <c>taxon</c>, bản dịch ↔ <c>taxon_translation</c>, lịch sử slug ↔ <c>taxon_slug_history</c>.</summary>
public sealed class TaxonConfiguration : IEntityTypeConfiguration<Taxon>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Taxon> builder)
    {
        builder.IsAggregateRoot();
        builder.ToTable("taxon");
        builder.Ignore(x => x.IsArchived);
        builder.OwnsMany(x => x.Translations, t =>
        {
            t.ToTable("taxon_translation");
            t.WithOwner().HasForeignKey("TaxonId");
            t.HasKey("TaxonId", nameof(TaxonTranslation.Locale));
        });
        builder.Navigation(x => x.Translations).HasField("_translations");
        builder.OwnsMany(x => x.SlugHistory, h =>
        {
            h.ToTable("taxon_slug_history");
            h.WithOwner().HasForeignKey("TaxonId");
            h.HasKey("TaxonId", nameof(TaxonSlugRedirect.Locale), nameof(TaxonSlugRedirect.Slug));
        });
        builder.Navigation(x => x.SlugHistory).HasField("_slugHistory");
    }
}
