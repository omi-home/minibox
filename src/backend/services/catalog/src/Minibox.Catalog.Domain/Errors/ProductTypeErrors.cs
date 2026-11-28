using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;

namespace Minibox.Catalog.Domain.Errors;

/// <summary>Lỗi nghiệp vụ của <see cref="ProductType"/> (nhóm sản phẩm và bộ thuộc tính); mã ổn định cho client và ProblemDetails.</summary>
public static class ProductTypeErrors
{
    /// <summary>Không có nhóm sản phẩm theo Id.</summary>
    public static readonly Error NotFound = Error.NotFound("ProductType.NotFound", "Không tìm thấy nhóm sản phẩm.");

    /// <summary>Mã nhóm sai dạng.</summary>
    public static readonly Error CodeInvalid = Error.Validation("ProductType.CodeInvalid",
        $"Mã nhóm gồm chữ thường/số ASCII nối bằng '-', bắt đầu bằng chữ, tối đa {ProductType.CodeMaxLength} ký tự.");

    /// <summary>Mã nhóm đã được nhóm khác dùng.</summary>
    public static readonly Error CodeTaken = Error.Conflict("ProductType.CodeTaken", "Mã nhóm sản phẩm đã được dùng.");

    /// <summary>Bản dịch tên nhóm sai: locale không đúng BCP 47, tên rỗng/quá dài, hoặc locale lặp.</summary>
    public static readonly Error TranslationInvalid = Error.Validation("ProductType.TranslationInvalid",
        $"Bản dịch không hợp lệ: locale BCP 47 không lặp, tên 1..{ProductType.LabelMaxLength} ký tự.");

    /// <summary>Thiếu nhãn <c>vi</c> (fallback cuối) ở tên nhóm, thuộc tính hoặc giá trị.</summary>
    public static readonly Error DefaultTranslationMissing = Error.Validation("ProductType.DefaultTranslationMissing",
        $"Nhóm, thuộc tính và giá trị cho phép phải có nhãn '{LocaleChain.Default}'.");

    /// <summary>Thuộc tính sai: mã sai dạng/trùng, kiểu lạ, đơn vị ngoài kiểu number hoặc quá dài, giá trị cho phép không khớp kiểu, nhãn sai.</summary>
    public static readonly Error AttributeInvalid = Error.Validation("ProductType.AttributeInvalid",
        $"Thuộc tính không hợp lệ: mã không trùng trong nhóm, kiểu thuộc {string.Join("/", AttributeTypes.All)}, " +
        $"đơn vị chỉ cho number (tối đa {ProductType.UnitMaxLength} ký tự), select/multi_select cần giá trị cho phép (mã không trùng), kiểu khác không có; " +
        $"nhãn 1..{ProductType.LabelMaxLength} ký tự, locale không lặp.");

    /// <summary>Thuộc tính đánh dấu tạo biến thể nhưng không phải kiểu <c>select</c>.</summary>
    public static readonly Error VariantRequiresSelect = Error.Validation("ProductType.VariantRequiresSelect",
        "Thuộc tính tạo biến thể phải có kiểu 'select' (giá trị rời rạc).");

    /// <summary>Bỏ, đổi kiểu, chuyển sang tạo biến thể một thuộc tính đang có giá trị ở sản phẩm, hoặc bỏ giá trị cho phép đang được dùng.</summary>
    public static readonly Error AttributeInUse = Error.Conflict("ProductType.AttributeInUse",
        "Thuộc tính hoặc giá trị cho phép đang được sản phẩm dùng: không bỏ, đổi kiểu hay chuyển sang tạo biến thể được.");

    /// <summary>Đã xóa mềm, không sửa được.</summary>
    public static readonly Error Archived = Error.Failure("ProductType.Archived", "Nhóm sản phẩm đã bị xóa.");
}
