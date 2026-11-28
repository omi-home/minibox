using System.Reflection;
using NetArchTest.Rules;

namespace Minibox.BuildingBlocks.Architecture;

/// <summary>Luật phụ thuộc spec §5.1 dùng chung cho ArchitectureTests của mọi service: phụ thuộc hướng vào trong; service khác chỉ qua *.Contracts.</summary>
public static class DependencyRules
{
    /// <summary>
    /// Trả danh sách vi phạm (rỗng = đạt). Layer suy từ tên assembly <c>{service}.{Layer}</c>
    /// (Domain, Application, Infrastructure, Contracts, Api); assembly khác bỏ qua luật layer.
    /// </summary>
    /// <param name="service">Tiền tố assembly của service, vd <c>Minibox.Order</c>.</param>
    /// <param name="assemblies">Các assembly của service cần kiểm.</param>
    /// <returns>Mỗi phần tử là một vi phạm: <c>{type} phụ thuộc layer ngoài</c> hoặc <c>{assembly} → {assembly service khác}</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="service"/> rỗng.</exception>
    public static IReadOnlyList<string> Violations(string service, params Assembly[] assemblies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        var violations = new List<string>();
        foreach (var assembly in assemblies)
        {
            var name = assembly.GetName().Name!;
            var layer = name.StartsWith($"{service}.", StringComparison.Ordinal) ? name[(service.Length + 1)..] : "";
            if (Forbidden(service).TryGetValue(layer, out var forbidden))
            {
                var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
                violations.AddRange((result.FailingTypeNames ?? []).Select(t => $"{t} phụ thuộc layer ngoài"));
            }
            violations.AddRange(assembly.GetReferencedAssemblies()
                .Select(r => r.Name!)
                .Where(target => target.StartsWith("Minibox.", StringComparison.Ordinal)
                                 && !target.StartsWith($"{service}.", StringComparison.Ordinal)
                                 && !target.StartsWith("Minibox.BuildingBlocks.", StringComparison.Ordinal)
                                 && !target.EndsWith(".Contracts", StringComparison.Ordinal))
                .Select(target => $"{name} → {target}"));
        }
        return violations;
    }

    static Dictionary<string, string[]> Forbidden(string svc) => new()
    {
        ["Domain"] = [$"{svc}.Application", $"{svc}.Infrastructure", $"{svc}.Api", $"{svc}.Contracts",
                      "Minibox.BuildingBlocks.Application", "Minibox.BuildingBlocks.Infrastructure", "Minibox.BuildingBlocks.Web"],
        ["Application"] = [$"{svc}.Infrastructure", $"{svc}.Api", "Minibox.BuildingBlocks.Infrastructure", "Minibox.BuildingBlocks.Web"],
        ["Infrastructure"] = [$"{svc}.Api", "Minibox.BuildingBlocks.Web"],
        ["Contracts"] = [$"{svc}.Domain", $"{svc}.Application", $"{svc}.Infrastructure", $"{svc}.Api", "Minibox.BuildingBlocks"],
    };
}
