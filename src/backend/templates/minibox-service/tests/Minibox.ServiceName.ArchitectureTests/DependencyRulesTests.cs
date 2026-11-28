using Minibox.BuildingBlocks.Architecture;

namespace Minibox.ServiceName.ArchitectureTests;

/// <summary>Luật phụ thuộc spec §5.1 (dùng chung từ Minibox.BuildingBlocks.Architecture).</summary>
public class DependencyRulesTests
{
    [Fact(DisplayName = "Phụ thuộc hướng vào trong; service khác chỉ qua *.Contracts")]
    public void dependency_rules_hold() =>
        Assert.Empty(DependencyRules.Violations("Minibox.ServiceName",
            typeof(Domain.AssemblyReference).Assembly,
            typeof(Application.AssemblyReference).Assembly,
            typeof(Infrastructure.AssemblyReference).Assembly,
            typeof(Contracts.AssemblyReference).Assembly,
            typeof(Program).Assembly));
}
