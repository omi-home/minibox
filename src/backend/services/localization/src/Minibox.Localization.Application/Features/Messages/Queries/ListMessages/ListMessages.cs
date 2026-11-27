using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Domain.Aggregates.Messages;

namespace Minibox.Localization.Application.Features.Messages.Queries.ListMessages;

/// <summary>Truy vấn chuỗi (nháp và bản công khai) của một namespace cho màn quản trị bản dịch.</summary>
/// <param name="Namespace">Nhóm chuỗi.</param>
/// <param name="Locale">Lọc theo locale; <c>null</c> = mọi locale.</param>
public sealed record ListMessages(string Namespace, string? Locale) : IQuery<UiMessageDto[]>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => LocalizationPermissions.MessageManage;
}

/// <summary>Chuỗi giao diện cho quản trị.</summary>
/// <param name="Id">Id chuỗi.</param>
/// <param name="Namespace">Nhóm.</param>
/// <param name="Key">Khoá.</param>
/// <param name="Locale">Locale.</param>
/// <param name="DraftText">Bản nháp.</param>
/// <param name="PublishedText">Bản đã xuất bản; <c>null</c> = chưa xuất bản.</param>
/// <param name="PublishedAt">Thời điểm xuất bản gần nhất.</param>
/// <param name="UpdatedAt">Thời điểm sửa nháp gần nhất.</param>
/// <param name="Version">Phiên bản gửi lại khi sửa.</param>
public sealed record UiMessageDto(Guid Id, string Namespace, string Key, string Locale, string DraftText, string? PublishedText,
    DateTimeOffset? PublishedAt, DateTimeOffset UpdatedAt, long Version);

/// <summary>Chặn namespace sai dạng.</summary>
public sealed class ListMessagesValidator : AbstractValidator<ListMessages>
{
    /// <summary>Khai báo luật: namespace đúng dạng <see cref="UiMessage.IsValidNamespace"/>.</summary>
    public ListMessagesValidator() => RuleFor(x => x.Namespace).Must(UiMessage.IsValidNamespace);
}

/// <summary>Đọc qua <see cref="IUiMessageReadService"/>.</summary>
/// <param name="reader">Port đọc chuỗi.</param>
public sealed class ListMessagesHandler(IUiMessageReadService reader) : IQueryHandler<ListMessages, UiMessageDto[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<UiMessageDto[]>> Handle(ListMessages query, CancellationToken cancellationToken) =>
        await reader.ListAsync(query.Namespace, query.Locale, cancellationToken);
}
