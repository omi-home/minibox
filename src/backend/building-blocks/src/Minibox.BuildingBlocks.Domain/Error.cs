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
    /// <summary>Thông tin xác thực thiếu, sai hoặc hết hạn (401).</summary>
    Unauthorized,
}

/// <summary>Lỗi nghiệp vụ trả về trong <see cref="Result"/> thay vì ném exception.</summary>
/// <param name="Code">Mã ổn định dạng <c>Miền.TênLỗi</c>, dùng cho client và log.</param>
/// <param name="Message">Mô tả cho người đọc.</param>
/// <param name="Type">Loại lỗi, quyết định mã HTTP.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Lệnh vẫn commit thay đổi đã ghi (cùng audit/outbox) dù trả lỗi này — cho thay đổi phải còn lại khi từ chối:
    /// bộ đếm đăng nhập sai, thu hồi phiên khi refresh token bị dùng lại. Mặc định <c>false</c> (lỗi = rollback).</summary>
    public bool CommitChanges { get; init; }

    /// <summary>Bản sao của lỗi có <see cref="CommitChanges"/> = <c>true</c>.</summary>
    /// <returns>Lỗi cùng mã, mô tả, loại; transaction của lệnh được commit.</returns>
    public Error WithCommit() => this with { CommitChanges = true };

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

    /// <summary>Tạo lỗi xác thực (phiên/token không hợp lệ).</summary>
    /// <param name="code">Mã lỗi.</param>
    /// <param name="message">Mô tả.</param>
    /// <returns>Lỗi loại <see cref="ErrorType.Unauthorized"/>.</returns>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
