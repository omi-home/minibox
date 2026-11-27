using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Aggregates.Messages;
using Minibox.Localization.Domain.Errors;
using Minibox.Localization.Domain.Services;

namespace Minibox.Localization.UnitTests;

/// <summary>Sửa locale, chuỗi fallback (chặn vòng) và vòng đời nháp → xuất bản của chuỗi giao diện.</summary>
public class LocaleFallbackTests
{
    static readonly Dictionary<string, string?> Seed = new() { ["vi"] = null, ["en"] = "vi", ["ja"] = "en", ["ko"] = null };

    [Fact(DisplayName = "Chuỗi fallback đi qua các locale rồi kết thúc ở vi")]
    public void chain_ends_with_default()
    {
        Assert.Equal(["ja", "en", "vi"], LocaleFallback.Chain("ja", Seed));
        Assert.Equal(["ko", "vi"], LocaleFallback.Chain("ko", Seed));
        Assert.Equal(["vi"], LocaleFallback.Chain("vi", Seed));
    }

    [Fact(DisplayName = "Đặt fallback tạo vòng (en → ja → en) hoặc trỏ locale không có bị từ chối")]
    public void validate_rejects_cycle_and_missing()
    {
        Assert.Equal(LocalizationErrors.LocaleFallbackCycle, LocaleFallback.Validate("en", "ja", Seed).Error);
        Assert.Equal(LocalizationErrors.LocaleFallbackNotFound, LocaleFallback.Validate("ko", "fr", Seed).Error);
        Assert.True(LocaleFallback.Validate("ko", "ja", Seed).IsSuccess);
        Assert.True(LocaleFallback.Validate("ko", null, Seed).IsSuccess);
        Assert.Null(LocaleFallback.Chain("a", new Dictionary<string, string?> { ["a"] = "b", ["b"] = "a" }));
    }

    [Fact(DisplayName = "Locale.Update đổi trạng thái và phát LocaleChanged; vi không tắt được, không có fallback")]
    public void update_raises_changed_and_locks_default()
    {
        var ja = Locale.Create("ja", "日本語", 2, "en").Value;
        Assert.True(ja.Update(" Nihongo ", true, 5, null).IsSuccess);
        Assert.Equal(new LocaleChanged("ja", "Nihongo", true, 5, null), ja.DomainEvents[^1]);

        var vi = Locale.Create("vi", "Tiếng Việt", 0, null).Value;
        Assert.Equal(LocalizationErrors.DefaultLocaleLocked, vi.Update("Tiếng Việt", false, 0, null).Error);
        Assert.Equal(LocalizationErrors.DefaultLocaleLocked, vi.Update("Tiếng Việt", true, 0, "en").Error);
        Assert.Equal(LocalizationErrors.LocaleFallbackInvalid, ja.Update("x", true, 0, "ja").Error);
    }

    [Fact(DisplayName = "UiMessage: tạo nháp chưa công khai; xuất bản chép nháp sang công khai, xuất bản lại khi không đổi là no-op")]
    public void message_draft_publish_cycle()
    {
        var now = DateTimeOffset.UtcNow;
        var m = UiMessage.Create("shop", "cart.title", "en", "Cart", now).Value;
        Assert.Null(m.PublishedText);
        Assert.True(m.Publish(now));
        Assert.Equal("Cart", m.PublishedText);
        Assert.False(m.Publish(now));

        Assert.True(m.Edit("Basket", now).IsSuccess);
        Assert.Equal("Cart", m.PublishedText);
        Assert.True(m.Publish(now));
        Assert.IsType<UiMessagePublished>(m.DomainEvents[^1]);
        Assert.Equal("Basket", m.PublishedText);
    }

    [Theory(DisplayName = "UiMessage từ chối nội dung rỗng hoặc dài quá 10000 ký tự, namespace/key sai dạng")]
    [InlineData("shop", "k", "")]
    [InlineData("shop", "k", null)]
    [InlineData("Shop", "k", "x")]
    [InlineData("shop", "a b", "x")]
    public void message_rejects_bad_input(string ns, string key, string? text) =>
        Assert.True(UiMessage.Create(ns, key, "vi", text, DateTimeOffset.UtcNow).IsFailure);

    [Fact(DisplayName = "UiMessage nhận đúng 10000 ký tự, từ chối 10001")]
    public void message_text_limit()
    {
        Assert.True(UiMessage.Create("shop", "k", "vi", new string('a', UiMessage.TextMaxLength), DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(LocalizationErrors.MessageTextInvalid,
            UiMessage.Create("shop", "k", "vi", new string('a', UiMessage.TextMaxLength + 1), DateTimeOffset.UtcNow).Error);
    }
}
