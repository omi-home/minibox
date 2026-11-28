using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.ProductTypes.Commands.ArchiveProductType;

/// <summary>Lệnh xóa mềm nhóm sản phẩm; cần quyền <see cref="CatalogPermissions.AttributeManage"/>.</summary>
/// <param name="Id">Nhóm.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record ArchiveProductType(Guid Id, long Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.AttributeManage;
}

/// <summary>Xóa mềm qua aggregate.</summary>
/// <param name="types">Repository nhóm sản phẩm.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class ArchiveProductTypeHandler(IProductTypeRepository types, TimeProvider time) : ICommandHandler<ArchiveProductType>
{
    /// <inheritdoc/>
    /// <returns>Thành công (kể cả đã xóa trước đó); lỗi <see cref="ProductTypeErrors.NotFound"/> hoặc xung đột phiên bản.</returns>
    public async ValueTask<Result> Handle(ArchiveProductType command, CancellationToken cancellationToken)
    {
        var type = await types.GetAsync(command.Id, cancellationToken);
        if (type is null)
            return ProductTypeErrors.NotFound;
        if (type.IsArchived)
            return Result.Success();
        type.Archive(time.GetUtcNow());
        return await types.UpdateAsync(type, command.Version, cancellationToken);
    }
}
