using FluentValidation;
using Minibox.Catalog.Domain.Aggregates.Products;

namespace Minibox.Catalog.Application.Features.Products;

/// <summary>Bản dịch của sản phẩm ở một locale, dùng cho request/response (Api không dùng kiểu Domain).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Slug">Slug theo ngôn ngữ.</param>
/// <param name="SeoTitle">Tiêu đề SEO.</param>
/// <param name="SeoDescription">Mô tả SEO.</param>
public sealed record ProductTranslationDto(string Locale, string Name, string? Description, string Slug, string? SeoTitle, string? SeoDescription)
{
    /// <summary>Chuyển sang đầu vào của aggregate.</summary>
    /// <param name="items">Bản dịch nhận từ client; <c>null</c> coi như rỗng.</param>
    /// <returns>Bản dịch cho <see cref="Product"/>.</returns>
    public static ProductText[] ToDomain(IEnumerable<ProductTranslationDto>? items) =>
        items?.Select(t => t is null ? null! : new ProductText(t.Locale, t.Name, t.Description, t.Slug, t.SeoTitle, t.SeoDescription)).ToArray() ?? [];

    /// <summary>Lấy bản dịch hiện hành (đã chuẩn hoá) của sản phẩm.</summary>
    /// <param name="product">Sản phẩm.</param>
    /// <returns>Bản dịch dạng đầu vào aggregate, để kiểm slug trùng.</returns>
    public static ProductText[] Current(Product product) =>
        product.Translations.Select(t => new ProductText(t.Locale, t.Name, t.Description, t.Slug, t.SeoTitle, t.SeoDescription)).ToArray();
}

/// <summary>Giá trị của một thuộc tính mô tả.</summary>
/// <param name="Code">Mã thuộc tính trong nhóm của sản phẩm.</param>
/// <param name="Values">Giá trị (một phần tử, trừ <c>multi_select</c>); number dạng invariant, boolean <c>true</c>/<c>false</c>, select là mã giá trị cho phép.</param>
public sealed record ProductAttributeDto(string Code, string[] Values)
{
    /// <summary>Chuyển sang đầu vào của aggregate.</summary>
    /// <param name="items">Giá trị nhận từ client; <c>null</c> coi như rỗng.</param>
    /// <returns>Giá trị cho <see cref="Product"/>.</returns>
    public static AttributeValueInput[] ToDomain(IEnumerable<ProductAttributeDto>? items) =>
        items?.Select(a => a is null ? null! : new AttributeValueInput(a.Code, a.Values ?? [])).ToArray() ?? [];
}

/// <summary>Luật chung cho dữ liệu vào của lệnh sản phẩm (chặn payload phình; chi tiết do aggregate kiểm).</summary>
public static class ProductRules
{
    /// <summary>Số bản dịch tối đa.</summary>
    public const int MaxTranslations = 50;

    /// <summary>Số thuộc tính tối đa mỗi request.</summary>
    public const int MaxAttributes = 100;

    /// <summary>Số giá trị tối đa mỗi thuộc tính.</summary>
    public const int MaxValues = 200;

    /// <summary>Thêm luật: có 1..<see cref="MaxTranslations"/> bản dịch.</summary>
    /// <typeparam name="T">Kiểu lệnh.</typeparam>
    /// <param name="rule">Luật trên trường bản dịch.</param>
    /// <returns>Luật để nối tiếp.</returns>
    public static IRuleBuilderOptions<T, ProductTranslationDto[]> Translations<T>(this IRuleBuilder<T, ProductTranslationDto[]> rule) =>
        rule.NotEmpty().Must(x => x.Length <= MaxTranslations).WithMessage($"Tối đa {MaxTranslations} bản dịch.");

    /// <summary>Thêm luật: tối đa <see cref="MaxAttributes"/> thuộc tính, mỗi thuộc tính tối đa <see cref="MaxValues"/> giá trị.</summary>
    /// <typeparam name="T">Kiểu lệnh.</typeparam>
    /// <param name="rule">Luật trên trường thuộc tính.</param>
    /// <returns>Luật để nối tiếp.</returns>
    public static IRuleBuilderOptions<T, ProductAttributeDto[]> Attributes<T>(this IRuleBuilder<T, ProductAttributeDto[]> rule) =>
        rule.NotNull().Must(x => x.Length <= MaxAttributes && x.All(a => a is null || (a.Values?.Length ?? 0) <= MaxValues))
            .WithMessage($"Tối đa {MaxAttributes} thuộc tính, {MaxValues} giá trị mỗi thuộc tính.");
}
