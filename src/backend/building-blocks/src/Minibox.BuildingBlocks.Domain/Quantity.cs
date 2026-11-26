namespace Minibox.BuildingBlocks.Domain;

/// <summary>Số lượng không âm kèm đơn vị (vd. <c>kg</c>, <c>cái</c>); chỉ cộng/trừ được khi cùng đơn vị.</summary>
public sealed record Quantity : ValueObject
{
    /// <summary>Tạo số lượng.</summary>
    /// <param name="value">Giá trị, phải ≥ 0.</param>
    /// <param name="unit">Mã đơn vị, không rỗng; so sánh phân biệt hoa thường.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> âm.</exception>
    /// <exception cref="ArgumentException"><paramref name="unit"/> rỗng.</exception>
    public Quantity(decimal value, string unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        Value = value;
        Unit = unit;
    }

    /// <summary>Giá trị số lượng.</summary>
    public decimal Value { get; }

    /// <summary>Mã đơn vị.</summary>
    public string Unit { get; }

    /// <summary>Cộng hai số lượng cùng đơn vị.</summary>
    /// <param name="a">Số hạng thứ nhất.</param>
    /// <param name="b">Số hạng thứ hai.</param>
    /// <returns>Tổng, cùng đơn vị.</returns>
    /// <exception cref="InvalidOperationException">Khác đơn vị.</exception>
    public static Quantity operator +(Quantity a, Quantity b) => new(a.Value + SameUnit(a, b).Value, a.Unit);

    /// <summary>Trừ hai số lượng cùng đơn vị.</summary>
    /// <param name="a">Số bị trừ.</param>
    /// <param name="b">Số trừ.</param>
    /// <returns>Hiệu, cùng đơn vị.</returns>
    /// <exception cref="InvalidOperationException">Khác đơn vị.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Kết quả âm.</exception>
    public static Quantity operator -(Quantity a, Quantity b) => new(a.Value - SameUnit(a, b).Value, a.Unit);

    static Quantity SameUnit(Quantity a, Quantity b) =>
        a.Unit == b.Unit ? b : throw new InvalidOperationException($"Khác đơn vị: {a.Unit} và {b.Unit}.");
}
