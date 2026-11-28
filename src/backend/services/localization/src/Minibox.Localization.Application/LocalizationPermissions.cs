namespace Minibox.Localization.Application;

/// <summary>Mã quyền của service localization (seed ở <c>database/schemas/identity</c>, migration <c>0012</c>).</summary>
public static class LocalizationPermissions
{
    /// <summary>Xem và sửa bản nháp chuỗi giao diện.</summary>
    public const string MessageManage = "localization.message.manage";

    /// <summary>Xuất bản chuỗi giao diện.</summary>
    public const string MessagePublish = "localization.message.publish";

    /// <summary>Thêm, sửa, bật/tắt ngôn ngữ.</summary>
    public const string LocaleManage = "localization.locale.manage";
}
