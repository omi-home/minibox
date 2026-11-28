using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Catalog.Application.Abstractions;
using Minibox.Catalog.Domain.Errors;

namespace Minibox.Catalog.Application.Features.Taxa.Commands.ArchiveTaxon;

/// <summary>Lệnh xóa mềm bộ sưu tập/thẻ; cần quyền <see cref="CatalogPermissions.CollectionManage"/>.</summary>
/// <param name="Kind">Loại của đường dẫn; Id thuộc loại khác coi như không có.</param>
/// <param name="Id">Taxon.</param>
/// <param name="Version">Phiên bản lúc tải.</param>
public sealed record ArchiveTaxon(string Kind, Guid Id, long Version) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => CatalogPermissions.CollectionManage;
}

/// <summary>Xóa mềm qua aggregate.</summary>
/// <param name="taxa">Repository taxon.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class ArchiveTaxonHandler(ITaxonRepository taxa, TimeProvider time) : ICommandHandler<ArchiveTaxon>
{
    /// <inheritdoc/>
    /// <returns>Thành công (kể cả đã xóa trước đó); lỗi <see cref="TaxonErrors.NotFound"/> hoặc xung đột phiên bản.</returns>
    public async ValueTask<Result> Handle(ArchiveTaxon command, CancellationToken cancellationToken)
    {
        var taxon = await taxa.GetAsync(command.Id, cancellationToken);
        if (taxon is null || taxon.Kind != command.Kind)
            return TaxonErrors.NotFound;
        if (taxon.IsArchived)
            return Result.Success();
        taxon.Archive(time.GetUtcNow());
        return await taxa.UpdateAsync(taxon, command.Version, cancellationToken);
    }
}
