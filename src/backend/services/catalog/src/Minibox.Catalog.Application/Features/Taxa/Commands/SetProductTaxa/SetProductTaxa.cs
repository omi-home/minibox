using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
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

/// <summary>Kiểm mọi taxon đang hoạt động, đúng loại rồi thay liên kết của sản phẩm.</summary>
/// <param name="taxa">Repository taxon.</param>
/// <remarks>ponytail: chưa có bảng <c>product</c> nên chưa kiểm sản phẩm tồn tại và không ghi audit; khi có aggregate Product,
/// chuyển liên kết vào Product (FK <c>product_taxon.product_id</c> + sự kiện audit).</remarks>
public sealed class SetProductTaxaHandler(ITaxonRepository taxa) : ICommandHandler<SetProductTaxa>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="TaxonErrors.NotFound"/> nếu có Id không phải taxon đang hoạt động thuộc loại yêu cầu.</returns>
    public async ValueTask<Result> Handle(SetProductTaxa command, CancellationToken cancellationToken)
    {
        var ids = command.TaxonIds.Distinct().ToArray();
        if (ids.Length > 0 && await taxa.CountActiveAsync(command.Kind, ids, cancellationToken) != ids.Length)
            return TaxonErrors.NotFound;
        await taxa.ReplaceProductTaxaAsync(command.ProductId, command.Kind, ids, cancellationToken);
        return Result.Success();
    }
}
