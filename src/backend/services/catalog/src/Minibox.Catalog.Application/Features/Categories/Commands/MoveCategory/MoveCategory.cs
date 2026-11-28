using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Categories.Commands.MoveCategory;

/// <summary>Lệnh chuyển danh mục sang cha khác; cần quyền <see cref="CatalogPermissions.CategoryManage"/>.</summary>
/// <param name="Id">Danh mục.</param>
/// <param name="ParentId">Cha mới; <c>null</c> = lên gốc.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record MoveCategory(Guid Id, Guid? ParentId, long Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CategoryManage;
}

/// <summary>Khoá cây, kiểm cha mới đang hoạt động rồi để aggregate chặn vòng lặp cha–con.</summary>
/// <param name="categories">Repository danh mục.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class MoveCategoryHandler(ICategoryRepository categories, TimeProvider time) : ICommandHandler<MoveCategory>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="CategoryErrors.NotFound"/> (danh mục hoặc cha), <see cref="CategoryErrors.Cycle"/>,
    /// <see cref="CategoryErrors.Archived"/> hoặc xung đột phiên bản.</returns>
    public async ValueTask<Result> Handle(MoveCategory command, CancellationToken cancellationToken)
    {
        var tree = await categories.LockTreeAsync(cancellationToken);
        var category = await categories.GetAsync(command.Id, cancellationToken);
        if (category is null || command.ParentId is { } parent && !tree.Any(n => n.Id == parent && !n.Archived))
            return CategoryErrors.NotFound;
        var moved = category.MoveTo(command.ParentId, tree.ToDictionary(n => n.Id, n => n.ParentId), time.GetUtcNow());
        if (moved.IsFailure)
            return moved;
        return await categories.UpdateAsync(category, command.Version, cancellationToken);
    }
}
