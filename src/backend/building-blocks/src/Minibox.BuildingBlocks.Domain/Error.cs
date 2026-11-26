namespace Minibox.BuildingBlocks.Domain;

/// <summary>Loại lỗi nghiệp vụ; Api ánh xạ sang mã HTTP của ProblemDetails (RFC 9457).</summary>
public enum ErrorType
{
    /// <summary>Dữ liệu vào không hợp lệ (400).</summary>
    Validation,
    /// <summary>Không tìm thấy đối tượng (404).</summary>
    NotFound,
    /// <summary>Xung đột trạng thái hoặc phiên bản (409).</summary>
    Conflict,
    /// <summary>Không có quyền trên đối tượng (403).</summary>
    Forbidden,
    /// <summary>Quy tắc nghiệp vụ không cho phép thao tác (422).</summary>
    Failure,
}

/// <summary>Lỗi nghiệp vụ trả về trong <see cref="Result"/> thay vì ném exception.</summary>
/// <param name="Code">Mã ổn định dạng <c>Miền.TênLỗi</c>, dùng cho client và log.</param>
/// <param name="Message">Mô tả cho người đọc.</param>
/// <param name="Type">Loại lỗi, quyết định mã HTTP.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Tạo lỗi dữ liệu vào không hợp lệ.</summary>
    /// <param name="code">Mã lỗi.</param>
    /// <param name="message">Mô tả.</param>
    /// <returns>Lỗi loại <see cref="ErrorType.Validation"/>.</returns>
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    /// <summary>Tạo lỗi không tìm thấy.</summary>
    /// <param name="code">Mã lỗi.</param>
    /// <param name="message">Mô tả.</param>
    /// <returns>Lỗi loại <see cref="ErrorType.NotFound"/>.</returns>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Tạo lỗi xung đột.</summary>
    /// <param name="code">Mã lỗi.</param>
    /// <param name="message">Mô tả.</param>
    /// <returns>Lỗi loại <see cref="ErrorType.Conflict"/>.</returns>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Tạo lỗi không có quyền.</summary>
    /// <param name="code">Mã lỗi.</param>
    /// <param name="message">Mô tả.</param>
    /// <returns>Lỗi loại <see cref="ErrorType.Forbidden"/>.</returns>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Tạo lỗi vi phạm quy tắc nghiệp vụ.</summary>
    /// <param name="code">Mã lỗi.</param>
    /// <param name="message">Mô tả.</param>
    /// <returns>Lỗi loại <see cref="ErrorType.Failure"/>.</returns>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
