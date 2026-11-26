using Minibox.Other.Domain;

namespace Minibox.Bad.Infrastructure;

/// <summary>Vi phạm cố ý: tham chiếu Domain của service khác thay vì *.Contracts.</summary>
public sealed class InfraThing
{
    /// <summary>Tham chiếu chéo service.</summary>
    public OtherThing? Leak { get; init; }
}
