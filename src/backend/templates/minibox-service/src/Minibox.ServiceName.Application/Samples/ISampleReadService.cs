namespace Minibox.ServiceName.Application.Samples;

/// <summary>Dòng đọc của <c>Sample</c> trả cho query.</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Tên hiển thị.</param>
public sealed record SampleDto(Guid Id, string Name);

/// <summary>Read service mẫu (phía query của CQRS): chiếu thẳng ra DTO, không qua repository.</summary>
public interface ISampleReadService
{
    /// <summary>Liệt kê theo tên.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Danh sách, sắp theo tên.</returns>
    Task<IReadOnlyList<SampleDto>> ListAsync(CancellationToken cancellationToken = default);
}
