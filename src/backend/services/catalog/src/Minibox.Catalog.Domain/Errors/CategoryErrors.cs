using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Aggregates.Categories;

namespace Minibox.Catalog.Domain.Errors;

/// <summary>Lỗi nghiệp vụ của <see cref="Category"/>; mã ổn định cho client và ProblemDetails.</summary>
public static class CategoryErrors
{
    /// <summary>Không có danh mục (hoặc cha) theo Id, hoặc cha đã bị xóa.</summary>
    public static readonly Error NotFound = Error.NotFound("Category.NotFound", "Không tìm thấy danh mục.");

    /// <summary>Bản dịch sai: locale không đúng BCP 47, tên rỗng/quá dài, mô tả quá dài, slug sai dạng, hoặc locale lặp.</summary>
    public static readonly Error TranslationInvalid = Error.Validation("Category.TranslationInvalid",
        $"Bản dịch không hợp lệ: locale BCP 47 không lặp, tên 1..{Category.NameMaxLength} ký tự, mô tả tối đa {Category.DescriptionMaxLength}, " +
        $"slug chữ thường/số nối bằng '-' tối đa {Category.SlugMaxLength} ký tự.");

    /// <summary>Thiếu bản dịch <c>vi</c> (fallback cuối).</summary>
    public static readonly Error DefaultTranslationMissing = Error.Validation("Category.DefaultTranslationMissing",
        $"Danh mục phải có bản dịch '{LocaleChain.Default}'.");

    /// <summary>Slug đã được danh mục khác dùng ở cùng locale.</summary>
    public static readonly Error SlugTaken = Error.Conflict("Category.SlugTaken", "Slug đã được dùng ở cùng ngôn ngữ.");

    /// <summary>Cha mới là chính danh mục hoặc con/cháu của nó.</summary>
    public static readonly Error Cycle = Error.Validation("Category.Cycle", "Không thể đặt danh mục vào chính nó hoặc danh mục con của nó.");

    /// <summary>Còn danh mục con chưa xóa.</summary>
    public static readonly Error HasActiveChildren = Error.Failure("Category.HasActiveChildren",
        "Danh mục còn danh mục con; chuyển hoặc xóa các danh mục con trước.");

    /// <summary>Danh mục đã xóa mềm, không sửa/di chuyển được.</summary>
    public static readonly Error Archived = Error.Failure("Category.Archived", "Danh mục đã bị xóa.");
}
