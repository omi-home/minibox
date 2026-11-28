using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Products;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Taxa.Commands.SetProductTaxa;

/// <summary>Lệnh đặt bộ sưu tập (hoặc thẻ) của một sản phẩm: gắn/gỡ bằng cách gửi trọn danh sách mới; cần quyền
/// <see cref="CatalogPermissions.CollectionManage"/>. Taxon loại kia và danh mục của sản phẩm không đổi (BR-CAT-01).</summary>
/// <param name="Kind">Loại được thay: <c>collection</c> hoặc <c>tag</c>.</param>
/// <param name="ProductId">Sản phẩm.</param>
/// <param name="TaxonIds">Taxon đang hoạt động thuộc <paramref name="Kind"/>; rỗng = gỡ hết.</param>
public sealed record SetProductTaxa(string Kind, Guid ProductId, Guid[] TaxonIds) : ICommand, IRequirePermission
{
    /// <summary>Số taxon tối đa mỗi loại cho một sản phẩm.</summary>
    public const int MaxCount = 100;

    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CollectionManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction.</summary>
public sealed class SetProductTaxaValidator : AbstractValidator<SetProductTaxa>
{
    /// <summary>Khai báo luật: loại hợp lệ, có sản phẩm, danh sách không null và tối đa <see cref="SetProductTaxa.MaxCount"/>.</summary>
    public SetProductTaxaValidator()
    {
        RuleFor(x => x.Kind).TaxonKind();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.TaxonIds).NotNull().Must(x => x.Length <= SetProductTaxa.MaxCount);
    }
}

/// <summary>Kiểm mọi taxon đang hoạt động, đúng loại rồi thay liên kết loại đó qua aggregate <see cref="Product"/> (ghi audit).</summary>
/// <param name="products">Repository sản phẩm.</param>
/// <param name="taxa">Repository taxon.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class SetProductTaxaHandler(IProductRepository products, ITaxonRepository taxa, TimeProvider time) : ICommandHandler<SetProductTaxa>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="ProductErrors.NotFound"/>, <see cref="TaxonErrors.NotFound"/> nếu có Id không phải taxon đang hoạt động
    /// thuộc loại yêu cầu, hoặc xung đột phiên bản khi sản phẩm vừa bị sửa song song.</returns>
    public async ValueTask<Result> Handle(SetProductTaxa command, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(command.ProductId, cancellationToken);
        if (product is null)
            return ProductErrors.NotFound;
        var ids = command.TaxonIds.Distinct().ToArray();
        if (ids.Length > 0 && await taxa.CountActiveAsync(command.Kind, ids, cancellationToken) != ids.Length)
            return TaxonErrors.NotFound;
        var sameKind = await taxa.OfKindAsync(command.Kind, product.TaxonIds, cancellationToken);
        product.SetTaxa(product.TaxonIds.Except(sameKind).Concat(ids).ToArray(), time.GetUtcNow());
        return await products.UpdateAsync(product, product.Version, cancellationToken);
    }
}
