using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Domain.Aggregates.Products;

namespace Minibox.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="Product"/> ↔ <c>product</c>, bản dịch ↔ <c>product_translation</c>, lịch sử slug ↔ <c>product_slug_history</c>,
/// giá trị thuộc tính ↔ <c>product_attribute_value</c>, bộ sưu tập/thẻ ↔ <c>product_taxon</c>.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.IsAggregateRoot();
        builder.ToTable("product");
        builder.Ignore(x => x.TaxonIds);
        builder.OwnsMany(x => x.Translations, t =>
        {
            t.ToTable("product_translation");
            t.WithOwner().HasForeignKey("ProductId");
            t.HasKey("ProductId", nameof(ProductTranslation.Locale));
        });
        builder.Navigation(x => x.Translations).HasField("_translations");
        builder.OwnsMany(x => x.SlugHistory, h =>
        {
            h.ToTable("product_slug_history");
            h.WithOwner().HasForeignKey("ProductId");
            h.HasKey("ProductId", nameof(ProductSlugRedirect.Locale), nameof(ProductSlugRedirect.Slug));
        });
        builder.Navigation(x => x.SlugHistory).HasField("_slugHistory");
        builder.OwnsMany(x => x.AttributeValues, v =>
        {
            v.ToTable("product_attribute_value");
            v.WithOwner().HasForeignKey("ProductId");
            v.HasKey("ProductId", nameof(ProductAttributeValue.AttributeId), nameof(ProductAttributeValue.Value));
        });
        builder.Navigation(x => x.AttributeValues).HasField("_attributeValues");
        builder.OwnsMany(x => x.Taxa, t =>
        {
            t.ToTable("product_taxon");
            t.WithOwner().HasForeignKey("ProductId");
            t.HasKey("ProductId", nameof(ProductTaxonLink.TaxonId));
        });
        builder.Navigation(x => x.Taxa).HasField("_taxa");
    }
}
