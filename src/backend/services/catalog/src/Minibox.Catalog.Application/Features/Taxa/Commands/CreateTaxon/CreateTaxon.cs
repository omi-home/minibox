using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Taxa;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Taxa.Commands.CreateTaxon;

/// <summary>Lệnh tạo bộ sưu tập/thẻ; cần quyền <see cref="CatalogPermissions.CollectionManage"/>, gửi lặp cùng <c>Idempotency-Key</c> trả lại Id lần đầu.</summary>
/// <param name="Kind">Loại: <c>collection</c> hoặc <c>tag</c>.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="Translations">Bản dịch, bắt buộc có <c>vi</c>.</param>
/// <param name="IdempotencyKey">Khoá do client sinh (header <c>Idempotency-Key</c>).</param>
public sealed record CreateTaxon(string Kind, int SortOrder, TaxonTranslationDto[] Translations, string IdempotencyKey)
    : ICommand<Guid>, IIdempotentCommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CollectionManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction (chi tiết bản dịch do aggregate kiểm).</summary>
public sealed class CreateTaxonValidator : AbstractValidator<CreateTaxon>
{
    /// <summary>Khai báo luật: loại hợp lệ, 1..50 bản dịch.</summary>
    public CreateTaxonValidator()
    {
        RuleFor(x => x.Kind).TaxonKind();
        RuleFor(x => x.Translations).Translations();
    }
}

/// <summary>Tạo <see cref="Taxon"/> với slug không trùng; transaction/audit/idempotency do pipeline lo.</summary>
/// <param name="taxa">Repository taxon.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class CreateTaxonHandler(ITaxonRepository taxa, TimeProvider time) : ICommandHandler<CreateTaxon, Guid>
{
    /// <inheritdoc/>
    /// <returns>Id mới; lỗi của <see cref="Taxon.Create"/> hoặc <see cref="TaxonErrors.SlugTaken"/>.</returns>
    public async ValueTask<Result<Guid>> Handle(CreateTaxon command, CancellationToken cancellationToken)
    {
        var taxon = Taxon.Create(command.Kind, command.SortOrder, TaxonTranslationDto.ToDomain(command.Translations), time.GetUtcNow());
        if (taxon.IsFailure)
            return taxon.Error;
        if (await taxa.AnySlugTakenAsync(taxon.Value.Id, TaxonTranslationDto.Current(taxon.Value), cancellationToken))
            return TaxonErrors.SlugTaken;
        await taxa.AddAsync(taxon.Value, cancellationToken);
        return taxon.Value.Id;
    }
}
