using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Products;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Products.Commands.CreateProduct;

/// <summary>Lệnh tạo sản phẩm nháp; cần quyền <see cref="CatalogPermissions.ProductManage"/>, gửi lặp cùng <c>Idempotency-Key</c> trả lại Id lần đầu.</summary>
/// <param name="ProductTypeId">Nhóm đang hoạt động.</param>
/// <param name="CategoryId">Danh mục đang hoạt động.</param>
/// <param name="Translations">Bản dịch, bắt buộc có <c>vi</c>.</param>
/// <param name="Attributes">Giá trị thuộc tính mô tả theo bộ thuộc tính của nhóm (có thể thiếu thuộc tính bắt buộc khi còn nháp).</param>
/// <param name="IdempotencyKey">Khoá do client sinh (header <c>Idempotency-Key</c>).</param>
public sealed record CreateProduct(Guid ProductTypeId, Guid CategoryId, ProductTranslationDto[] Translations, ProductAttributeDto[] Attributes, string IdempotencyKey)
    : ICommand<Guid>, IIdempotentCommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.ProductManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction.</summary>
public sealed class CreateProductValidator : AbstractValidator<CreateProduct>
{
    /// <summary>Khai báo luật: có nhóm và danh mục, giới hạn số bản dịch/thuộc tính/giá trị.</summary>
    public CreateProductValidator()
    {
        RuleFor(x => x.ProductTypeId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Translations).Translations();
        RuleFor(x => x.Attributes).Attributes();
    }
}

/// <summary>Tạo <see cref="Product"/> nháp trong nhóm và danh mục đang hoạt động, slug không trùng; transaction/audit/idempotency do pipeline lo.</summary>
/// <param name="products">Repository sản phẩm.</param>
/// <param name="types">Repository nhóm sản phẩm.</param>
/// <param name="categories">Repository danh mục.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class CreateProductHandler(IProductRepository products, IProductTypeRepository types, ICategoryRepository categories, TimeProvider time)
    : ICommandHandler<CreateProduct, Guid>
{
    /// <inheritdoc/>
    /// <returns>Id mới; lỗi <see cref="ProductTypeErrors.NotFound"/>, <see cref="CategoryErrors.NotFound"/>, lỗi của <see cref="Product.Create"/>
    /// hoặc <see cref="ProductErrors.SlugTaken"/>.</returns>
    public async ValueTask<Result<Guid>> Handle(CreateProduct command, CancellationToken cancellationToken)
    {
        var type = await types.GetAsync(command.ProductTypeId, cancellationToken);
        if (type is null || type.IsArchived)
            return ProductTypeErrors.NotFound;
        if (await categories.GetAsync(command.CategoryId, cancellationToken) is not { IsArchived: false })
            return CategoryErrors.NotFound;
        var product = Product.Create(type, command.CategoryId, ProductTranslationDto.ToDomain(command.Translations),
            ProductAttributeDto.ToDomain(command.Attributes), time.GetUtcNow());
        if (product.IsFailure)
            return product.Error;
        if (await products.AnySlugTakenAsync(product.Value.Id, ProductTranslationDto.Current(product.Value), cancellationToken))
            return ProductErrors.SlugTaken;
        await products.AddAsync(product.Value, cancellationToken);
        return product.Value.Id;
    }
}
