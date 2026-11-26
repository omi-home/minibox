using System.Reflection;
using NetArchTest.Rules;

namespace Minibox.ServiceName.ArchitectureTests;

/// <summary>Luật phụ thuộc spec §5.1: phụ thuộc hướng vào trong; service khác chỉ qua *.Contracts.</summary>
public class DependencyRulesTests
{
    const string Svc = "Minibox.ServiceName";

    static readonly Assembly[] Layers =
    [
        typeof(Domain.AssemblyReference).Assembly,
        typeof(Application.AssemblyReference).Assembly,
        typeof(Infrastructure.AssemblyReference).Assembly,
        typeof(Contracts.AssemblyReference).Assembly,
        typeof(Program).Assembly,
    ];

    public static TheoryData<string, string[]> Forbidden => new()
    {
        { "Domain", [$"{Svc}.Application", $"{Svc}.Infrastructure", $"{Svc}.Api", $"{Svc}.Contracts",
                     "Minibox.BuildingBlocks.Application", "Minibox.BuildingBlocks.Infrastructure", "Minibox.BuildingBlocks.Web"] },
        { "Application", [$"{Svc}.Infrastructure", $"{Svc}.Api", "Minibox.BuildingBlocks.Infrastructure", "Minibox.BuildingBlocks.Web"] },
        { "Infrastructure", [$"{Svc}.Api", "Minibox.BuildingBlocks.Web"] },
        { "Contracts", [$"{Svc}.Domain", $"{Svc}.Application", $"{Svc}.Infrastructure", $"{Svc}.Api", "Minibox.BuildingBlocks"] },
    };

    [Theory(DisplayName = "Layer không phụ thuộc layer ngoài")]
    [MemberData(nameof(Forbidden))]
    public void layer_does_not_depend_outward(string layer, string[] forbidden)
    {
        var result = Types.InAssembly(Layers.Single(a => a.GetName().Name == $"{Svc}.{layer}"))
            .ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful, $"{layer} phụ thuộc sai: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact(DisplayName = "Chỉ tham chiếu service khác qua *.Contracts")]
    public void other_services_only_via_contracts()
    {
        var violations = Layers
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => $"{a.GetName().Name} → {r.Name}"))
            .Where(edge => edge.Split(" → ")[1] is var target
                           && target.StartsWith("Minibox.", StringComparison.Ordinal)
                           && !target.StartsWith($"{Svc}.", StringComparison.Ordinal)
                           && !target.StartsWith("Minibox.BuildingBlocks.", StringComparison.Ordinal)
                           && !target.EndsWith(".Contracts", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(violations);
    }
}
