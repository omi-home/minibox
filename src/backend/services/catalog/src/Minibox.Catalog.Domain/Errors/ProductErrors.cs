using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Aggregates.Products;

namespace Minibox.Catalog.Domain.Errors;

/// <summary>Lỗi nghiệp vụ của <see cref="Product"/>; mã ổn định cho client và ProblemDetails.</summary>
public static class ProductErrors
{
    /// <summary>Không có sản phẩm theo Id.</summary>
    public static readonly Error NotFound = Error.NotFound("Product.NotFound", "Không tìm thấy sản phẩm.");

    /// <summary>Bản dịch sai: locale không đúng BCP 47, tên rỗng/quá dài, mô tả/SEO quá dài, slug sai dạng, hoặc locale lặp.</summary>
    public static readonly Error TranslationInvalid = Error.Validation("Product.TranslationInvalid",
        $"Bản dịch không hợp lệ: locale BCP 47 không lặp, tên 1..{Product.NameMaxLength} ký tự, mô tả tối đa {Product.DescriptionMaxLength}, " +
        $"SEO title tối đa {Product.SeoTitleMaxLength}, SEO description tối đa {Product.SeoDescriptionMaxLength}, " +
        $"slug chữ thường/số nối bằng '-' tối đa {Product.SlugMaxLength} ký tự.");

    /// <summary>Thiếu bản dịch <c>vi</c> (fallback cuối).</summary>
    public static readonly Error DefaultTranslationMissing = Error.Validation("Product.DefaultTranslationMissing",
        $"Sản phẩm phải có bản dịch '{LocaleChain.Default}'.");

    /// <summary>Slug đã được sản phẩm khác dùng ở cùng locale.</summary>
    public static readonly Error SlugTaken = Error.Conflict("Product.SlugTaken", "Slug đã được dùng ở cùng ngôn ngữ.");

    /// <summary>Giá trị thuộc tính sai: thuộc tính không thuộc nhóm hoặc khai báo lặp, thuộc tính tạo biến thể, sai kiểu, ngoài giá trị cho phép.</summary>
    public static readonly Error AttributeValueInvalid = Error.Validation("Product.AttributeValueInvalid",
        "Giá trị thuộc tính không hợp lệ: thuộc tính phải thuộc nhóm của sản phẩm, không lặp, không phải thuộc tính tạo biến thể; " +
        $"text 1..{Product.TextValueMaxLength} ký tự, number là số, boolean 'true'/'false', select đúng một mã cho phép, multi_select ít nhất một mã cho phép.");
}
