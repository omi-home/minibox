using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;

namespace Minibox.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="ProductType"/> ↔ <c>product_type</c> (+ <c>_translation</c>), thuộc tính ↔ <c>attribute</c> (+ <c>_translation</c>),
/// giá trị cho phép ↔ <c>attribute_value</c> (+ <c>_translation</c>).</summary>
public sealed class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProductType> builder)
    {
        builder.IsAggregateRoot();
        builder.ToTable("product_type");
        builder.Ignore(x => x.IsArchived);
        builder.OwnsMany(x => x.Translations, t =>
        {
            t.ToTable("product_type_translation");
            t.WithOwner().HasForeignKey("ProductTypeId");
            t.HasKey("ProductTypeId", nameof(ProductTypeTranslation.Locale));
        });
        builder.Navigation(x => x.Translations).HasField("_translations");
        builder.OwnsMany(x => x.Attributes, a =>
        {
            a.ToTable("attribute");
            a.WithOwner().HasForeignKey("ProductTypeId");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).ValueGeneratedNever();
            a.OwnsMany(x => x.Translations, t =>
            {
                t.ToTable("attribute_translation");
                t.WithOwner().HasForeignKey("AttributeId");
                t.HasKey("AttributeId", nameof(AttributeTranslation.Locale));
            });
            a.Navigation(x => x.Translations).HasField("_translations");
            a.OwnsMany(x => x.Values, v =>
            {
                v.ToTable("attribute_value");
                v.WithOwner().HasForeignKey("AttributeId");
                v.HasKey(x => x.Id);
                v.Property(x => x.Id).ValueGeneratedNever();
                v.OwnsMany(x => x.Translations, t =>
                {
                    t.ToTable("attribute_value_translation");
                    t.WithOwner().HasForeignKey("AttributeValueId");
                    t.HasKey("AttributeValueId", nameof(AttributeValueTranslation.Locale));
                });
                v.Navigation(x => x.Translations).HasField("_translations");
            });
            a.Navigation(x => x.Values).HasField("_values");
        });
        builder.Navigation(x => x.Attributes).HasField("_attributes");
    }
}
