using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Categories;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Categories.Commands.CreateCategory;

/// <summary>Lệnh tạo danh mục; cần quyền <see cref="CatalogPermissions.CategoryManage"/>, gửi lặp cùng <c>Idempotency-Key</c> trả lại Id lần đầu.</summary>
/// <param name="ParentId">Cha; <c>null</c> = gốc.</param>
/// <param name="SortOrder">Thứ tự trong cha.</param>
/// <param name="Translations">Bản dịch, bắt buộc có <c>vi</c>.</param>
/// <param name="IdempotencyKey">Khoá do client sinh (header <c>Idempotency-Key</c>).</param>
public sealed record CreateCategory(Guid? ParentId, int SortOrder, CategoryTranslationDto[] Translations, string IdempotencyKey)
    : ICommand<Guid>, IIdempotentCommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CategoryManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction (chi tiết bản dịch do aggregate kiểm).</summary>
public sealed class CreateCategoryValidator : AbstractValidator<CreateCategory>
{
    /// <summary>Khai báo luật: có 1..50 bản dịch.</summary>
    public CreateCategoryValidator() => RuleFor(x => x.Translations).NotEmpty().Must(x => x.Length <= 50);
}

/// <summary>Tạo <see cref="Category"/> dưới cha đang hoạt động, slug không trùng; transaction/audit/idempotency do pipeline lo.</summary>
/// <param name="categories">Repository danh mục.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class CreateCategoryHandler(ICategoryRepository categories, TimeProvider time) : ICommandHandler<CreateCategory, Guid>
{
    /// <inheritdoc/>
    /// <returns>Id danh mục mới; lỗi <see cref="CategoryErrors.NotFound"/> (cha không có/đã xóa), lỗi bản dịch hoặc <see cref="CategoryErrors.SlugTaken"/>.</returns>
    public async ValueTask<Result<Guid>> Handle(CreateCategory command, CancellationToken cancellationToken)
    {
        var tree = await categories.LockTreeAsync(cancellationToken);
        if (command.ParentId is { } parent && !tree.Any(n => n.Id == parent && !n.Archived))
            return CategoryErrors.NotFound;
        var category = Category.Create(command.ParentId, command.SortOrder, CategoryTranslationDto.ToDomain(command.Translations), time.GetUtcNow());
        if (category.IsFailure)
            return category.Error;
        if (await categories.AnySlugTakenAsync(category.Value.Id, CategoryTranslationDto.Current(category.Value), cancellationToken))
            return CategoryErrors.SlugTaken;
        await categories.AddAsync(category.Value, cancellationToken);
        return category.Value.Id;
    }
}
