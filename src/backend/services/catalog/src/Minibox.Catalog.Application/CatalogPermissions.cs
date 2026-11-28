namespace Minibox.Catalog.Application;

/// <summary>Mã quyền của service catalog (seed ở <c>database/schemas/identity</c>: <c>catalog.product.manage</c> từ migration
/// <c>0007</c>, còn lại <c>0014</c>). Command ghi khai báo qua <c>IRequirePermission</c>; thiếu quyền pipeline trả 403.</summary>
public static class CatalogPermissions
{
    /// <summary>Tạo/sửa nháp sản phẩm, biến thể, thông tin sử dụng/an toàn.</summary>
    public const string ProductManage = "catalog.product.manage";

    /// <summary>Gửi sản phẩm đi duyệt.</summary>
    public const string ProductSubmit = "catalog.product.submit";

    /// <summary>Duyệt/từ chối/tạm dừng sản phẩm (tách khỏi người gửi).</summary>
    public const string ProductApprove = "catalog.product.approve";

    /// <summary>Tạo/sửa/di chuyển/xóa mềm danh mục.</summary>
    public const string CategoryManage = "catalog.category.manage";

    /// <summary>Quản lý bộ sưu tập và thẻ.</summary>
    public const string CollectionManage = "catalog.collection.manage";

    /// <summary>Quản lý nhóm sản phẩm và bộ thuộc tính.</summary>
    public const string AttributeManage = "catalog.attribute.manage";

    /// <summary>Đặt giá bán.</summary>
    public const string PriceWrite = "catalog.price.write";

    /// <summary>Quản lý file số, hồ sơ nguồn quyền và giấy phép.</summary>
    public const string FileManage = "catalog.file.manage";
}
