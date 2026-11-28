using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Products;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Products.Commands.UpdateProduct;

/// <summary>Lệnh sửa sản phẩm nháp (nhóm, danh mục, bản dịch, giá trị thuộc tính); cần quyền <see cref="CatalogPermissions.ProductManage"/>.</summary>
/// <param name="Id">Sản phẩm.</param>
/// <param name="ProductTypeId">Nhóm đang hoạt động (đổi nhóm thì giá trị kiểm theo nhóm mới).</param>
/// <param name="CategoryId">Danh mục đang hoạt động.</param>
/// <param name="Translations">Bộ bản dịch mới (thay hết bộ cũ); slug đổi thì slug cũ vào lịch sử.</param>
/// <param name="Attributes">Bộ giá trị thuộc tính mới (thay hết bộ cũ).</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateProduct(Guid Id, Guid ProductTypeId, Guid CategoryId, ProductTranslationDto[] Translations, ProductAttributeDto[] Attributes, long Version)
    : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.ProductManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction.</summary>
public sealed class UpdateProductValidator : AbstractValidator<UpdateProduct>
{
    /// <summary>Khai báo luật: có nhóm và danh mục, giới hạn số bản dịch/thuộc tính/giá trị.</summary>
    public UpdateProductValidator()
    {
        RuleFor(x => x.ProductTypeId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Translations).Translations();
        RuleFor(x => x.Attributes).Attributes();
    }
}

/// <summary>Sửa <see cref="Product"/> qua aggregate, kiểm slug không trùng sản phẩm khác.</summary>
/// <param name="products">Repository sản phẩm.</param>
/// <param name="types">Repository nhóm sản phẩm.</param>
/// <param name="categories">Repository danh mục.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class UpdateProductHandler(IProductRepository products, IProductTypeRepository types, ICategoryRepository categories, TimeProvider time)
    : ICommandHandler<UpdateProduct>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="ProductErrors.NotFound"/>, <see cref="ProductTypeErrors.NotFound"/>, <see cref="CategoryErrors.NotFound"/>,
    /// lỗi của <see cref="Product.Update"/>, <see cref="ProductErrors.SlugTaken"/> hoặc xung đột phiên bản.</returns>
    /// <remarks>Nhóm/danh mục đã xóa vẫn giữ được nếu không đổi (sản phẩm cũ không bị khoá sửa).</remarks>
    public async ValueTask<Result> Handle(UpdateProduct command, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(command.Id, cancellationToken);
        if (product is null)
            return ProductErrors.NotFound;
        var type = await types.GetAsync(command.ProductTypeId, cancellationToken);
        if (type is null || type.IsArchived && type.Id != product.ProductTypeId)
            return ProductTypeErrors.NotFound;
        var category = await categories.GetAsync(command.CategoryId, cancellationToken);
        if (category is null || category.IsArchived && category.Id != product.CategoryId)
            return CategoryErrors.NotFound;
        var updated = product.Update(type, command.CategoryId, ProductTranslationDto.ToDomain(command.Translations),
            ProductAttributeDto.ToDomain(command.Attributes), time.GetUtcNow());
        if (updated.IsFailure)
            return updated;
        if (await products.AnySlugTakenAsync(product.Id, ProductTranslationDto.Current(product), cancellationToken))
            return ProductErrors.SlugTaken;
        return await products.UpdateAsync(product, command.Version, cancellationToken);
    }
}
