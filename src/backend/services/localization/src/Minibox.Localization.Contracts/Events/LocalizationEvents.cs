namespace Minibox.Localization.Contracts.Events;

/// <summary>Integration event <c>localization.MessagesPublished.v1</c>: chuỗi giao diện của (locale, namespace) vừa được xuất bản;
/// BFF làm mới cache namespace đó (cả các locale rơi về locale này).</summary>
/// <param name="Locale">Mã locale BCP 47.</param>
/// <param name="Namespace">Nhóm chuỗi.</param>
/// <param name="Keys">Các key vừa xuất bản (ít nhất một).</param>
/// <remarks>Hợp đồng là <c>contracts/events/localization.MessagesPublished.v1.json</c>; đổi phá vỡ thì tạo <c>V2</c>, không sửa V1.
/// Không mang nội dung chuỗi: BFF nạp lại qua API công khai.</remarks>
public sealed record MessagesPublishedV1(string Locale, string Namespace, IReadOnlyList<string> Keys);

/// <summary>Integration event <c>localization.LocaleChanged.v1</c>: locale vừa được tạo hoặc sửa; mang trạng thái đầy đủ để service khác
/// giữ bản sao danh sách locale.</summary>
/// <param name="Code">Mã BCP 47.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="Enabled">Đang bật.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <c>vi</c>.</param>
/// <remarks>Hợp đồng là <c>contracts/events/localization.LocaleChanged.v1.json</c>.</remarks>
public sealed record LocaleChangedV1(string Code, string Name, bool Enabled, int SortOrder, string? FallbackCode);
