using Minibox.BuildingBlocks.Application;
using Minibox.Catalog.Application.Features.Taxa;
using Minibox.Catalog.Domain.Aggregates.Taxa;

namespace Minibox.Catalog.Application.Abstractions;

/// <summary>Repository của <see cref="Taxon"/> (bộ sưu tập/thẻ) và liên kết sản phẩm–taxon (<c>product_taxon</c>).</summary>
public interface ITaxonRepository : IRepository<Taxon, Guid>
{
    /// <summary>Kiểm có slug nào trong <paramref name="texts"/> đã được taxon khác dùng ở cùng locale.</summary>
    /// <param name="taxonId">Taxon đang ghi (bỏ qua slug của chính nó).</param>
    /// <param name="texts">Bản dịch sắp ghi.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns><c>true</c> nếu trùng.</returns>
    Task<bool> AnySlugTakenAsync(Guid taxonId, IReadOnlyCollection<TaxonText> texts, CancellationToken cancellationToken = default);

    /// <summary>Đếm số Id trong <paramref name="ids"/> là taxon đang hoạt động thuộc <paramref name="kind"/>.</summary>
    /// <param name="kind">Loại (<see cref="TaxonKinds"/>).</param>
    /// <param name="ids">Id cần kiểm (không trùng).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Số Id khớp.</returns>
    Task<int> CountActiveAsync(string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Thay toàn bộ taxon loại <paramref name="kind"/> của sản phẩm bằng <paramref name="ids"/>; taxon loại khác và danh mục không đổi.</summary>
    /// <param name="productId">Sản phẩm.</param>
    /// <param name="kind">Loại được thay.</param>
    /// <param name="ids">Taxon mới (đã kiểm đang hoạt động, đúng loại).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Tác vụ hoàn tất khi đã ghi trong transaction của command.</returns>
    /// <remarks>Khoá theo sản phẩm tới hết transaction để hai lệnh song song không chồng nhau.</remarks>
    Task ReplaceProductTaxaAsync(Guid productId, string kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>Taxon kèm mọi bản dịch cho trang quản trị.</summary>
/// <param name="Id">Id.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Archived">Đã xóa mềm.</param>
/// <param name="Version">Phiên bản để gửi lại khi sửa/xóa.</param>
/// <param name="Translations">Bản dịch.</param>
public sealed record TaxonAdminRow(Guid Id, int SortOrder, bool Archived, long Version, TaxonTranslationDto[] Translations);

/// <summary>Dòng bản dịch của taxon đang hoạt động cho danh sách công khai.</summary>
/// <param name="Id">Id taxon.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Locale">Locale của bản dịch.</param>
/// <param name="Name">Tên.</param>
/// <param name="Description">Mô tả.</param>
/// <param name="Slug">Slug.</param>
public sealed record TaxonTextRow(Guid Id, int SortOrder, string Locale, string Name, string? Description, string Slug);

/// <summary>Port đọc taxon (phía query): Infrastructure chiếu thẳng ra DTO, không tải aggregate.</summary>
public interface ITaxonReadService
{
    /// <summary>Đọc mọi taxon thuộc <paramref name="kind"/> kèm mọi bản dịch.</summary>
    /// <param name="kind">Loại.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Danh sách kể cả đã xóa.</returns>
    Task<TaxonAdminRow[]> ListAllAsync(string kind, CancellationToken cancellationToken = default);

    /// <summary>Đọc bản dịch thuộc <paramref name="locales"/> của taxon đang hoạt động thuộc <paramref name="kind"/>.</summary>
    /// <param name="kind">Loại.</param>
    /// <param name="locales">Chuỗi fallback (<c>ILocaleReplica.ChainAsync</c>).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Mọi dòng khớp (nhiều locale mỗi taxon); chọn bản tốt nhất bằng <c>LocaleChain.PickPerKey</c>.</returns>
    Task<TaxonTextRow[]> ListActiveAsync(string kind, IReadOnlyList<string> locales, CancellationToken cancellationToken = default);
}
