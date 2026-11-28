using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Domain.Aggregates.Taxa;

namespace Minibox.Catalog.Domain.Errors;

/// <summary>Lỗi nghiệp vụ của <see cref="Taxon"/> (bộ sưu tập/thẻ); mã ổn định cho client và ProblemDetails.</summary>
public static class TaxonErrors
{
    /// <summary>Không có bộ sưu tập/thẻ theo Id ở loại yêu cầu, hoặc (khi gắn sản phẩm) có Id đã bị xóa.</summary>
    public static readonly Error NotFound = Error.NotFound("Taxon.NotFound", "Không tìm thấy bộ sưu tập/thẻ.");

    /// <summary>Loại không phải <c>collection</c>/<c>tag</c>.</summary>
    public static readonly Error KindInvalid = Error.Validation("Taxon.KindInvalid", "Loại phải là 'collection' hoặc 'tag'.");

    /// <summary>Bản dịch sai: locale không đúng BCP 47, tên rỗng/quá dài, mô tả quá dài, slug sai dạng, hoặc locale lặp.</summary>
    public static readonly Error TranslationInvalid = Error.Validation("Taxon.TranslationInvalid",
        $"Bản dịch không hợp lệ: locale BCP 47 không lặp, tên 1..{Taxon.NameMaxLength} ký tự, mô tả tối đa {Taxon.DescriptionMaxLength}, " +
        $"slug chữ thường/số nối bằng '-' tối đa {Taxon.SlugMaxLength} ký tự.");

    /// <summary>Thiếu bản dịch <c>vi</c> (fallback cuối).</summary>
    public static readonly Error DefaultTranslationMissing = Error.Validation("Taxon.DefaultTranslationMissing",
        $"Bộ sưu tập/thẻ phải có bản dịch '{LocaleChain.Default}'.");

    /// <summary>Slug đã được bộ sưu tập/thẻ khác dùng ở cùng locale.</summary>
    public static readonly Error SlugTaken = Error.Conflict("Taxon.SlugTaken", "Slug đã được dùng ở cùng ngôn ngữ.");

    /// <summary>Đã xóa mềm, không sửa được.</summary>
    public static readonly Error Archived = Error.Failure("Taxon.Archived", "Bộ sưu tập/thẻ đã bị xóa.");
}
