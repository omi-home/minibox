using System.Text.RegularExpressions;

namespace Minibox.Catalog.Domain.Aggregates;

// Dạng locale/slug/mã dùng chung cho catalog (khớp CHECK ở database/schemas/catalog). \z thay $ để không nhận '\n' cuối chuỗi.
internal static partial class TextRules
{
    [GeneratedRegex(@"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*\z")]
    internal static partial Regex Locale();

    // Chữ thường/chữ không phân hoa thường (CJK…)/số, nối bằng một dấu '-'.
    [GeneratedRegex(@"^[\p{Ll}\p{Lo}\p{Lm}\p{Mn}\p{Nd}]+(-[\p{Ll}\p{Lo}\p{Lm}\p{Mn}\p{Nd}]+)*\z")]
    internal static partial Regex Slug();

    // Mã ổn định (nhóm, thuộc tính, giá trị): ASCII chữ thường/số, bắt đầu bằng chữ, nối bằng '-'.
    [GeneratedRegex(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*\z")]
    internal static partial Regex Code();
}
