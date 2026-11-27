using Minibox.Localization.Application.Features.Messages.Queries.GetPublishedMessages;
using Minibox.Localization.Application.Features.Messages.Queries.ListMessages;

namespace Minibox.Localization.Application.Abstractions;

/// <summary>Port đọc chuỗi giao diện (phía query của CQRS): Infrastructure chiếu thẳng ra DTO.</summary>
public interface IUiMessageReadService
{
    /// <summary>Đọc bản đã xuất bản của một namespace trong các locale cho trước.</summary>
    /// <param name="namespace">Nhóm.</param>
    /// <param name="localeCodes">Các locale cần đọc (chuỗi fallback).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Mọi chuỗi đã xuất bản thuộc các locale đó.</returns>
    Task<PublishedMessageRow[]> ListPublishedAsync(string @namespace, IReadOnlyCollection<string> localeCodes, CancellationToken cancellationToken = default);

    /// <summary>Liệt kê chuỗi (nháp và bản công khai) cho màn quản trị.</summary>
    /// <param name="namespace">Nhóm.</param>
    /// <param name="localeCode">Lọc theo locale; <c>null</c> = mọi locale.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Chuỗi sắp theo key rồi locale.</returns>
    Task<UiMessageDto[]> ListAsync(string @namespace, string? localeCode, CancellationToken cancellationToken = default);
}
