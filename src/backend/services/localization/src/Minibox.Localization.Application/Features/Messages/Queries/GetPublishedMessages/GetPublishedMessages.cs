using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Domain.Aggregates.Messages;
using Minibox.Localization.Domain.Errors;
using Minibox.Localization.Domain.Services;

namespace Minibox.Localization.Application.Features.Messages.Queries.GetPublishedMessages;

/// <summary>Truy vấn công khai chuỗi đã xuất bản của một namespace theo locale, đã áp chuỗi fallback (BFF shop/admin nạp và cache).</summary>
/// <param name="Locale">Mã locale đang bật.</param>
/// <param name="Namespace">Nhóm chuỗi.</param>
public sealed record GetPublishedMessages(string Locale, string Namespace) : IQuery<PublishedMessages>;

/// <summary>Thân phản hồi: chuỗi đã xuất bản (văn bản thuần, không phải HTML).</summary>
/// <param name="Locale">Locale được yêu cầu.</param>
/// <param name="Namespace">Nhóm chuỗi.</param>
/// <param name="Messages">Key → nội dung; key thiếu ở locale được lấy từ locale fallback kế tiếp, cuối cùng là <c>vi</c>.</param>
public sealed record PublishedMessagesDto(string Locale, string Namespace, IReadOnlyDictionary<string, string> Messages);

/// <summary>Kết quả kèm ETag để client gửi lại <c>If-None-Match</c>.</summary>
/// <param name="Body">Thân phản hồi.</param>
/// <param name="ETag">ETag mạnh (có dấu nháy) tính từ nội dung đã áp fallback.</param>
public sealed record PublishedMessages(PublishedMessagesDto Body, string ETag);

/// <summary>Một chuỗi đã xuất bản đọc từ DB.</summary>
/// <param name="LocaleCode">Locale của bản dịch.</param>
/// <param name="Key">Khoá.</param>
/// <param name="Text">Nội dung đã xuất bản.</param>
public sealed record PublishedMessageRow(string LocaleCode, string Key, string Text);

/// <summary>Chặn namespace sai dạng trước khi đọc DB.</summary>
public sealed class GetPublishedMessagesValidator : AbstractValidator<GetPublishedMessages>
{
    /// <summary>Khai báo luật: namespace đúng dạng <see cref="UiMessage.IsValidNamespace"/>.</summary>
    public GetPublishedMessagesValidator() => RuleFor(x => x.Namespace).Must(UiMessage.IsValidNamespace);
}

/// <summary>Dựng chuỗi fallback của locale, chọn bản có ưu tiên cao nhất cho mỗi key, tính ETag từ nội dung.</summary>
/// <param name="locales">Port đọc locale.</param>
/// <param name="messages">Port đọc chuỗi.</param>
public sealed class GetPublishedMessagesHandler(ILocaleReadService locales, IUiMessageReadService messages)
    : IQueryHandler<GetPublishedMessages, PublishedMessages>
{
    /// <inheritdoc/>
    /// <returns>Chuỗi đã áp fallback kèm ETag; lỗi <see cref="LocalizationErrors.LocaleNotFound"/> khi locale không có hoặc đang tắt.</returns>
    public async ValueTask<Result<PublishedMessages>> Handle(GetPublishedMessages query, CancellationToken cancellationToken)
    {
        var all = await locales.ListAllAsync(cancellationToken);
        if (!all.Any(l => l.Code == query.Locale && l.Enabled))
            return LocalizationErrors.LocaleNotFound;
        // Vòng fallback bị chặn khi ghi (LocaleFallback.Validate); nếu dữ liệu vẫn có vòng thì chỉ dùng locale đó rồi vi.
        var chain = LocaleFallback.Chain(query.Locale, all.ToDictionary(l => l.Code, l => l.FallbackCode))
                    ?? [query.Locale, Domain.Aggregates.Locales.Locale.Default];
        var rows = await messages.ListPublishedAsync(query.Namespace, chain, cancellationToken);

        var resolved = new SortedDictionary<string, string>(
            LocaleChain.PickPerKey(rows, r => r.Key, r => r.LocaleCode, chain).ToDictionary(p => p.Key, p => p.Value.Text), StringComparer.Ordinal);
        var body = new PublishedMessagesDto(query.Locale, query.Namespace, resolved);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body));
        return new PublishedMessages(body, $"\"{Convert.ToHexStringLower(hash.AsSpan(0, 16))}\"");
    }
}
