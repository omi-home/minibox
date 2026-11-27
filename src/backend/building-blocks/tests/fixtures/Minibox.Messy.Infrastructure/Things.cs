using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Messy.Application.Abstractions;
using Minibox.Messy.Application.Features.Things;

namespace Minibox.Messy.Infrastructure;

/// <summary>Đúng chỗ: cài port của Application.</summary>
public sealed class ThingReader : IThingReader
{
    /// <inheritdoc/>
    public int Count() => 1;
}

/// <summary>Vi phạm cố ý: logic nghiệp vụ (không cài port) đặt trong Infrastructure.</summary>
public sealed class PriceCalculator
{
    /// <summary>Tính giá.</summary>
    /// <param name="qty">Số lượng.</param>
    /// <returns>Thành tiền.</returns>
    public long Total(int qty) => qty * 1000L;
}

/// <summary>Vi phạm cố ý: handler trong Infrastructure.</summary>
public sealed class MisplacedHandler : ICommandHandler<DoThing>
{
    /// <inheritdoc/>
    public ValueTask<Result> Handle(DoThing command, CancellationToken cancellationToken) => new(Result.Success());
}

/// <summary>Đúng chỗ: static class đăng ký.</summary>
public static class InfrastructureSetup
{
    /// <summary>Tạo reader.</summary>
    /// <returns>Reader mặc định.</returns>
    public static IThingReader Reader() => new ThingReader();
}
