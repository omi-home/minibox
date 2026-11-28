using Minibox.Bad.Application;

namespace Minibox.Bad.Domain;

/// <summary>Vi phạm cố ý: Domain phụ thuộc Application.</summary>
public sealed class DomainThing
{
    /// <summary>Tham chiếu ra layer ngoài.</summary>
    public AppThing? Leak { get; init; }
}
