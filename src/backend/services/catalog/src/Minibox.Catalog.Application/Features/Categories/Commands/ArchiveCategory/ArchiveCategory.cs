using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Categories.Commands.ArchiveCategory;

/// <summary>Lệnh xóa mềm danh mục; cần quyền <see cref="CatalogPermissions.CategoryManage"/>.</summary>
/// <param name="Id">Danh mục.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record ArchiveCategory(Guid Id, long Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CategoryManage;
}

/// <summary>Khoá cây rồi xóa mềm nếu không còn con đang hoạt động.</summary>
/// <param name="categories">Repository danh mục.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class ArchiveCategoryHandler(ICategoryRepository categories, TimeProvider time) : ICommandHandler<ArchiveCategory>
{
    /// <inheritdoc/>
    /// <returns>Thành công (kể cả đã xóa trước đó); lỗi <see cref="CategoryErrors.NotFound"/>, <see cref="CategoryErrors.HasActiveChildren"/>
    /// hoặc xung đột phiên bản.</returns>
    public async ValueTask<Result> Handle(ArchiveCategory command, CancellationToken cancellationToken)
    {
        var tree = await categories.LockTreeAsync(cancellationToken);
        var category = await categories.GetAsync(command.Id, cancellationToken);
        if (category is null)
            return CategoryErrors.NotFound;
        if (category.IsArchived)
            return Result.Success();
        var archived = category.Archive(tree.Any(n => n.ParentId == category.Id && !n.Archived), time.GetUtcNow());
        if (archived.IsFailure)
            return archived;
        return await categories.UpdateAsync(category, command.Version, cancellationToken);
    }
}
