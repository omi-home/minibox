using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Domain.Aggregates.Locales;

namespace Minibox.Localization.Domain.Errors;

/// <summary>Lỗi nghiệp vụ của service localization; mã ổn định để client và ProblemDetails dựa vào.</summary>
public static class LocalizationErrors
{
    /// <summary>Mã locale không theo dạng BCP 47 được hỗ trợ.</summary>
    public static readonly Error LocaleCodeInvalid =
        Error.Validation("Localization.LocaleCodeInvalid", "Mã ngôn ngữ phải theo BCP 47, ví dụ vi, en, zh-Hant.");

    /// <summary>Tên locale rỗng hoặc dài quá <see cref="Locale.NameMaxLength"/> ký tự.</summary>
    public static readonly Error LocaleNameInvalid =
        Error.Validation("Localization.LocaleNameInvalid", $"Tên ngôn ngữ không được rỗng và tối đa {Locale.NameMaxLength} ký tự.");

    /// <summary>Locale fallback trùng chính nó hoặc sai dạng.</summary>
    public static readonly Error LocaleFallbackInvalid =
        Error.Validation("Localization.LocaleFallbackInvalid", "Ngôn ngữ dự phòng phải khác ngôn ngữ đang sửa và theo BCP 47.");
}
