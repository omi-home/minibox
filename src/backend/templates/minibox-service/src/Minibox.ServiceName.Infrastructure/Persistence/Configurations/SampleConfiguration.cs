using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.ServiceName.Domain.Samples;

namespace Minibox.ServiceName.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="Sample"/> ↔ bảng <c>sample</c> (tên snake_case theo quy ước).</summary>
public sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Sample> builder) => builder.IsAggregateRoot();
}
