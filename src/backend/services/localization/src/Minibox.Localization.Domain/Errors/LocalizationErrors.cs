using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Aggregates.Messages;

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

    /// <summary>Locale fallback không tồn tại.</summary>
    public static readonly Error LocaleFallbackNotFound =
        Error.Validation("Localization.LocaleFallbackNotFound", "Ngôn ngữ dự phòng không tồn tại.");

    /// <summary>Fallback tạo vòng (ja → ko → ja).</summary>
    public static readonly Error LocaleFallbackCycle =
        Error.Validation("Localization.LocaleFallbackCycle", "Ngôn ngữ dự phòng tạo vòng lặp.");

    /// <summary>Tắt hoặc đặt fallback cho locale mặc định.</summary>
    public static readonly Error DefaultLocaleLocked =
        Error.Failure("Localization.DefaultLocaleLocked", $"Ngôn ngữ mặc định '{Locale.Default}' luôn bật và không có ngôn ngữ dự phòng.");

    /// <summary>Không có locale (với API công khai: hoặc locale đang tắt).</summary>
    public static readonly Error LocaleNotFound = Error.NotFound("Localization.LocaleNotFound", "Không có ngôn ngữ này.");

    /// <summary>Tạo locale trùng mã.</summary>
    public static readonly Error LocaleExists = Error.Conflict("Localization.LocaleExists", "Ngôn ngữ đã tồn tại.");

    /// <summary>Namespace hoặc key của chuỗi sai dạng.</summary>
    public static readonly Error MessageKeyInvalid =
        Error.Validation("Localization.MessageKeyInvalid", "Namespace dạng a-z0-9_.- (tối đa 64 ký tự), key dạng A-Za-z0-9_.- (tối đa 200 ký tự).");

    /// <summary>Nội dung chuỗi rỗng hoặc quá dài.</summary>
    public static readonly Error MessageTextInvalid =
        Error.Validation("Localization.MessageTextInvalid", $"Nội dung chuỗi không được rỗng và tối đa {UiMessage.TextMaxLength} ký tự.");

    /// <summary>Sửa chuỗi đã có mà không gửi phiên bản đang sửa.</summary>
    public static readonly Error MessageVersionRequired =
        Error.Conflict("Localization.MessageVersionRequired", "Chuỗi đã tồn tại; gửi kèm version đang sửa.");

    /// <summary>Gửi version cho chuỗi chưa tồn tại.</summary>
    public static readonly Error MessageNotFound = Error.NotFound("Localization.MessageNotFound", "Không có chuỗi này.");
}
