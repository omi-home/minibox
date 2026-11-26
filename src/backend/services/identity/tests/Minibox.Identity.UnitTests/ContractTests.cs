using System.Text.Json.Nodes;
using Json.Schema;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure.Accounts;
using Wolverine;

namespace Minibox.Identity.UnitTests;

/// <summary>Message phát ra outbox khớp JSON Schema có phiên bản trong <c>contracts/events</c> (spec §6.3).</summary>
public sealed class ContractTests
{
    static readonly JsonSchema Schema = JsonSchema.FromFile(Path.Combine(ContractsDir(), "events", "identity.AccountRegistered.v1.json"));

    static string ContractsDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "contracts", "events")))
                return Path.Combine(dir.FullName, "contracts");
        throw new DirectoryNotFoundException("Không tìm thấy contracts/events");
    }

    static EvaluationResults Evaluate(string json) =>
        Schema.Evaluate(System.Text.Json.JsonDocument.Parse(json).RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });

    [Theory(DisplayName = "ac212_5 Đăng ký tài khoản phát AccountRegistered; message (serializer Wolverine) khớp JSON Schema v1")]
    [InlineData(AccountKind.Staff, "staff")]
    [InlineData(AccountKind.Customer, "customer")]
    public void ac212_5_account_registered_matches_schema(AccountKind kind, string expectedKind)
    {
        var account = Account.Register(kind, "an@minibox.vn", null, null).Value;
        var message = new IdentityIntegrationEvents().Map(Assert.Single(account.DomainEvents));

        Assert.NotNull(message);
        var json = System.Text.Encoding.UTF8.GetString(new WolverineOptions().DefaultSerializer.WriteMessage(message));
        var result = Evaluate(json);
        Assert.True(result.IsValid, json + "\n" + string.Join("\n", (result.Details ?? []).Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))));
        var node = JsonNode.Parse(json)!;
        Assert.Equal(account.Id.ToString(), node["accountId"]!.GetValue<string>());
        Assert.Equal(expectedKind, node["kind"]!.GetValue<string>());
        Assert.DoesNotContain("minibox.vn", json); // không mang PII
    }

    [Fact(DisplayName = "ac212_5 Schema v1 chặn message sai: thiếu trường, sai kind, thêm trường lạ")]
    public void ac212_5_schema_rejects_drift()
    {
        Assert.False(Evaluate("""{"kind":"staff"}""").IsValid);
        Assert.False(Evaluate($$"""{"accountId":"{{Guid.CreateVersion7()}}","kind":"admin"}""").IsValid);
        Assert.False(Evaluate($$"""{"accountId":"{{Guid.CreateVersion7()}}","kind":"staff","email":"a@b.vn"}""").IsValid);
    }
}
