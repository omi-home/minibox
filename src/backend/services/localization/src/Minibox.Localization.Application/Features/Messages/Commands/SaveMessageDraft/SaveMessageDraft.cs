using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Domain.Aggregates.Messages;
using Minibox.Localization.Domain.Errors;

namespace Minibox.Localization.Application.Features.Messages.Commands.SaveMessageDraft;

/// <summary>Lệnh tạo hoặc sửa bản nháp của một chuỗi; bản công khai chỉ đổi khi xuất bản.</summary>
/// <param name="Namespace">Nhóm.</param>
/// <param name="Key">Khoá.</param>
/// <param name="Locale">Locale (phải tồn tại, có thể đang tắt để chuẩn bị trước).</param>
/// <param name="Text">Nội dung văn bản thuần.</param>
/// <param name="Version">Phiên bản lúc tải khi sửa chuỗi đã có; <c>null</c> khi tạo mới.</param>
public sealed record SaveMessageDraft(string Namespace, string Key, string Locale, string Text, long? Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => LocalizationPermissions.MessageManage;
}

/// <summary>Chặn nội dung rỗng/quá dài và khoá sai dạng trước khi mở transaction.</summary>
public sealed class SaveMessageDraftValidator : AbstractValidator<SaveMessageDraft>
{
    /// <summary>Khai báo luật: namespace/key đúng dạng, nội dung 1..<see cref="UiMessage.TextMaxLength"/> ký tự.</summary>
    public SaveMessageDraftValidator()
    {
        RuleFor(x => x.Namespace).Must(UiMessage.IsValidNamespace);
        RuleFor(x => x.Key).Must(UiMessage.IsValidKey);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(UiMessage.TextMaxLength);
    }
}

/// <summary>Tạo hoặc sửa <see cref="UiMessage"/> qua aggregate; audit đi qua Unit of Work.</summary>
/// <param name="messages">Repository chuỗi.</param>
/// <param name="locales">Repository locale.</param>
/// <param name="clock">Đồng hồ.</param>
public sealed class SaveMessageDraftHandler(IUiMessageRepository messages, ILocaleRepository locales, TimeProvider clock)
    : ICommandHandler<SaveMessageDraft>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="LocalizationErrors.LocaleNotFound"/>, <see cref="LocalizationErrors.MessageVersionRequired"/>,
    /// <see cref="LocalizationErrors.MessageNotFound"/>, lỗi dạng dữ liệu của <see cref="UiMessage"/> hoặc xung đột phiên bản.</returns>
    /// <remarks>Hai lệnh tạo cùng khoá song song: lệnh sau vi phạm UNIQUE và thất bại (client tải lại rồi sửa với version).</remarks>
    public async ValueTask<Result> Handle(SaveMessageDraft command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var message = await messages.FindAsync(command.Namespace, command.Key, command.Locale, cancellationToken);
        if (message is not null)
        {
            if (command.Version is not { } version)
                return LocalizationErrors.MessageVersionRequired;
            var edited = message.Edit(command.Text, now);
            return edited.IsFailure ? edited : await messages.UpdateAsync(message, version, cancellationToken);
        }
        if (command.Version is not null)
            return LocalizationErrors.MessageNotFound;
        if (await locales.GetAsync(command.Locale, cancellationToken) is null)
            return LocalizationErrors.LocaleNotFound;
        var created = UiMessage.Create(command.Namespace, command.Key, command.Locale, command.Text, now);
        if (created.IsFailure)
            return created.Error;
        await messages.AddAsync(created.Value, cancellationToken);
        return Result.Success();
    }
}
