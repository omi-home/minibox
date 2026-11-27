using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using NetArchTest.Rules;

namespace Minibox.BuildingBlocks.Architecture;

/// <summary>Luật cấu trúc bên trong layer theo spec §5.2/§5.4, bổ sung cho <see cref="DependencyRules"/> (vốn chỉ kiểm tham chiếu giữa assembly).</summary>
/// <remarks>
/// <list type="bullet">
/// <item>Api: <c>Program</c> chỉ là composition root — không dùng <c>{svc}.Application.Features</c>, <c>{svc}.Domain</c> hay <c>ISender</c>
/// (endpoint không viết lambda trong Program); <c>{svc}.Api.Endpoints</c> không dùng <c>{svc}.Infrastructure</c>/<c>{svc}.Domain</c>;
/// type Api khác không dùng Features/Domain/Infrastructure/<c>ISender</c>.</item>
/// <item>Handler Mediator chỉ nằm trong <c>{svc}.Application.Features</c>.</item>
/// <item>Infrastructure chỉ chứa class cài port (interface của <c>{svc}.Application</c>, BuildingBlocks.Application hoặc BuildingBlocks.Infrastructure
/// như <c>IIntegrationEventMapper</c>), <c>DbContext</c>,
/// <c>IEntityTypeConfiguration</c> và static class đăng ký; class khác bị coi là logic nghiệp vụ đặt sai chỗ.</item>
/// </list>
/// </remarks>
public static class StructureRules
{
    static readonly string[] Sender = ["Mediator.ISender", "Mediator.IMediator"];

    /// <summary>Kiểm luật cấu trúc trên ba assembly của service.</summary>
    /// <param name="service">Tiền tố assembly của service, vd <c>Minibox.Order</c>.</param>
    /// <param name="api">Assembly <c>{service}.Api</c>.</param>
    /// <param name="application">Assembly <c>{service}.Application</c>.</param>
    /// <param name="infrastructure">Assembly <c>{service}.Infrastructure</c>.</param>
    /// <returns>Danh sách vi phạm, mỗi phần tử <c>{type}: {lý do}</c>; rỗng = đạt.</returns>
    /// <exception cref="ArgumentException"><paramref name="service"/> rỗng hoặc assembly không đúng tên <c>{service}.{Layer}</c>.</exception>
    public static IReadOnlyList<string> Violations(string service, Assembly api, Assembly application, Assembly infrastructure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        Expect(api, $"{service}.Api");
        Expect(application, $"{service}.Application");
        Expect(infrastructure, $"{service}.Infrastructure");

        var features = $"{service}.Application.Features";
        var domain = $"{service}.Domain";
        var infra = $"{service}.Infrastructure";
        var endpoints = $"{service}.Api.Endpoints";
        var violations = new List<string>();

        violations.AddRange(Failing(api, top => top.FullName == "Program",
            [features, domain, .. Sender], "Program là composition root, không xử lý request (đặt endpoint trong Api/Endpoints, gửi qua ISender)"));
        violations.AddRange(Failing(api, top => top.Namespace.StartsWith(endpoints, StringComparison.Ordinal),
            [infra, domain], "endpoint chỉ gửi command/query của Application, không dùng Infrastructure/Domain"));
        violations.AddRange(Failing(api, top => top.FullName != "Program" && !top.Namespace.StartsWith(endpoints, StringComparison.Ordinal),
            [features, domain, infra, .. Sender], "type Api ngoài Endpoints/Program không chứa logic request"));

        foreach (var type in new[] { api, application, infrastructure }.SelectMany(Concrete).Where(IsHandler))
            if (type.Assembly != application || type.Namespace?.StartsWith(features, StringComparison.Ordinal) != true)
                violations.Add($"{type.FullName}: handler chỉ được đặt trong {features}");

        foreach (var type in Concrete(infrastructure).Where(t => !IsHandler(t) && !IsInfrastructure(t, service)))
            violations.Add($"{type.FullName}: Infrastructure chỉ chứa repository/read service/adapter cài port của Application, DbContext, Configurations, setup");

        return violations;
    }

    static void Expect(Assembly assembly, string name)
    {
        if (assembly.GetName().Name != name)
            throw new ArgumentException($"Cần assembly {name}, nhận {assembly.GetName().Name}");
    }

    // Nhóm theo type ngoài cùng (lambda của Program là type lồng Program/<>c); bỏ type do source generator sinh
    // (Mediator sinh vào namespace Mediator và Microsoft.Extensions.DependencyInjection của host).
    static IEnumerable<string> Failing(Assembly assembly, Func<TypeDefinition, bool> group, string[] forbidden, string reason) =>
        (Types.InAssembly(assembly).That().MeetCustomRule(new TopLevel(group)).ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult().FailingTypeNames ?? []).Select(t => $"{t}: {reason}");

    sealed class TopLevel(Func<TypeDefinition, bool> group) : ICustomRule
    {
        public bool MeetsRule(TypeDefinition type)
        {
            while (type.DeclaringType is not null) type = type.DeclaringType;
            return !type.Namespace.StartsWith("Mediator", StringComparison.Ordinal)
                   && !type.Namespace.StartsWith("Microsoft", StringComparison.Ordinal)
                   && group(type);
        }
    }

    static IEnumerable<Type> Concrete(Assembly assembly) => assembly.GetTypes().Where(t =>
        t is { IsClass: true, IsAbstract: false, IsNested: false }
        && !t.IsDefined(typeof(CompilerGeneratedAttribute), false)
        && t.Namespace?.StartsWith("Mediator", StringComparison.Ordinal) != true
        && t.Namespace?.StartsWith("Microsoft", StringComparison.Ordinal) != true);

    static bool IsHandler(Type type) => type.GetInterfaces().Any(i => i.Namespace == "Mediator" && i.Name.Contains("Handler", StringComparison.Ordinal));

    // ponytail: chưa có Messaging/Storage/Integrations; khi thêm consumer Wolverine (theo quy ước, không interface) thì mở thêm điều kiện ở đây.
    static bool IsInfrastructure(Type type, string service) =>
        BaseTypes(type).Any(b => b.FullName == "Microsoft.EntityFrameworkCore.DbContext")
        || type.GetInterfaces().Any(i => i.Namespace is { } ns
            && (ns.StartsWith($"{service}.Application", StringComparison.Ordinal)
                || ns.StartsWith("Minibox.BuildingBlocks.Application", StringComparison.Ordinal)
                || ns.StartsWith("Minibox.BuildingBlocks.Infrastructure", StringComparison.Ordinal)
                || (ns == "Microsoft.EntityFrameworkCore" && i.Name.StartsWith("IEntityTypeConfiguration", StringComparison.Ordinal))));

    static IEnumerable<Type> BaseTypes(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType) yield return t;
    }
}
