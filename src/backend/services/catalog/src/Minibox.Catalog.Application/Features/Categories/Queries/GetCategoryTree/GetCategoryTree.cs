using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;

namespace Minibox.Catalog.Application.Features.Categories.Queries.GetCategoryTree;

/// <summary>Truy vấn cây danh mục công khai theo locale (không cần đăng nhập); bản dịch đi theo chuỗi fallback, thiếu hết dùng <c>vi</c>.</summary>
/// <param name="Locale">Locale của request.</param>
public sealed record GetCategoryTree(string Locale) : IQuery<CategoryTreeNode[]>;

/// <summary>Nút cây công khai.</summary>
/// <param name="Id">Id.</param>
/// <param name="Locale">Locale của bản dịch đang hiển thị; khác locale yêu cầu là fallback (trang đặt <c>noindex</c>).</param>
/// <param name="Name">Tên.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Slug">Slug theo <paramref name="Locale"/>.</param>
/// <param name="Children">Danh mục con đang hoạt động.</param>
public sealed record CategoryTreeNode(Guid Id, string Locale, string Name, string? Description, string Slug, CategoryTreeNode[] Children);

/// <summary>Kiểm locale đúng dạng BCP 47.</summary>
public sealed class GetCategoryTreeValidator : AbstractValidator<GetCategoryTree>
{
    /// <summary>Khai báo luật: locale không rỗng, tối đa 35 ký tự, dạng BCP 47.</summary>
    public GetCategoryTreeValidator() => RuleFor(x => x.Locale).NotEmpty().MaximumLength(35).Matches("^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$");
}

/// <summary>Dựng chuỗi fallback, đọc bản dịch của danh mục đang hoạt động, chọn bản tốt nhất mỗi danh mục rồi dựng cây.</summary>
/// <param name="locales">Bản sao locale.</param>
/// <param name="reader">Port đọc.</param>
public sealed class GetCategoryTreeHandler(ILocaleReplica locales, ICategoryReadService reader) : IQueryHandler<GetCategoryTree, CategoryTreeNode[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<CategoryTreeNode[]>> Handle(GetCategoryTree query, CancellationToken cancellationToken)
    {
        var chain = await locales.ChainAsync(query.Locale, cancellationToken);
        var best = LocaleChain.PickPerKey(await reader.ListActiveAsync(chain, cancellationToken), r => r.Id, r => r.Locale, chain);
        return CategoryTree.Build(best.Values, r => r.Id, r => r.ParentId, r => (r.SortOrder, r.Name),
            (CategoryTextRow r, CategoryTreeNode[] children) => new CategoryTreeNode(r.Id, r.Locale, r.Name, r.Description, r.Slug, children));
    }
}
