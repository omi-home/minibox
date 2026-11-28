using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Messy.Application.Features.Things;

namespace Minibox.Messy.Application.Misc;

/// <summary>Vi phạm cố ý: handler ngoài Application.Features.</summary>
public sealed class StrayHandler : ICommandHandler<DoThing>
{
    /// <inheritdoc/>
    public ValueTask<Result> Handle(DoThing command, CancellationToken cancellationToken) => new(Result.Success());
}
