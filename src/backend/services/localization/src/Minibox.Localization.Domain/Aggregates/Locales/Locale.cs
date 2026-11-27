using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Domain.Errors;

namespace Minibox.Localization.Domain.Aggregates.Locales;

/// <summary>Ngôn ngữ cấu hình được (bảng <c>locale</c>, spec §6.6); định danh là mã BCP 47 vì bản dịch ở mọi service khoá theo mã này.</summary>
public sealed partial class Locale : AggregateRoot<string>
{
    /// <summary>Locale mặc định và fallback cuối của mọi chuỗi fallback.</summary>
    public const string Default = "vi";

    /// <summary>Độ dài tối đa của <see cref="Name"/>.</summary>
    public const int NameMaxLength = 100;

    Locale(string id, string name, int sortOrder, string? fallbackCode) : base(id) =>
        (Name, SortOrder, FallbackCode) = (name, sortOrder, fallbackCode);

    /// <summary>Tên tự gọi của ngôn ngữ (<c>Tiếng Việt</c>, <c>English</c>).</summary>
    public string Name { get; private set; }

    /// <summary>Đang bật cho người dùng chọn; locale mới tạo ở trạng thái tắt.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Thứ tự hiển thị trong bộ chọn ngôn ngữ (nhỏ trước).</summary>
    public int SortOrder { get; private set; }

    /// <summary>Locale rơi về khi thiếu bản dịch; <c>null</c> nghĩa là rơi thẳng về <see cref="Default"/>.</summary>
    public string? FallbackCode { get; private set; }

    /// <summary>Tạo locale mới ở trạng thái tắt.</summary>
    /// <param name="code">Mã BCP 47 (ngôn ngữ chữ thường, subtag tuỳ chọn: <c>en</c>, <c>zh-Hant</c>).</param>
    /// <param name="name">Tên tự gọi, không rỗng, tối đa <see cref="NameMaxLength"/> ký tự.</param>
    /// <param name="sortOrder">Thứ tự hiển thị.</param>
    /// <param name="fallbackCode">Locale rơi về, khác <paramref name="code"/>; <c>null</c> = <see cref="Default"/>.</param>
    /// <returns>Locale mới; lỗi <see cref="LocalizationErrors.LocaleCodeInvalid"/>, <see cref="LocalizationErrors.LocaleNameInvalid"/>
    /// hoặc <see cref="LocalizationErrors.LocaleFallbackInvalid"/>.</returns>
    /// <remarks>Fallback phải tồn tại và không tạo vòng: Application kiểm khi tạo/sửa (cần đọc các locale khác).</remarks>
    public static Result<Locale> Create(string? code, string? name, int sortOrder, string? fallbackCode)
    {
        if (code is null || !CodePattern().IsMatch(code))
            return LocalizationErrors.LocaleCodeInvalid;
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
            return LocalizationErrors.LocaleNameInvalid;
        if (fallbackCode == code || (fallbackCode is not null && !CodePattern().IsMatch(fallbackCode)))
            return LocalizationErrors.LocaleFallbackInvalid;
        return new Locale(code, name.Trim(), sortOrder, fallbackCode);
    }

    // Khớp CHECK của cột locale.code (database/schemas/localization/tables/locale.sql).
    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex CodePattern();
}
