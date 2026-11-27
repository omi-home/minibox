using Microsoft.EntityFrameworkCore;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Application.Features.Messages.Queries.GetPublishedMessages;
using Minibox.Localization.Application.Features.Messages.Queries.ListMessages;
using Minibox.Localization.Domain.Aggregates.Messages;

namespace Minibox.Localization.Infrastructure.Persistence;

/// <summary>Đọc chuỗi giao diện bằng LINQ <c>AsNoTracking</c> + <c>Select</c> ra DTO.</summary>
/// <param name="db">DbContext của request.</param>
public sealed class UiMessageReadService(LocalizationDbContext db) : IUiMessageReadService
{
    /// <inheritdoc/>
    public async Task<PublishedMessageRow[]> ListPublishedAsync(string @namespace, IReadOnlyCollection<string> localeCodes,
        CancellationToken cancellationToken = default) =>
        await db.Set<UiMessage>().AsNoTracking()
            .Where(m => m.Namespace == @namespace && localeCodes.Contains(m.LocaleCode) && m.PublishedText != null)
            .Select(m => new PublishedMessageRow(m.LocaleCode, m.Key, m.PublishedText!)).ToArrayAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<UiMessageDto[]> ListAsync(string @namespace, string? localeCode, CancellationToken cancellationToken = default) =>
        await db.Set<UiMessage>().AsNoTracking()
            .Where(m => m.Namespace == @namespace && (localeCode == null || m.LocaleCode == localeCode))
            .OrderBy(m => m.Key).ThenBy(m => m.LocaleCode)
            .Select(m => new UiMessageDto(m.Id, m.Namespace, m.Key, m.LocaleCode, m.DraftText, m.PublishedText, m.PublishedAt, m.UpdatedAt, m.Version))
            .ToArrayAsync(cancellationToken);
}
