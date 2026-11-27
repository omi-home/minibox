using Minibox.Messy.Application.Features.Things;
using Minibox.Messy.Infrastructure;

namespace Minibox.BuildingBlocks.Architecture.Tests;

/// <summary>Test ngược: fixture Minibox.Messy.* cố tình đặt sai từng chỗ thì luật cấu trúc §5.2 phải báo, chỗ đúng không bị báo.</summary>
public class StructureRulesTests
{
    static readonly IReadOnlyList<string> Violations = StructureRules.Violations("Minibox.Messy",
        typeof(Minibox.Messy.Api.Endpoints.ThingEndpoints).Assembly, typeof(GetThing).Assembly, typeof(ThingReader).Assembly);

    static void Flagged(string type) =>
        Assert.True(Violations.Any(v => v.StartsWith(type, StringComparison.Ordinal)), $"{type} phải bị báo; có: {string.Join(" | ", Violations)}");

    [Fact(DisplayName = "ac455_2 Program viết endpoint lambda gửi ISender bị báo")]
    public void ac455_2_program_lambda_endpoint_is_violation() => Flagged("Program");

    [Fact(DisplayName = "ac455_2 Endpoint dùng Infrastructure bị báo")]
    public void ac455_2_endpoint_using_infrastructure_is_violation() => Flagged("Minibox.Messy.Api.Endpoints.ThingEndpoints");

    [Fact(DisplayName = "ac455_2 Type Api ngoài Endpoints/Program gửi query bị báo")]
    public void ac455_2_other_api_type_sending_is_violation() => Flagged("Minibox.Messy.Api.Helpers.Shortcut");

    [Theory(DisplayName = "ac455_2 Handler ngoài Application.Features bị báo")]
    [InlineData("Minibox.Messy.Application.Misc.StrayHandler")]
    [InlineData("Minibox.Messy.Infrastructure.MisplacedHandler")]
    public void ac455_2_handler_outside_features_is_violation(string type) => Flagged(type);

    [Fact(DisplayName = "ac455_2 Logic nghiệp vụ trong Infrastructure (không cài port) bị báo")]
    public void ac455_2_business_logic_in_infrastructure_is_violation() => Flagged("Minibox.Messy.Infrastructure.PriceCalculator");

    [Theory(DisplayName = "ac455_2 Chỗ đặt đúng không bị báo")]
    [InlineData("Minibox.Messy.Application.Features.Things.GetThingHandler")]
    [InlineData("Minibox.Messy.Infrastructure.ThingReader")]
    [InlineData("Minibox.Messy.Infrastructure.InfrastructureSetup")]
    public void ac455_2_correct_placement_is_clean(string type) =>
        Assert.DoesNotContain(Violations, v => v.StartsWith(type + ":", StringComparison.Ordinal));

    [Fact(DisplayName = "ac455_2 Chỉ đúng các vi phạm cố ý bị báo (không báo nhầm)")]
    public void ac455_2_only_intended_violations() =>
        Assert.Equal(
            ["Minibox.Messy.Api.Endpoints.ThingEndpoints", "Minibox.Messy.Api.Helpers.Shortcut", "Minibox.Messy.Application.Misc.StrayHandler",
             "Minibox.Messy.Infrastructure.MisplacedHandler", "Minibox.Messy.Infrastructure.PriceCalculator", "Program"],
            Violations.Select(v => v[..v.IndexOf(':')].Split('+', '/')[0]).Distinct().Order(StringComparer.Ordinal));

    [Fact(DisplayName = "ac455_2 Assembly sai tên layer thì ném lỗi thay vì bỏ qua")]
    public void ac455_2_wrong_assembly_throws() =>
        Assert.Throws<ArgumentException>(() => StructureRules.Violations("Minibox.Messy",
            typeof(GetThing).Assembly, typeof(GetThing).Assembly, typeof(ThingReader).Assembly));
}
