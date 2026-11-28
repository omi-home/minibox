using Microsoft.EntityFrameworkCore;
using Minibox.ServiceName.Application.Abstractions;
using Minibox.ServiceName.Application.Features.Samples.Queries.ListSamples;
using Minibox.ServiceName.Domain.Aggregates.Samples;

namespace Minibox.ServiceName.Infrastructure.Persistence;

/// <summary>Read service mẫu: LINQ <c>AsNoTracking</c> + <c>Select</c> chiếu thẳng ra DTO (không tải entity).</summary>
/// <param name="db">DbContext của request.</param>
public sealed class SampleReadService(ServiceNameDbContext db) : ISampleReadService
{
    /// <inheritdoc/>
    public async Task<SampleDto[]> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Set<Sample>().AsNoTracking().OrderBy(s => s.Name).Select(s => new SampleDto(s.Id, s.Name)).ToArrayAsync(cancellationToken);
}
