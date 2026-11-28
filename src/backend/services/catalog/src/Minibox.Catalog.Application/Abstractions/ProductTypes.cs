using Minibox.BuildingBlocks.Application;
using Minibox.Catalog.Application.Features.ProductTypes;
using Minibox.Catalog.Domain.Aggregates.ProductTypes;

namespace Minibox.Catalog.Application.Abstractions;

/// <summary>Repository của <see cref="ProductType"/> (nhóm sản phẩm và bộ thuộc tính).</summary>
public interface IProductTypeRepository : IRepository<ProductType, Guid>
{
    /// <summary>Kiểm mã nhóm đã được dùng (kể cả nhóm đã xóa).</summary>
    /// <param name="code">Mã nhóm.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns><c>true</c> nếu đã có.</returns>
    Task<bool> CodeTakenAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Đọc các giá trị sản phẩm đang dùng thuộc tính của nhóm.</summary>
    /// <param name="productTypeId">Nhóm.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Cặp (thuộc tính, giá trị) không trùng.</returns>
    Task<AttributeUsage[]> UsageAsync(Guid productTypeId, CancellationToken cancellationToken = default);
}

/// <summary>Nhóm sản phẩm kèm mọi bản dịch và bộ thuộc tính cho trang quản trị.</summary>
/// <param name="Id">Id.</param>
/// <param name="Code">Mã nhóm.</param>
/// <param name="SortOrder">Thứ tự.</param>
/// <param name="Archived">Đã xóa mềm.</param>
/// <param name="Version">Phiên bản để gửi lại khi sửa/xóa.</param>
/// <param name="Names">Tên theo locale.</param>
/// <param name="Attributes">Bộ thuộc tính theo thứ tự (giá trị cho phép theo thứ tự).</param>
public sealed record ProductTypeAdminRow(Guid Id, string Code, int SortOrder, bool Archived, long Version, LabelDto[] Names, ProductTypeAttributeRow[] Attributes);

/// <summary>Thuộc tính của nhóm kèm Id (sản phẩm/biến thể tham chiếu Id).</summary>
/// <param name="Id">Id thuộc tính.</param>
/// <param name="Code">Mã.</param>
/// <param name="DataType">Kiểu.</param>
/// <param name="Unit">Đơn vị.</param>
/// <param name="Required">Bắt buộc.</param>
/// <param name="Variant">Tạo biến thể.</param>
/// <param name="Labels">Nhãn theo locale.</param>
/// <param name="Values">Giá trị cho phép theo thứ tự.</param>
public sealed record ProductTypeAttributeRow(Guid Id, string Code, string DataType, string? Unit, bool Required, bool Variant, LabelDto[] Labels,
    ProductTypeValueRow[] Values);

/// <summary>Giá trị cho phép kèm Id.</summary>
/// <param name="Id">Id giá trị.</param>
/// <param name="Code">Mã.</param>
/// <param name="Labels">Nhãn theo locale.</param>
public sealed record ProductTypeValueRow(Guid Id, string Code, LabelDto[] Labels);

/// <summary>Port đọc nhóm sản phẩm (phía query): Infrastructure chiếu thẳng ra DTO, không tải aggregate.</summary>
public interface IProductTypeReadService
{
    /// <summary>Đọc mọi nhóm kèm bản dịch và bộ thuộc tính.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Danh sách kể cả đã xóa.</returns>
    Task<ProductTypeAdminRow[]> ListAllAsync(CancellationToken cancellationToken = default);
}
