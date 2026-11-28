using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Domain.Errors;

namespace Minibox.Localization.Domain.Aggregates.Locales;

/// <summary>Ngôn ngữ cấu hình được (bảng <c>locale</c>, spec §6.6); định danh là mã BCP 47 vì bản dịch ở mọi service khoá theo mã này.</summary>
public sealed partial class Locale : AggregateRoot<string>
{
    /// <summary>Locale mặc định và fallback cuối của mọi chuỗi fallback.</summary>
    public const string Default = LocaleChain.Default;

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

    /// <summary>Phiên bản cho optimistic concurrency (cột <c>version</c>).</summary>
    public long Version { get; private set; }

    /// <summary>Tạo locale mới ở trạng thái tắt; phát <see cref="LocaleChanged"/>.</summary>
    /// <param name="code">Mã BCP 47 (ngôn ngữ chữ thường, subtag tuỳ chọn: <c>en</c>, <c>zh-Hant</c>).</param>
    /// <param name="name">Tên tự gọi, không rỗng, tối đa <see cref="NameMaxLength"/> ký tự.</param>
    /// <param name="sortOrder">Thứ tự hiển thị.</param>
    /// <param name="fallbackCode">Locale rơi về, khác <paramref name="code"/>; <c>null</c> = <see cref="Default"/>.</param>
    /// <returns>Locale mới; lỗi <see cref="LocalizationErrors.LocaleCodeInvalid"/>, <see cref="LocalizationErrors.LocaleNameInvalid"/>
    /// hoặc <see cref="LocalizationErrors.LocaleFallbackInvalid"/>.</returns>
    /// <remarks>Fallback phải tồn tại và không tạo vòng: kiểm bằng <see cref="Services.LocaleFallback.Validate"/> (cần đọc các locale khác).</remarks>
    public static Result<Locale> Create(string? code, string? name, int sortOrder, string? fallbackCode)
    {
        if (!IsValidCode(code))
            return LocalizationErrors.LocaleCodeInvalid;
        if (Check(code!, name, fallbackCode) is { } error)
            return error;
        var locale = new Locale(code!, name!.Trim(), sortOrder, fallbackCode);
        locale.RaiseChanged();
        return locale;
    }

    /// <summary>Sửa tên, bật/tắt, thứ tự, fallback; phát <see cref="LocaleChanged"/>. <see cref="Default"/> luôn bật và không có fallback.</summary>
    /// <param name="name">Tên tự gọi.</param>
    /// <param name="enabled">Bật cho người dùng chọn.</param>
    /// <param name="sortOrder">Thứ tự hiển thị.</param>
    /// <param name="fallbackCode">Locale rơi về; <c>null</c> = <see cref="Default"/>.</param>
    /// <returns>Thành công; lỗi <see cref="LocalizationErrors.LocaleNameInvalid"/>, <see cref="LocalizationErrors.LocaleFallbackInvalid"/>
    /// hoặc <see cref="LocalizationErrors.DefaultLocaleLocked"/>.</returns>
    public Result Update(string? name, bool enabled, int sortOrder, string? fallbackCode)
    {
        if (Check(Id, name, fallbackCode) is { } error)
            return error;
        if (Id == Default && (!enabled || fallbackCode is not null))
            return LocalizationErrors.DefaultLocaleLocked;
        (Name, Enabled, SortOrder, FallbackCode) = (name!.Trim(), enabled, sortOrder, fallbackCode);
        RaiseChanged();
        return Result.Success();
    }

    /// <summary>Kiểm mã đúng dạng BCP 47 được hỗ trợ (khớp CHECK của cột <c>locale.code</c>).</summary>
    /// <param name="code">Mã cần kiểm.</param>
    /// <returns><c>true</c> nếu hợp lệ.</returns>
    public static bool IsValidCode(string? code) => code is not null && CodePattern().IsMatch(code);

    static Error? Check(string code, string? name, string? fallbackCode)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
            return LocalizationErrors.LocaleNameInvalid;
        if (fallbackCode == code || (fallbackCode is not null && !IsValidCode(fallbackCode)))
            return LocalizationErrors.LocaleFallbackInvalid;
        return null;
    }

    void RaiseChanged() => Raise(new LocaleChanged(Id, Name, Enabled, SortOrder, FallbackCode));

    // Khớp CHECK của cột locale.code (database/schemas/localization/tables/locale.sql).
    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    private static partial Regex CodePattern();
}

/// <summary>Locale vừa được tạo hoặc sửa; mang trạng thái đầy đủ để service khác giữ bản sao danh sách locale.</summary>
/// <param name="Code">Mã BCP 47.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="Enabled">Đang bật.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <see cref="Locale.Default"/>.</param>
public sealed record LocaleChanged(string Code, string Name, bool Enabled, int SortOrder, string? FallbackCode) : IDomainEvent;
