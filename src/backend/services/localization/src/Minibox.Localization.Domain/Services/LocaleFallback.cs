using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Errors;

namespace Minibox.Localization.Domain.Services;

/// <summary>Chuỗi fallback của locale (spec §6.6): locale → fallback → … → <see cref="Locale.Default"/>.</summary>
public static class LocaleFallback
{
    /// <summary>Dựng chuỗi fallback, luôn kết thúc bằng <see cref="Locale.Default"/>.</summary>
    /// <param name="code">Locale bắt đầu.</param>
    /// <param name="fallbacks">Mã locale → mã fallback (<c>null</c> = rơi về <see cref="Locale.Default"/>).</param>
    /// <returns>Chuỗi mã theo thứ tự ưu tiên; <c>null</c> nếu gặp vòng.</returns>
    public static IReadOnlyList<string>? Chain(string code, IReadOnlyDictionary<string, string?> fallbacks) => LocaleChain.Build(code, fallbacks);

    /// <summary>Kiểm việc đặt fallback của <paramref name="code"/> thành <paramref name="fallbackCode"/>: fallback phải tồn tại và không tạo vòng
    /// (DB chỉ chặn tự trỏ).</summary>
    /// <param name="code">Locale đang tạo/sửa.</param>
    /// <param name="fallbackCode">Fallback mới; <c>null</c> luôn hợp lệ.</param>
    /// <param name="fallbacks">Fallback hiện tại của mọi locale.</param>
    /// <returns>Thành công; lỗi <see cref="LocalizationErrors.LocaleFallbackNotFound"/> hoặc <see cref="LocalizationErrors.LocaleFallbackCycle"/>.</returns>
    public static Result Validate(string code, string? fallbackCode, IReadOnlyDictionary<string, string?> fallbacks)
    {
        if (fallbackCode is null)
            return Result.Success();
        if (!fallbacks.ContainsKey(fallbackCode))
            return LocalizationErrors.LocaleFallbackNotFound;
        var next = new Dictionary<string, string?>(fallbacks) { [code] = fallbackCode };
        return Chain(code, next) is null ? LocalizationErrors.LocaleFallbackCycle : Result.Success();
    }
}
