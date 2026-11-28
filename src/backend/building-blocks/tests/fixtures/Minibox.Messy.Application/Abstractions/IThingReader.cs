namespace Minibox.Messy.Application.Abstractions;

/// <summary>Port đọc (đúng chỗ): Infrastructure cài đặt.</summary>
public interface IThingReader
{
    /// <summary>Đếm số thing.</summary>
    /// <returns>Số lượng.</returns>
    int Count();
}
