using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Domain.Aggregates.Messages;

namespace Minibox.Localization.Application.Features.Messages.Commands.PublishMessages;

/// <summary>Lệnh xuất bản mọi bản nháp đang chờ của (namespace, locale).</summary>
/// <param name="Namespace">Nhóm.</param>
/// <param name="Locale">Locale.</param>
public sealed record PublishMessages(string Namespace, string Locale) : ICommand<PublishMessagesResult>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => LocalizationPermissions.MessagePublish;
}

/// <summary>Kết quả xuất bản.</summary>
/// <param name="Published">Số chuỗi vừa xuất bản (0 nếu không có gì mới).</param>
public sealed record PublishMessagesResult(int Published);

/// <summary>Chặn namespace sai dạng.</summary>
public sealed class PublishMessagesValidator : AbstractValidator<PublishMessages>
{
    /// <summary>Khai báo luật: namespace đúng dạng <see cref="UiMessage.IsValidNamespace"/>.</summary>
    public PublishMessagesValidator() => RuleFor(x => x.Namespace).Must(UiMessage.IsValidNamespace);
}

/// <summary>Xuất bản từng chuỗi qua aggregate (kiểm phiên bản); mỗi chuỗi ghi audit và phát <c>localization.MessagesPublished.v1</c> qua outbox
/// trong cùng transaction — bản nháp bị sửa song song thì cả lệnh rollback.</summary>
/// <param name="messages">Repository chuỗi.</param>
/// <param name="clock">Đồng hồ.</param>
public sealed class PublishMessagesHandler(IUiMessageRepository messages, TimeProvider clock) : ICommandHandler<PublishMessages, PublishMessagesResult>
{
    /// <inheritdoc/>
    /// <returns>Số chuỗi đã xuất bản; lỗi xung đột phiên bản khi bản nháp bị sửa song song.</returns>
    public async ValueTask<Result<PublishMessagesResult>> Handle(PublishMessages command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var pending = await messages.ListPendingAsync(command.Namespace, command.Locale, cancellationToken);
        foreach (var message in pending)
        {
            var version = message.Version;
            message.Publish(now);
            var saved = await messages.UpdateAsync(message, version, cancellationToken);
            if (saved.IsFailure)
                return saved.Error;
        }
        return new PublishMessagesResult(pending.Count);
    }
}
