using Mediator;
using Minibox.Messy.Application.Features.Things;

namespace Minibox.Messy.Api.Helpers;

/// <summary>Vi phạm cố ý: type Api ngoài Endpoints/Program gửi query.</summary>
public static class Shortcut
{
    /// <summary>Gửi query.</summary>
    /// <param name="sender">Mediator.</param>
    /// <returns>Kết quả query.</returns>
    public static ValueTask<Minibox.BuildingBlocks.Domain.Result<int>> Run(ISender sender) => sender.Send(new GetThing());
}
