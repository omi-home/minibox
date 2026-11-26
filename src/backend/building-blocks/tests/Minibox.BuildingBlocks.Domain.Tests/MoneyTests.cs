using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Minibox.BuildingBlocks.Domain.Tests;

public class MoneyTests
{
    static Int128 Total(Money[] parts) => parts.Aggregate(Int128.Zero, (s, p) => s + p.Amount);

    static Arbitrary<long[]> Weights() =>
        Gen.Choose(0, 1_000_000).Select(w => (long)w).NonEmptyListOf()
            .Where(ws => ws.Sum() > 0).Select(ws => ws.ToArray()).ToArbitrary();

    [Property(DisplayName = "ac187_6 phân bổ phần dư lớn nhất: tổng các phần luôn bằng tổng gốc", MaxTest = 500)]
    public Property ac187_6_allocation_sums_to_total() =>
        Prop.ForAll(ArbMap.Default.ArbFor<long>(), Weights(), (amount, weights) =>
        {
            var parts = Money.Vnd(amount).Allocate(weights);
            return parts.Length == weights.Length && Total(parts) == amount;
        });

    [Property(DisplayName = "phân bổ: mỗi phần lệch tỉ lệ chính xác < 1 đồng, phần trọng số 0 nhận 0", MaxTest = 500)]
    public Property allocation_is_fair() =>
        Prop.ForAll(Gen.Choose(0, int.MaxValue).Select(a => (long)a).ToArbitrary(), Weights(), (amount, weights) =>
        {
            var parts = Money.Vnd(amount).Allocate(weights);
            var total = weights.Sum();
            return parts.Select((p, i) =>
            {
                var exact = (decimal)amount * weights[i] / total;
                return Math.Abs(p.Amount - exact) < 1 && (weights[i] != 0 || p.Amount == 0);
            }).All(ok => ok);
        });

    [Fact(DisplayName = "phân bổ xác định: phần dư bằng nhau ưu tiên vị trí đứng trước")]
    public void allocation_tie_break_by_index()
    {
        Assert.Equal([34L, 33, 33], Money.Vnd(100).Allocate([1, 1, 1]).Select(m => m.Amount));
        Assert.Equal([-34L, -33, -33], Money.Vnd(-100).Allocate([1, 1, 1]).Select(m => m.Amount));
        // 1000 theo 1:2:3 = 166.67 / 333.33 / 500 → phần dư lớn nhất (0.67) nhận đồng lẻ
        Assert.Equal([167L, 333, 500], Money.Vnd(1000).Allocate([1, 2, 3]).Select(m => m.Amount));
    }

    [Fact(DisplayName = "phân bổ không tràn số với tiền và trọng số lớn")]
    public void allocation_no_overflow()
    {
        var parts = Money.Vnd(long.MaxValue).Allocate([long.MaxValue, 1]);
        Assert.Equal(long.MaxValue, Total(parts));
    }

    [Fact(DisplayName = "phân bổ từ chối trọng số rỗng, âm hoặc tổng 0")]
    public void allocation_rejects_bad_weights()
    {
        Assert.Throws<ArgumentException>(() => Money.Vnd(10).Allocate([]));
        Assert.Throws<ArgumentException>(() => Money.Vnd(10).Allocate([0, 0]));
        Assert.Throws<ArgumentException>(() => Money.Vnd(10).Allocate([2, -1]));
    }

    [Fact(DisplayName = "Money: cộng/trừ/so sánh theo đồng, bằng nhau theo giá trị, tràn số ném lỗi")]
    public void arithmetic_and_equality()
    {
        Assert.Equal(Money.Vnd(150), Money.Vnd(100) + Money.Vnd(50));
        Assert.Equal(Money.Vnd(-50), Money.Vnd(50) - Money.Vnd(100));
        Assert.True(Money.Vnd(1) > Money.Zero);
        Assert.Equal(Money.Vnd(7), Money.Vnd(7));
        Assert.Throws<OverflowException>(() => Money.Vnd(long.MaxValue) + Money.Vnd(1));
    }
}
