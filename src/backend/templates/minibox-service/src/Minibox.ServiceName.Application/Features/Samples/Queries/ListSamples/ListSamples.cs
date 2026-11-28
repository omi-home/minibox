using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.ServiceName.Application.Abstractions;

namespace Minibox.ServiceName.Application.Features.Samples.Queries.ListSamples;

/// <summary>Truy vấn danh sách mẫu; cần quyền <c>servicename.sample.read</c>.</summary>
public sealed record ListSamples : IQuery<SampleDto[]>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => "servicename.sample.read";
}

/// <summary>Dòng đọc của <c>Sample</c> trả cho client.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Tên hiển thị.</param>
public sealed record SampleDto(Guid Id, string Name);

/// <summary>Đọc qua <see cref="ISampleReadService"/> (không qua repository, không mở transaction).</summary>
/// <param name="reader">Port đọc.</param>
public sealed class ListSamplesHandler(ISampleReadService reader) : IQueryHandler<ListSamples, SampleDto[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<SampleDto[]>> Handle(ListSamples query, CancellationToken cancellationToken) =>
        await reader.ListAsync(cancellationToken);
}
