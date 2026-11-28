using Minibox.BuildingBlocks.Domain;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Localization.Contracts.Events;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Aggregates.Messages;

namespace Minibox.Localization.Infrastructure.Messaging;

/// <summary>Ánh xạ domain event của localization sang integration event trong <c>Minibox.Localization.Contracts</c> để phát qua outbox.</summary>
public sealed class LocalizationIntegrationEvents : IIntegrationEventMapper
{
    /// <inheritdoc/>
    public object? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        UiMessagePublished e => new MessagesPublishedV1(e.LocaleCode, e.Namespace, [e.Key]),
        LocaleChanged e => new LocaleChangedV1(e.Code, e.Name, e.Enabled, e.SortOrder, e.FallbackCode),
        _ => null,
    };
}
