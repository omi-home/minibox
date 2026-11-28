using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Localization.Domain.Aggregates.Messages;

namespace Minibox.Localization.Infrastructure.Persistence.Configurations;

/// <summary>Mapping <see cref="UiMessage"/> ↔ bảng <c>ui_message</c>.</summary>
public sealed class UiMessageConfiguration : IEntityTypeConfiguration<UiMessage>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UiMessage> builder)
    {
        builder.IsAggregateRoot();
        builder.ToTable("ui_message");
        builder.Ignore(x => x.HasPendingChanges);
    }
}
