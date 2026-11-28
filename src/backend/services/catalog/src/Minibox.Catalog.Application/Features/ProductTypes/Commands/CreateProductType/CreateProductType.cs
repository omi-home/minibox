using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.ProductTypes.Commands.CreateProductType;

/// <summary>Lệnh tạo nhóm sản phẩm kèm bộ thuộc tính; cần quyền <see cref="CatalogPermissions.AttributeManage"/>, gửi lặp cùng
/// <c>Idempotency-Key</c> trả lại Id lần đầu.</summary>
/// <param name="Code">Mã nhóm (duy nhất, không đổi).</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="Names">Tên theo locale, bắt buộc có <c>vi</c>.</param>
/// <param name="Attributes">Bộ thuộc tính theo thứ tự.</param>
/// <param name="IdempotencyKey">Khoá do client sinh (header <c>Idempotency-Key</c>).</param>
public sealed record CreateProductType(string Code, int SortOrder, LabelDto[] Names, AttributeDto[] Attributes, string IdempotencyKey)
    : ICommand<Guid>, IIdempotentCommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.AttributeManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction (chi tiết do aggregate kiểm).</summary>
public sealed class CreateProductTypeValidator : AbstractValidator<CreateProductType>
{
    /// <summary>Khai báo luật: có mã, giới hạn số tên/thuộc tính/giá trị/nhãn.</summary>
    public CreateProductTypeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(ProductType.CodeMaxLength);
        RuleFor(x => x.Names).Names();
        RuleFor(x => x.Attributes).Attributes();
    }
}

/// <summary>Tạo <see cref="ProductType"/> với mã chưa dùng; transaction/audit/idempotency do pipeline lo.</summary>
/// <param name="types">Repository nhóm sản phẩm.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class CreateProductTypeHandler(IProductTypeRepository types, TimeProvider time) : ICommandHandler<CreateProductType, Guid>
{
    /// <inheritdoc/>
    /// <returns>Id mới; lỗi của <see cref="ProductType.Create"/> hoặc <see cref="ProductTypeErrors.CodeTaken"/>.</returns>
    /// <remarks>ponytail: kiểm mã trước khi ghi như slug của Taxon; hai lệnh song song cùng mã vẫn có thể vỡ unique (500).</remarks>
    public async ValueTask<Result<Guid>> Handle(CreateProductType command, CancellationToken cancellationToken)
    {
        var type = ProductType.Create(command.Code, command.SortOrder, AttributeDto.ToLabels(command.Names), AttributeDto.ToDomain(command.Attributes), time.GetUtcNow());
        if (type.IsFailure)
            return type.Error;
        if (await types.CodeTakenAsync(command.Code, cancellationToken))
            return ProductTypeErrors.CodeTaken;
        await types.AddAsync(type.Value, cancellationToken);
        return type.Value.Id;
    }
}
