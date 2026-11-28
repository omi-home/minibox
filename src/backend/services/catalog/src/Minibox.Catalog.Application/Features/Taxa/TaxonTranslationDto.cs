using FluentValidation;
using Minibox.Catalog.Domain.Aggregates.Taxa;

namespace Minibox.Catalog.Application.Features.Taxa;

/// <summary>Bản dịch của bộ sưu tập/thẻ ở một locale, dùng cho request/response (Api không dùng kiểu Domain).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Slug">Slug theo ngôn ngữ.</param>
public sealed record TaxonTranslationDto(string Locale, string Name, string? Description, string Slug)
{
    /// <summary>Số bản dịch tối đa mỗi request (chặn payload phình).</summary>
    public const int MaxCount = 50;

    /// <summary>Chuyển sang đầu vào của aggregate.</summary>
    /// <param name="items">Bản dịch nhận từ client; <c>null</c> coi như rỗng.</param>
    /// <returns>Bản dịch cho <see cref="Taxon"/>.</returns>
    public static TaxonText[] ToDomain(IEnumerable<TaxonTranslationDto>? items) =>
        items?.Select(t => new TaxonText(t.Locale, t.Name, t.Description, t.Slug)).ToArray() ?? [];

    /// <summary>Lấy bản dịch hiện hành (đã chuẩn hoá) của taxon.</summary>
    /// <param name="taxon">Taxon.</param>
    /// <returns>Bản dịch dạng đầu vào aggregate, để kiểm slug trùng.</returns>
    public static TaxonText[] Current(Taxon taxon) =>
        taxon.Translations.Select(t => new TaxonText(t.Locale, t.Name, t.Description, t.Slug)).ToArray();
}

/// <summary>Luật chung cho dữ liệu vào của lệnh taxon.</summary>
public static class TaxonRules
{
    /// <summary>Thêm luật: loại là <c>collection</c>/<c>tag</c>.</summary>
    /// <typeparam name="T">Kiểu lệnh.</typeparam>
    /// <param name="rule">Luật trên trường loại.</param>
    /// <returns>Luật để nối tiếp.</returns>
    public static IRuleBuilderOptions<T, string> TaxonKind<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(TaxonKinds.IsValid).WithMessage("Loại phải là 'collection' hoặc 'tag'.");

    /// <summary>Thêm luật: có 1..<see cref="TaxonTranslationDto.MaxCount"/> bản dịch.</summary>
    /// <typeparam name="T">Kiểu lệnh.</typeparam>
    /// <param name="rule">Luật trên trường bản dịch.</param>
    /// <returns>Luật để nối tiếp.</returns>
    public static IRuleBuilderOptions<T, TaxonTranslationDto[]> Translations<T>(this IRuleBuilder<T, TaxonTranslationDto[]> rule) =>
        rule.NotEmpty().Must(x => x.Length <= TaxonTranslationDto.MaxCount).WithMessage($"Tối đa {TaxonTranslationDto.MaxCount} bản dịch.");
}
