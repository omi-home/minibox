using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.ProductTypes.Commands.UpdateProductType;

/// <summary>Lệnh sửa thứ tự, tên và bộ thuộc tính của nhóm sản phẩm; cần quyền <see cref="CatalogPermissions.AttributeManage"/>.</summary>
/// <param name="Id">Nhóm.</param>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Names">Tên mới (thay hết bộ cũ), bắt buộc có <c>vi</c>.</param>
/// <param name="Attributes">Bộ thuộc tính mới (thay hết bộ cũ, khớp theo mã nên giữ Id).</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateProductType(Guid Id, int SortOrder, LabelDto[] Names, AttributeDto[] Attributes, long Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.AttributeManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction.</summary>
public sealed class UpdateProductTypeValidator : AbstractValidator<UpdateProductType>
{
    /// <summary>Khai báo luật: giới hạn số tên/thuộc tính/giá trị/nhãn.</summary>
    public UpdateProductTypeValidator()
    {
        RuleFor(x => x.Names).Names();
        RuleFor(x => x.Attributes).Attributes();
    }
}

/// <summary>Sửa <see cref="ProductType"/> qua aggregate; không làm hỏng giá trị thuộc tính sản phẩm đang dùng.</summary>
/// <param name="types">Repository nhóm sản phẩm.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class UpdateProductTypeHandler(IProductTypeRepository types, TimeProvider time) : ICommandHandler<UpdateProductType>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="ProductTypeErrors.NotFound"/>, lỗi của <see cref="ProductType.Update"/> (gồm <see cref="ProductTypeErrors.AttributeInUse"/>) hoặc xung đột phiên bản.</returns>
    /// <remarks>ponytail: đọc giá trị đang dùng không khoá; sản phẩm ghi song song có thể lọt (FK <c>attribute</c> vẫn chặn xóa thuộc tính).</remarks>
    public async ValueTask<Result> Handle(UpdateProductType command, CancellationToken cancellationToken)
    {
        var type = await types.GetAsync(command.Id, cancellationToken);
        if (type is null)
            return ProductTypeErrors.NotFound;
        var updated = type.Update(command.SortOrder, AttributeDto.ToLabels(command.Names), AttributeDto.ToDomain(command.Attributes), time.GetUtcNow(),
            await types.UsageAsync(type.Id, cancellationToken));
        if (updated.IsFailure)
            return updated;
        return await types.UpdateAsync(type, command.Version, cancellationToken);
    }
}
