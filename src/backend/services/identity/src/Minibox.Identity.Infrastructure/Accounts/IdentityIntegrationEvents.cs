using Minibox.BuildingBlocks.Domain;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Contracts.Events;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Ánh xạ domain event của identity sang integration event trong <c>Minibox.Identity.Contracts</c> để phát qua outbox.</summary>
public sealed class IdentityIntegrationEvents : IIntegrationEventMapper
{
    /// <inheritdoc/>
    public object? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        AccountRegistered e => new AccountRegisteredV1(e.AccountId, AccountRepository.KindText(e.Kind)),
        _ => null,
    };
}
