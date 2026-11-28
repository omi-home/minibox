using Minibox.ServiceName.Application.Features.Samples.Queries.ListSamples;

namespace Minibox.ServiceName.Application.Abstractions;

/// <summary>Port đọc mẫu (phía query của CQRS): Infrastructure chiếu thẳng ra DTO, không qua repository.</summary>
/// <remarks>Ghi đi qua <c>IRepository&lt;Sample, Guid&gt;</c> của BuildingBlocks; chỉ thêm port repository riêng ở đây khi cần truy vấn đặc thù.</remarks>
public interface ISampleReadService
{
    /// <summary>Liệt kê theo tên.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Danh sách, sắp theo tên.</returns>
    Task<SampleDto[]> ListAsync(CancellationToken cancellationToken = default);
}
