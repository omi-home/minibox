using FluentValidation;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Aggregates.Taxa;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Taxa.Commands.UpdateTaxon;

/// <summary>Lệnh sửa thứ tự và bộ bản dịch của bộ sưu tập/thẻ; cần quyền <see cref="CatalogPermissions.CollectionManage"/>.</summary>
/// <param name="Kind">Loại của đường dẫn; Id thuộc loại khác coi như không có.</param>
/// <param name="Id">Taxon.</param>
/// <param name="SortOrder">Thứ tự mới.</param>
/// <param name="Translations">Bộ bản dịch mới (thay hết bộ cũ), bắt buộc có <c>vi</c>; slug đổi thì slug cũ vào lịch sử.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record UpdateTaxon(string Kind, Guid Id, int SortOrder, TaxonTranslationDto[] Translations, long Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CollectionManage;
}

/// <summary>Kiểm dữ liệu vào trước khi mở transaction.</summary>
public sealed class UpdateTaxonValidator : AbstractValidator<UpdateTaxon>
{
    /// <summary>Khai báo luật: loại hợp lệ, 1..50 bản dịch.</summary>
    public UpdateTaxonValidator()
    {
        RuleFor(x => x.Kind).TaxonKind();
        RuleFor(x => x.Translations).Translations();
    }
}

/// <summary>Sửa <see cref="Taxon"/> qua aggregate, kiểm slug không trùng taxon khác.</summary>
/// <param name="taxa">Repository taxon.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class UpdateTaxonHandler(ITaxonRepository taxa, TimeProvider time) : ICommandHandler<UpdateTaxon>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="TaxonErrors.NotFound"/>, lỗi của <see cref="Taxon.Update"/>, <see cref="TaxonErrors.SlugTaken"/>
    /// hoặc xung đột phiên bản.</returns>
    public async ValueTask<Result> Handle(UpdateTaxon command, CancellationToken cancellationToken)
    {
        var taxon = await taxa.GetAsync(command.Id, cancellationToken);
        if (taxon is null || taxon.Kind != command.Kind)
            return TaxonErrors.NotFound;
        var updated = taxon.Update(command.SortOrder, TaxonTranslationDto.ToDomain(command.Translations), time.GetUtcNow());
        if (updated.IsFailure)
            return updated;
        if (await taxa.AnySlugTakenAsync(taxon.Id, TaxonTranslationDto.Current(taxon), cancellationToken))
            return TaxonErrors.SlugTaken;
        return await taxa.UpdateAsync(taxon, command.Version, cancellationToken);
    }
}
