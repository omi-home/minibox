using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Minibox.BuildingBlocks.Web;

/// <summary>Đọc cấu hình bắt buộc của composition root.</summary>
public static class MiniboxConfiguration
{
    /// <summary>Đang chạy trong bước sinh OpenAPI lúc build: <c>GetDocument.Insider</c> dựng host tới <c>Build()</c> nhưng không chạy, không có cấu hình môi trường.</summary>
    public static bool IsGeneratingOpenApi { get; } = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    /// <summary>Lấy connection string bắt buộc (ConnectionStrings:<paramref name="name"/>).</summary>
    /// <param name="configuration">Cấu hình ứng dụng.</param>
    /// <param name="name">Tên connection string, vd <c>App</c>, <c>RabbitMq</c>.</param>
    /// <param name="buildTimePlaceholder">Giá trị giữ chỗ chỉ dùng khi <see cref="IsGeneratingOpenApi"/> (host không kết nối gì).</param>
    /// <returns>Giá trị cấu hình; hoặc <paramref name="buildTimePlaceholder"/> khi đang sinh OpenAPI lúc build.</returns>
    /// <exception cref="InvalidOperationException">Thiếu cấu hình khi chạy thật.</exception>
    public static string RequiredConnectionString(this IConfiguration configuration, string name, string buildTimePlaceholder) =>
        configuration.GetConnectionString(name)
        ?? (IsGeneratingOpenApi ? buildTimePlaceholder : throw new InvalidOperationException($"Thiếu ConnectionStrings:{name}"));
}
