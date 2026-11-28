using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Localization.Domain.Aggregates.Locales;

namespace Minibox.Localization.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="Locale"/> ↔ bảng <c>locale</c> (khoá chính là cột <c>code</c>).</summary>
public sealed class LocaleConfiguration : IEntityTypeConfiguration<Locale>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Locale> builder)
    {
        builder.IsAggregateRoot();
        builder.Property(x => x.Id).HasColumnName("code");
    }
}
