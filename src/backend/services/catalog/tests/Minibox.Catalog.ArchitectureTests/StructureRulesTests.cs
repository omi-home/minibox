using Minibox.BuildingBlocks.Architecture;

namespace Minibox.Catalog.ArchitectureTests;

/// <summary>Luật cấu trúc trong layer spec §5.2 (dùng chung từ Minibox.BuildingBlocks.Architecture).</summary>
public class StructureRulesTests
{
    [Fact(DisplayName = "Program chỉ là composition root, endpoint ở Api/Endpoints, handler ở Application/Features, Infrastructure chỉ cài port")]
    public void structure_rules_hold() =>
        Assert.Empty(StructureRules.Violations("Minibox.Catalog",
            typeof(Program).Assembly,
            typeof(Application.AssemblyReference).Assembly,
            typeof(Infrastructure.AssemblyReference).Assembly));
}
