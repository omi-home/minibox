namespace Minibox.BuildingBlocks.Domain;

/// <summary>Số tiền VND tính theo đồng (số nguyên <c>long</c>, khớp cột <c>bigint</c>); không dùng số thực.</summary>
/// <remarks>Giao dịch luôn bằng VND; tiền tệ khác chỉ để hiển thị nên không có trường tiền tệ (spec §2.5).</remarks>
public sealed record Money : ValueObject, IComparable<Money>
{
    Money(long amount) => Amount = amount;

    /// <summary>Số đồng; âm được (bút toán điều chỉnh, hoàn tiền).</summary>
    public long Amount { get; }

    /// <summary>0 đồng.</summary>
    public static Money Zero { get; } = new(0);

    /// <summary>Tạo số tiền VND.</summary>
    /// <param name="amount">Số đồng.</param>
    /// <returns>Số tiền tương ứng.</returns>
    public static Money Vnd(long amount) => new(amount);

    /// <summary>Chia số tiền theo trọng số bằng phương pháp phần dư lớn nhất.</summary>
    /// <param name="weights">Trọng số từng phần, không âm, tổng &gt; 0 (vd. tiền hàng từng dòng).</param>
    /// <returns>Mỗi trọng số một phần, cùng thứ tự; tổng các phần luôn bằng <see cref="Amount"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="weights"/> rỗng, có phần tử âm, hoặc tổng bằng 0.</exception>
    /// <remarks>
    /// Mỗi phần nhận phần nguyên của <c>|Amount| × w / Σw</c>; các đồng còn lại lần lượt cho phần có phần dư lớn nhất,
    /// bằng nhau thì phần đứng trước được trước — kết quả xác định, lệch tỉ lệ chính xác dưới 1 đồng. Số âm chia theo trị
    /// tuyệt đối rồi đổi dấu. Tính bằng <see cref="Int128"/> nên không tràn số.
    /// </remarks>
    /// <example><c>Money.Vnd(1000).Allocate([1, 2, 3])</c> → 167, 333, 500.</example>
    public Money[] Allocate(IReadOnlyList<long> weights)
    {
        if (weights.Count == 0 || weights.Any(w => w < 0))
            throw new ArgumentException("Trọng số phải không rỗng và không âm.", nameof(weights));
        Int128 total = 0;
        foreach (var w in weights) total += w;
        if (total == 0) throw new ArgumentException("Tổng trọng số phải > 0.", nameof(weights));

        var abs = Int128.Abs(Amount);
        var parts = new Int128[weights.Count];
        var remainders = new Int128[weights.Count];
        var left = abs;
        for (var i = 0; i < parts.Length; i++)
        {
            (parts[i], remainders[i]) = Int128.DivRem(abs * weights[i], total);
            left -= parts[i];
        }
        // left < số phần: mỗi phần nhận tối đa 1 đồng lẻ
        foreach (var i in Enumerable.Range(0, parts.Length).OrderByDescending(i => remainders[i]).ThenBy(i => i).Take((int)left))
            parts[i]++;
        return [.. parts.Select(p => new Money((long)(Amount < 0 ? -p : p)))];
    }

    /// <inheritdoc/>
    public int CompareTo(Money? other) => other is null ? 1 : Amount.CompareTo(other.Amount);

    /// <summary>Cộng hai số tiền.</summary>
    /// <param name="a">Số hạng thứ nhất.</param>
    /// <param name="b">Số hạng thứ hai.</param>
    /// <returns>Tổng.</returns>
    /// <exception cref="OverflowException">Vượt phạm vi <c>long</c>.</exception>
    public static Money operator +(Money a, Money b) => new(checked(a.Amount + b.Amount));

    /// <summary>Trừ hai số tiền.</summary>
    /// <param name="a">Số bị trừ.</param>
    /// <param name="b">Số trừ.</param>
    /// <returns>Hiệu, có thể âm.</returns>
    /// <exception cref="OverflowException">Vượt phạm vi <c>long</c>.</exception>
    public static Money operator -(Money a, Money b) => new(checked(a.Amount - b.Amount));

    /// <summary>So sánh nhỏ hơn theo số đồng.</summary>
    /// <param name="a">Bên trái.</param>
    /// <param name="b">Bên phải.</param>
    /// <returns><c>true</c> nếu <paramref name="a"/> nhỏ hơn <paramref name="b"/>.</returns>
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;

    /// <summary>So sánh lớn hơn theo số đồng.</summary>
    /// <param name="a">Bên trái.</param>
    /// <param name="b">Bên phải.</param>
    /// <returns><c>true</c> nếu <paramref name="a"/> lớn hơn <paramref name="b"/>.</returns>
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;

    /// <summary>So sánh nhỏ hơn hoặc bằng theo số đồng.</summary>
    /// <param name="a">Bên trái.</param>
    /// <param name="b">Bên phải.</param>
    /// <returns><c>true</c> nếu <paramref name="a"/> không lớn hơn <paramref name="b"/>.</returns>
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;

    /// <summary>So sánh lớn hơn hoặc bằng theo số đồng.</summary>
    /// <param name="a">Bên trái.</param>
    /// <param name="b">Bên phải.</param>
    /// <returns><c>true</c> nếu <paramref name="a"/> không nhỏ hơn <paramref name="b"/>.</returns>
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;
}
