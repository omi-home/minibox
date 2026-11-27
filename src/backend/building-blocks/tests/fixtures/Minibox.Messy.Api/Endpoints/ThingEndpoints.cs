using Minibox.Messy.Infrastructure;

namespace Minibox.Messy.Api.Endpoints;

/// <summary>Vi phạm cố ý: endpoint dùng thẳng Infrastructure thay vì gửi query.</summary>
public static class ThingEndpoints
{
    /// <summary>Đếm thing bằng adapter hạ tầng.</summary>
    /// <returns>Số lượng.</returns>
    public static int Count() => new ThingReader().Count();
}
