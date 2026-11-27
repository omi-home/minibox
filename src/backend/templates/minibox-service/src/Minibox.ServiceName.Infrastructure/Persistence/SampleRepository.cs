using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.ServiceName.Domain.Aggregates.Samples;

namespace Minibox.ServiceName.Infrastructure.Persistence;

/// <summary>Repository mẫu: kế thừa bản generic, thêm truy vấn đặc thù (LINQ trên <see cref="EfRepository{TAggregate, TId}.Set"/>) khi cần.</summary>
/// <param name="db">DbContext của command.</param>
public sealed class SampleRepository(ServiceNameDbContext db) : EfRepository<Sample, Guid>(db);
