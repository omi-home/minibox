using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;

namespace Minibox.Catalog.Application.Features.Taxa.Queries.ListTaxa;

/// <summary>Truy vấn danh sách quản trị bộ sưu tập/thẻ (kể cả đã xóa, mọi bản dịch); cần quyền <see cref="CatalogPermissions.CollectionManage"/>.</summary>
/// <param name="Kind">Loại: <c>collection</c> hoặc <c>tag</c>.</param>
public sealed record ListTaxa(string Kind) : IQuery<TaxonAdminRow[]>, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CollectionManage;
}

/// <summary>Kiểm loại hợp lệ.</summary>
public sealed class ListTaxaValidator : AbstractValidator<ListTaxa>
{
    /// <summary>Khai báo luật: loại là <c>collection</c>/<c>tag</c>.</summary>
    public ListTaxaValidator() => RuleFor(x => x.Kind).TaxonKind();
}

/// <summary>Đọc qua <see cref="ITaxonReadService"/>, sắp theo thứ tự rồi tên <c>vi</c>.</summary>
/// <param name="reader">Port đọc.</param>
public sealed class ListTaxaHandler(ITaxonReadService reader) : IQueryHandler<ListTaxa, TaxonAdminRow[]>
{
    /// <inheritdoc/>
    public async ValueTask<Result<TaxonAdminRow[]>> Handle(ListTaxa query, CancellationToken cancellationToken) =>
        (await reader.ListAllAsync(query.Kind, cancellationToken))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Translations.FirstOrDefault(t => t.Locale == LocaleChain.Default)?.Name).ToArray();
}
