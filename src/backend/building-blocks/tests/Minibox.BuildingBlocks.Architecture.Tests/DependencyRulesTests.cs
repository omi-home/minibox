using Minibox.Bad.Application;
using Minibox.Bad.Domain;
using Minibox.Bad.Infrastructure;

namespace Minibox.BuildingBlocks.Architecture.Tests;

/// <summary>Test ngược: fixture Minibox.Bad.* cố tình vi phạm thì luật phải báo.</summary>
public class DependencyRulesTests
{
    [Fact(DisplayName = "ac193_1 Domain tham chiếu Application làm luật báo vi phạm")]
    public void ac193_1_domain_referencing_application_is_violation()
    {
        var violations = DependencyRules.Violations("Minibox.Bad", typeof(DomainThing).Assembly, typeof(AppThing).Assembly);
        Assert.Contains(violations, v => v.Contains("Minibox.Bad.Domain.DomainThing", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ac193_2 Tham chiếu project không phải *.Contracts của service khác làm luật báo vi phạm")]
    public void ac193_2_cross_service_non_contracts_is_violation()
    {
        var violations = DependencyRules.Violations("Minibox.Bad", typeof(InfraThing).Assembly);
        Assert.Contains("Minibox.Bad.Infrastructure → Minibox.Other.Domain", violations);
    }

    [Fact(DisplayName = "Layer sạch không có vi phạm")]
    public void clean_layer_has_no_violation() =>
        Assert.Empty(DependencyRules.Violations("Minibox.Bad", typeof(AppThing).Assembly));
}
