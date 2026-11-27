using Minibox.BuildingBlocks.Architecture;

namespace Minibox.ServiceName.ArchitectureTests;

/// <summary>Luật cấu trúc trong layer spec §5.2 (dùng chung từ Minibox.BuildingBlocks.Architecture).</summary>
public class StructureRulesTests
{
    [Fact(DisplayName = "ac455_1 Program chỉ là composition root, endpoint ở Api/Endpoints, handler ở Application/Features, Infrastructure chỉ cài port")]
    public void ac455_1_structure_rules_hold() =>
        Assert.Empty(StructureRules.Violations("Minibox.ServiceName",
            typeof(Program).Assembly,
            typeof(Application.AssemblyReference).Assembly,
            typeof(Infrastructure.AssemblyReference).Assembly));
}
