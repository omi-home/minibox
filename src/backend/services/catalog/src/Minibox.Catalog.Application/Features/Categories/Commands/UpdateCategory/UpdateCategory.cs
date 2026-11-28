using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Categories;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Categories.Commands.UpdateCategory;

/// <summary>Lệnh sửa thứ tự và bộ bản dịch của danh mục; cần quyền <see cref="CatalogPermissions.CategoryManage"/>.</summary>
/// <param name="Id">Danh mục.</param>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Translations">Bộ bản dịch mới (thay hết bộ cũ), bắt buộc có <c>vi</c>; slug đổi thì slug cũ vào lịch sử.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateCategory(Guid Id, int SortOrder, CategoryTranslationDto[] Translations, long Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CategoryManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction.</summary>
public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategory>
{
    /// <summary>Khai báo luật: có ít nhất một bản dịch.</summary>
    public UpdateCategoryValidator() => RuleFor(x => x.Translations).NotEmpty();
}

/// <summary>Sửa <see cref="Category"/> qua aggregate, kiểm slug không trùng danh mục khác.</summary>
/// <param name="categories">Repository danh mục.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class UpdateCategoryHandler(ICategoryRepository categories, TimeProvider time) : ICommandHandler<UpdateCategory>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="CategoryErrors.NotFound"/>, lỗi của <see cref="Category.Update"/>, <see cref="CategoryErrors.SlugTaken"/>
    /// hoặc xung đột phiên bản.</returns>
    public async ValueTask<Result> Handle(UpdateCategory command, CancellationToken cancellationToken)
    {
        var category = await categories.GetAsync(command.Id, cancellationToken);
        if (category is null)
            return CategoryErrors.NotFound;
        var updated = category.Update(command.SortOrder, CategoryTranslationDto.ToDomain(command.Translations), time.GetUtcNow());
        if (updated.IsFailure)
            return updated;
        if (await categories.AnySlugTakenAsync(category.Id, CategoryTranslationDto.Current(category), cancellationToken))
            return CategoryErrors.SlugTaken;
        return await categories.UpdateAsync(category, command.Version, cancellationToken);
    }
}
