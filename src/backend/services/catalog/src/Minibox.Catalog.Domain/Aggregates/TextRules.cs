using System.Text.RegularExpressions;

namespace Minibox.Catalog.Domain.Aggregates;

// Dạng locale/slug dùng chung cho mọi bản dịch của catalog (khớp CHECK ở database/schemas/catalog).
internal static partial class TextRules
{
    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$")]
    internal static partial Regex Locale();

    // Chữ thường/chữ không phân hoa thường (CJK…)/số, nối bằng một dấu '-'.
    [GeneratedRegex(@"^[\p{Ll}\p{Lo}\p{Lm}\p{Mn}\p{Nd}]+(-[\p{Ll}\p{Lo}\p{Lm}\p{Mn}\p{Nd}]+)*$")]
    internal static partial Regex Slug();
}
