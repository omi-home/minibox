using Microsoft.Extensions.Configuration;

namespace Minibox.BuildingBlocks.Web.Tests;

public class ConfigurationTests
{
    static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value))).Build();

    [Fact(DisplayName = "RequiredConnectionString trả giá trị cấu hình; thiếu khi chạy thật thì ném lỗi (không dùng giá trị giữ chỗ)")]
    public void required_connection_string()
    {
        Assert.False(MiniboxConfiguration.IsGeneratingOpenApi);
        Assert.Equal("Host=db", Config(("ConnectionStrings:App", "Host=db")).RequiredConnectionString("App", "Host=placeholder"));
        Assert.Throws<InvalidOperationException>(() => Config().RequiredConnectionString("App", "Host=placeholder"));
    }
}
