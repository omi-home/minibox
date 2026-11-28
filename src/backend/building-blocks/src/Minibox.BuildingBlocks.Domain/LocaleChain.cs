namespace Minibox.BuildingBlocks.Domain;

/// <summary>Chuỗi fallback ngôn ngữ (spec §6.6) dùng chung cho localization và mọi service có bảng <c>*_translation</c>:
/// locale → fallback → … → <see cref="Default"/>.</summary>
public static class LocaleChain
{
    /// <summary>Locale mặc định và fallback cuối.</summary>
    public const string Default = "vi";

    /// <summary>Dựng chuỗi fallback, luôn kết thúc bằng <see cref="Default"/>.</summary>
    /// <param name="code">Locale bắt đầu.</param>
    /// <param name="fallbacks">Mã locale → mã fallback (<c>null</c> hoặc vắng = rơi về <see cref="Default"/>).</param>
    /// <returns>Chuỗi mã theo thứ tự ưu tiên; <c>null</c> nếu gặp vòng.</returns>
    public static IReadOnlyList<string>? Build(string code, IReadOnlyDictionary<string, string?> fallbacks)
    {
        var chain = new List<string>();
        for (string? c = code; c is not null; c = fallbacks.GetValueOrDefault(c))
        {
            if (chain.Contains(c))
                return null;
            chain.Add(c);
        }
        if (!chain.Contains(Default))
            chain.Add(Default);
        return chain;
    }

    /// <summary>Chọn mỗi key một bản dịch có locale đứng trước nhất trong <paramref name="chain"/>.</summary>
    /// <typeparam name="TKey">Khoá của bản ghi được dịch (vd. id sản phẩm).</typeparam>
    /// <typeparam name="T">Dòng bản dịch.</typeparam>
    /// <param name="rows">Các dòng bản dịch, thường đã lọc <c>locale = ANY(chain)</c> ở DB.</param>
    /// <param name="key">Lấy khoá của dòng.</param>
    /// <param name="locale">Lấy locale của dòng.</param>
    /// <param name="chain">Chuỗi fallback từ <see cref="Build"/>.</param>
    /// <returns>Khoá → dòng tốt nhất; khoá không có dòng nào thuộc chuỗi thì vắng.</returns>
    public static IReadOnlyDictionary<TKey, T> PickPerKey<TKey, T>(
        IEnumerable<T> rows, Func<T, TKey> key, Func<T, string> locale, IReadOnlyList<string> chain) where TKey : notnull
    {
        var best = new Dictionary<TKey, (int Rank, T Row)>();
        foreach (var row in rows)
        {
            var rank = IndexOf(chain, locale(row));
            if (rank < 0)
                continue;
            var k = key(row);
            if (!best.TryGetValue(k, out var current) || rank < current.Rank)
                best[k] = (rank, row);
        }
        return best.ToDictionary(p => p.Key, p => p.Value.Row);
    }

    static int IndexOf(IReadOnlyList<string> chain, string code)
    {
        for (var i = 0; i < chain.Count; i++)
            if (chain[i] == code)
                return i;
        return -1;
    }
}
