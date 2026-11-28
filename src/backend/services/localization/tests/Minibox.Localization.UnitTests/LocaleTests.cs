using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Errors;

namespace Minibox.Localization.UnitTests;

/// <summary>Bất biến của <see cref="Locale"/>: mã BCP 47, tên, fallback.</summary>
public class LocaleTests
{
    [Theory(DisplayName = "Locale.Create nhận mã BCP 47 có subtag")]
    [InlineData("vi")]
    [InlineData("en")]
    [InlineData("zh-Hant")]
    [InlineData("pt-BR")]
    public void create_accepts_bcp47(string code)
    {
        var locale = Locale.Create(code, "  Tên  ", 3, code == "vi" ? null : "vi").Value;
        Assert.Equal(code, locale.Id);
        Assert.Equal("Tên", locale.Name);
        Assert.False(locale.Enabled);
    }

    [Theory(DisplayName = "Locale.Create từ chối mã sai dạng")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("VI")]
    [InlineData("vi_VN")]
    [InlineData("e")]
    [InlineData("vi-")]
    public void create_rejects_bad_code(string? code) =>
        Assert.Equal(LocalizationErrors.LocaleCodeInvalid, Locale.Create(code, "Tên", 0, null).Error);

    [Theory(DisplayName = "Locale.Create từ chối tên rỗng hoặc quá dài")]
    [InlineData(null)]
    [InlineData("  ")]
    public void create_rejects_bad_name(string? name)
    {
        Assert.Equal(LocalizationErrors.LocaleNameInvalid, Locale.Create("ja", name, 0, null).Error);
        Assert.Equal(LocalizationErrors.LocaleNameInvalid, Locale.Create("ja", new string('a', Locale.NameMaxLength + 1), 0, null).Error);
    }

    [Theory(DisplayName = "Locale.Create từ chối fallback trùng chính nó hoặc sai dạng")]
    [InlineData("ja")]
    [InlineData("EN")]
    public void create_rejects_bad_fallback(string fallback) =>
        Assert.Equal(LocalizationErrors.LocaleFallbackInvalid, Locale.Create("ja", "日本語", 0, fallback).Error);
}
