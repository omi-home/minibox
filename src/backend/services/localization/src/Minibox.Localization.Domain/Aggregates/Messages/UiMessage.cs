using System.Text.RegularExpressions;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Errors;

namespace Minibox.Localization.Domain.Aggregates.Messages;

/// <summary>Chuỗi giao diện của một (namespace, key, locale) (bảng <c>ui_message</c>, spec §6.6): sửa vào bản nháp, xuất bản chép sang bản công khai.</summary>
/// <remarks>Nội dung là văn bản thuần (có thể chứa placeholder như <c>{count}</c>), không phải HTML: frontend hiển thị như text,
/// không dùng <c>dangerouslySetInnerHTML</c>.</remarks>
public sealed partial class UiMessage : AggregateRoot<Guid>
{
    /// <summary>Độ dài tối đa của nội dung (khớp CHECK của <c>ui_message.draft_text</c>/<c>published_text</c>).</summary>
    public const int TextMaxLength = 10000;

    UiMessage(Guid id, string @namespace, string key, string localeCode, string draftText, DateTimeOffset updatedAt) : base(id) =>
        (Namespace, Key, LocaleCode, DraftText, UpdatedAt) = (@namespace, key, localeCode, draftText, updatedAt);

    /// <summary>Nhóm chuỗi (<c>shop</c>, <c>admin</c>, <c>common</c>…); BFF nạp theo nhóm.</summary>
    public string Namespace { get; private set; }

    /// <summary>Khoá chuỗi trong nhóm (<c>cart.title</c>).</summary>
    public string Key { get; private set; }

    /// <summary>Mã locale của bản dịch.</summary>
    public string LocaleCode { get; private set; }

    /// <summary>Bản nháp đang sửa.</summary>
    public string DraftText { get; private set; }

    /// <summary>Bản đã xuất bản (BFF chỉ đọc bản này); <c>null</c> = chưa xuất bản lần nào.</summary>
    public string? PublishedText { get; private set; }

    /// <summary>Thời điểm xuất bản gần nhất.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Thời điểm sửa nháp gần nhất.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Phiên bản cho optimistic concurrency (cột <c>version</c>).</summary>
    public long Version { get; private set; }

    /// <summary>Cờ bản nháp khác bản đã xuất bản (hoặc chưa xuất bản lần nào).</summary>
    public bool HasPendingChanges => PublishedText != DraftText;

    /// <summary>Tạo chuỗi mới ở dạng nháp; phát <see cref="UiMessageDraftSaved"/>.</summary>
    /// <param name="namespace">Nhóm chuỗi.</param>
    /// <param name="key">Khoá chuỗi.</param>
    /// <param name="localeCode">Mã locale (phải tồn tại; Application kiểm).</param>
    /// <param name="text">Nội dung văn bản thuần, 1..<see cref="TextMaxLength"/> ký tự.</param>
    /// <param name="now">Thời điểm sửa.</param>
    /// <returns>Chuỗi mới; lỗi <see cref="LocalizationErrors.MessageKeyInvalid"/>, <see cref="LocalizationErrors.LocaleCodeInvalid"/>
    /// hoặc <see cref="LocalizationErrors.MessageTextInvalid"/>.</returns>
    public static Result<UiMessage> Create(string? @namespace, string? key, string? localeCode, string? text, DateTimeOffset now)
    {
        if (!IsValidNamespace(@namespace) || !IsValidKey(key))
            return LocalizationErrors.MessageKeyInvalid;
        if (!Locale.IsValidCode(localeCode))
            return LocalizationErrors.LocaleCodeInvalid;
        if (!IsValidText(text))
            return LocalizationErrors.MessageTextInvalid;
        var message = new UiMessage(Guid.CreateVersion7(now), @namespace!, key!, localeCode!, text!, now);
        message.Raise(new UiMessageDraftSaved(message.Id, message.Namespace, message.Key, message.LocaleCode, text!));
        return message;
    }

    /// <summary>Sửa bản nháp; phát <see cref="UiMessageDraftSaved"/>. Bản công khai không đổi tới khi <see cref="Publish"/>.</summary>
    /// <param name="text">Nội dung mới.</param>
    /// <param name="now">Thời điểm sửa.</param>
    /// <returns>Thành công; lỗi <see cref="LocalizationErrors.MessageTextInvalid"/>.</returns>
    public Result Edit(string? text, DateTimeOffset now)
    {
        if (!IsValidText(text))
            return LocalizationErrors.MessageTextInvalid;
        (DraftText, UpdatedAt) = (text!, now);
        Raise(new UiMessageDraftSaved(Id, Namespace, Key, LocaleCode, text!));
        return Result.Success();
    }

    /// <summary>Xuất bản bản nháp nếu có thay đổi; phát <see cref="UiMessagePublished"/>.</summary>
    /// <param name="now">Thời điểm xuất bản.</param>
    /// <returns><c>true</c> nếu đã xuất bản; <c>false</c> nếu không có gì mới.</returns>
    public bool Publish(DateTimeOffset now)
    {
        if (!HasPendingChanges)
            return false;
        (PublishedText, PublishedAt) = (DraftText, now);
        Raise(new UiMessagePublished(Id, Namespace, Key, LocaleCode, DraftText));
        return true;
    }

    /// <summary>Kiểm nhóm chuỗi đúng dạng (khớp CHECK của <c>ui_message.namespace</c>).</summary>
    /// <param name="value">Giá trị cần kiểm.</param>
    /// <returns><c>true</c> nếu hợp lệ.</returns>
    public static bool IsValidNamespace(string? value) => value is not null && NamespacePattern().IsMatch(value);

    /// <summary>Kiểm khoá chuỗi đúng dạng (khớp CHECK của <c>ui_message.key</c>).</summary>
    /// <param name="value">Giá trị cần kiểm.</param>
    /// <returns><c>true</c> nếu hợp lệ.</returns>
    public static bool IsValidKey(string? value) => value is not null && KeyPattern().IsMatch(value);

    static bool IsValidText(string? text) => text is { Length: > 0 and <= TextMaxLength };

    [GeneratedRegex("^[a-z][a-z0-9_.-]{0,63}$")]
    private static partial Regex NamespacePattern();

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,200}$")]
    private static partial Regex KeyPattern();
}

/// <summary>Bản nháp của chuỗi vừa được tạo/sửa (ghi audit).</summary>
/// <param name="MessageId">Id chuỗi.</param>
/// <param name="Namespace">Nhóm.</param>
/// <param name="Key">Khoá.</param>
/// <param name="LocaleCode">Locale.</param>
/// <param name="Text">Nội dung nháp mới.</param>
public sealed record UiMessageDraftSaved(Guid MessageId, string Namespace, string Key, string LocaleCode, string Text) : IDomainEvent;

/// <summary>Chuỗi vừa được xuất bản (ghi audit, phát <c>localization.MessagesPublished.v1</c>).</summary>
/// <param name="MessageId">Id chuỗi.</param>
/// <param name="Namespace">Nhóm.</param>
/// <param name="Key">Khoá.</param>
/// <param name="LocaleCode">Locale.</param>
/// <param name="Text">Nội dung đã xuất bản.</param>
public sealed record UiMessagePublished(Guid MessageId, string Namespace, string Key, string LocaleCode, string Text) : IDomainEvent;
