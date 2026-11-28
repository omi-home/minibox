using Minibox.BuildingBlocks.Domain;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Contracts.Events;
using Minibox.Identity.Domain.Aggregates.Accounts;

namespace Minibox.Identity.Infrastructure.Messaging;

/// <summary>Ánh xạ domain event của identity sang integration event trong <c>Minibox.Identity.Contracts</c> để phát qua outbox.</summary>
public sealed class IdentityIntegrationEvents : IIntegrationEventMapper
{
    /// <inheritdoc/>
    public object? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountRegistered e => new AccountRegisteredV1(e.AccountId, e.Kind.ToCode()),
        _ => null,
    };
}
