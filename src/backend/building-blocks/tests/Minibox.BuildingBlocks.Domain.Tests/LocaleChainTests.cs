using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Domain.Tests;

public sealed class LocaleChainTests
{
    static readonly Dictionary<string, string?> Fallbacks = new() { ["vi"] = null, ["en"] = null, ["ja"] = "en", ["x"] = "y", ["y"] = "x" };

    [Fact]
    public void Build_follows_fallbacks_and_ends_with_vi()
    {
        Assert.Equal(["ja", "en", "vi"], LocaleChain.Build("ja", Fallbacks));
        Assert.Equal(["vi"], LocaleChain.Build("vi", Fallbacks));
        Assert.Equal(["ko", "vi"], LocaleChain.Build("ko", Fallbacks)); // không có trong danh sách: chỉ chính nó rồi vi
        Assert.Null(LocaleChain.Build("x", Fallbacks));
    }

    sealed record Row(int Id, string Locale, string Name);

    [Fact]
    public void PickPerKey_takes_highest_priority_locale_per_key_and_skips_locales_outside_chain()
    {
        Row[] rows = [new(1, "vi", "Rau"), new(1, "en", "Greens"), new(1, "ja", "野菜"), new(2, "vi", "Củ"), new(2, "en", "Roots"), new(3, "fr", "Fruit")];
        var picked = LocaleChain.PickPerKey(rows, r => r.Id, r => r.Locale, ["ja", "en", "vi"]);
        Assert.Equal("野菜", picked[1].Name);
        Assert.Equal("Roots", picked[2].Name);
        Assert.False(picked.ContainsKey(3));
    }
}
