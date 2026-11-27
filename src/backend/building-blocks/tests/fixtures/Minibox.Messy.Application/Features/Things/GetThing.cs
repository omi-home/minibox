using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Messy.Application.Abstractions;

namespace Minibox.Messy.Application.Features.Things;

/// <summary>Query đặt đúng chỗ.</summary>
public sealed record GetThing : IQuery<int>;

/// <summary>Handler đặt đúng chỗ (Application.Features).</summary>
/// <param name="reader">Port đọc.</param>
public sealed class GetThingHandler(IThingReader reader) : IQueryHandler<GetThing, int>
{
    /// <inheritdoc/>
    public ValueTask<Result<int>> Handle(GetThing query, CancellationToken cancellationToken) => new((Result<int>)reader.Count());
}

/// <summary>Command dùng cho các handler đặt sai chỗ.</summary>
public sealed record DoThing : ICommand;
