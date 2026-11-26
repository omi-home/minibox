using Microsoft.AspNetCore.Http;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Web;

/// <summary>Ánh xạ <see cref="Result"/> sang phản hồi HTTP; lỗi thành ProblemDetails (RFC 9457).</summary>
public static class ResultHttpExtensions
{
    /// <summary>Thành công → 204; thất bại → ProblemDetails.</summary>
    /// <param name="result">Kết quả lệnh.</param>
    /// <returns>Phản hồi HTTP.</returns>
    public static IResult ToHttpResult(this Result result) => result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    /// <summary>Thành công → 200 kèm giá trị; thất bại → ProblemDetails.</summary>
    /// <typeparam name="T">Kiểu giá trị.</typeparam>
    /// <param name="result">Kết quả lệnh/truy vấn.</param>
    /// <returns>Phản hồi HTTP.</returns>
    public static IResult ToHttpResult<T>(this Result<T> result) => result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>Mã HTTP theo loại lỗi.</summary>
    /// <param name="type">Loại lỗi.</param>
    /// <returns>400, 404, 409, 403 hoặc 422.</returns>
    public static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status422UnprocessableEntity,
    };

    /// <summary>Dựng ProblemDetails: <c>title</c> = mã lỗi ổn định, <c>detail</c> = mô tả, thêm trường <c>code</c>.</summary>
    /// <param name="error">Lỗi nghiệp vụ.</param>
    /// <returns>Phản hồi <c>application/problem+json</c>.</returns>
    public static IResult Problem(Error error) => Results.Problem(
        statusCode: StatusCode(error.Type),
        title: error.Code,
        detail: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
