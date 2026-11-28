using FluentValidation;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;

namespace Minibox.Catalog.Application.Features.ProductTypes;

/// <summary>Nhãn ở một locale, dùng cho request/response (Api không dùng kiểu Domain).</summary>
/// <param name="Locale">Mã BCP 47.</param>
/// <param name="Label">Nhãn hiển thị.</param>
public sealed record LabelDto(string Locale, string Label);

/// <summary>Giá trị cho phép của thuộc tính select/multi_select.</summary>
/// <param name="Code">Mã ổn định trong thuộc tính.</param>
/// <param name="Labels">Nhãn theo locale, bắt buộc có <c>vi</c>.</param>
public sealed record AttributeValueDto(string Code, LabelDto[] Labels);

/// <summary>Định nghĩa thuộc tính của nhóm sản phẩm.</summary>
/// <param name="Code">Mã ổn định trong nhóm.</param>
/// <param name="DataType">Kiểu: <c>text</c>, <c>number</c>, <c>boolean</c>, <c>select</c>, <c>multi_select</c>.</param>
/// <param name="Unit">Đơn vị (chỉ <c>number</c>).</param>
/// <param name="Required">Bắt buộc có giá trị trước khi gửi duyệt sản phẩm.</param>
/// <param name="Variant">Tạo biến thể (chỉ <c>select</c>); <c>false</c> = thuộc tính mô tả.</param>
/// <param name="Labels">Nhãn theo locale, bắt buộc có <c>vi</c>.</param>
/// <param name="Values">Giá trị cho phép theo thứ tự hiển thị; rỗng với kiểu không phải select/multi_select.</param>
public sealed record AttributeDto(string Code, string DataType, string? Unit, bool Required, bool Variant, LabelDto[] Labels, AttributeValueDto[] Values)
{
    /// <summary>Chuyển sang đầu vào của aggregate.</summary>
    /// <param name="items">Thuộc tính nhận từ client; <c>null</c> coi như rỗng.</param>
    /// <returns>Định nghĩa cho <see cref="ProductType"/>.</returns>
    public static AttributeSpec[] ToDomain(IEnumerable<AttributeDto>? items) =>
        items?.Select(a => a is null ? null! : new AttributeSpec(a.Code, a.DataType, a.Unit, a.Required, a.Variant, ToLabels(a.Labels),
            a.Values?.Select(v => v is null ? null! : new AttributeValueSpec(v.Code, ToLabels(v.Labels))).ToArray() ?? [])).ToArray() ?? [];

    /// <summary>Chuyển nhãn sang đầu vào của aggregate.</summary>
    /// <param name="items">Nhãn nhận từ client; <c>null</c> coi như rỗng.</param>
    /// <returns>Nhãn cho <see cref="ProductType"/>.</returns>
    public static LocalizedLabel[] ToLabels(IEnumerable<LabelDto>? items) =>
        items?.Select(l => l is null ? null! : new LocalizedLabel(l.Locale, l.Label)).ToArray() ?? [];
}

/// <summary>Luật chung cho dữ liệu vào của lệnh nhóm sản phẩm (chặn payload phình; chi tiết do aggregate kiểm).</summary>
public static class ProductTypeRules
{
    /// <summary>Số nhãn tối đa mỗi danh sách.</summary>
    public const int MaxLabels = 50;

    /// <summary>Số thuộc tính tối đa mỗi nhóm.</summary>
    public const int MaxAttributes = 100;

    /// <summary>Số giá trị cho phép tối đa mỗi thuộc tính.</summary>
    public const int MaxValues = 200;

    /// <summary>Thêm luật: tên nhóm có 1..<see cref="MaxLabels"/> bản dịch.</summary>
    /// <typeparam name="T">Kiểu lệnh.</typeparam>
    /// <param name="rule">Luật trên trường tên.</param>
    /// <returns>Luật để nối tiếp.</returns>
    public static IRuleBuilderOptions<T, LabelDto[]> Names<T>(this IRuleBuilder<T, LabelDto[]> rule) =>
        rule.NotEmpty().Must(x => x.Length <= MaxLabels).WithMessage($"Tối đa {MaxLabels} bản dịch.");

    /// <summary>Thêm luật: tối đa <see cref="MaxAttributes"/> thuộc tính, mỗi thuộc tính tối đa <see cref="MaxLabels"/> nhãn và
    /// <see cref="MaxValues"/> giá trị, mỗi giá trị tối đa <see cref="MaxLabels"/> nhãn.</summary>
    /// <typeparam name="T">Kiểu lệnh.</typeparam>
    /// <param name="rule">Luật trên trường thuộc tính.</param>
    /// <returns>Luật để nối tiếp.</returns>
    public static IRuleBuilderOptions<T, AttributeDto[]> Attributes<T>(this IRuleBuilder<T, AttributeDto[]> rule) =>
        rule.NotNull().Must(x => x.Length <= MaxAttributes && x.All(a => a is null ||
                (a.Labels?.Length ?? 0) <= MaxLabels && (a.Values?.Length ?? 0) <= MaxValues && (a.Values ?? []).All(v => v is null || (v.Labels?.Length ?? 0) <= MaxLabels)))
            .WithMessage($"Tối đa {MaxAttributes} thuộc tính, {MaxValues} giá trị mỗi thuộc tính, {MaxLabels} nhãn mỗi mục.");
}
