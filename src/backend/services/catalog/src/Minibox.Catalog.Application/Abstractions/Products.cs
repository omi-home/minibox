using Minibox.BuildingBlocks.Application;
using Minibox.Catalog.Application.Features.Products;
using Minibox.Catalog.Domain.Aggregates.Products;

namespace Minibox.Catalog.Application.Abstractions;

/// <summary>Repository của <see cref="Product"/>.</summary>
public interface IProductRepository : IRepository<Product, Guid>
{
    /// <summary>Kiểm có slug nào trong <paramref name="texts"/> đã được sản phẩm khác dùng ở cùng locale.</summary>
    /// <param name="productId">Sản phẩm đang ghi (bỏ qua slug của chính nó).</param>
    /// <param name="texts">Bản dịch sắp ghi.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns><c>true</c> nếu trùng.</returns>
    Task<bool> AnySlugTakenAsync(Guid productId, IReadOnlyCollection<ProductText> texts, CancellationToken cancellationToken = default);
}

/// <summary>Chi tiết sản phẩm cho trang quản trị.</summary>
/// <param name="Id">Id.</param>
/// <param name="ProductTypeId">Nhóm.</param>
/// <param name="CategoryId">Danh mục.</param>
/// <param name="Status">Trạng thái (<c>draft</c>…).</param>
/// <param name="Version">Phiên bản để gửi lại khi sửa.</param>
/// <param name="Translations">Bản dịch.</param>
/// <param name="Attributes">Giá trị thuộc tính theo thứ tự trong nhóm.</param>
/// <param name="TaxonIds">Bộ sưu tập và thẻ đang gắn.</param>
public sealed record ProductAdminDetail(Guid Id, Guid ProductTypeId, Guid CategoryId, string Status, long Version,
    ProductTranslationDto[] Translations, ProductAttributeDto[] Attributes, Guid[] TaxonIds);

/// <summary>Port đọc sản phẩm (phía query): Infrastructure chiếu thẳng ra DTO, không tải aggregate.</summary>
public interface IProductReadService
{
    /// <summary>Đọc chi tiết một sản phẩm.</summary>
    /// <param name="id">Sản phẩm.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Chi tiết; <c>null</c> nếu không có.</returns>
    Task<ProductAdminDetail?> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
