using System.Security.Cryptography;
using System.Text.Json;
using Mediator;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Behaviors;

/// <summary><see cref="IIdempotentCommand"/> gửi lặp cùng khoá (cùng người gọi, cùng loại lệnh) trả lại kết quả thành công
/// lần đầu mà không chạy lại handler; cùng khoá nhưng khác nội dung → <see cref="ErrorType.Conflict"/>.</summary>
/// <remarks>Đăng ký <b>sau</b> <see cref="TransactionBehavior{TMessage, TResponse}"/> (chạy trong transaction) để giữ chỗ khoá và
/// dữ liệu nghiệp vụ commit/rollback cùng nhau — không bao giờ tạo bản ghi thứ hai, kể cả khi hai request chạy song song.</remarks>
/// <typeparam name="TMessage">Kiểu message.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
/// <param name="store">Kho khoá idempotency.</param>
/// <param name="currentUser">Người gọi; khoá gắn <c>sub</c> để người khác không đọc được kết quả.</param>
public sealed class IdempotencyBehavior<TMessage, TResponse>(IIdempotencyStore store, ICurrentUser currentUser)
    : IPipelineBehavior<TMessage, TResponse> where TMessage : IMessage where TResponse : Result
{
    /// <summary>Mã lỗi khi khoá đã dùng cho nội dung khác.</summary>
    public const string KeyReusedCode = "Idempotency.KeyReused";

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is not IIdempotentCommand { IdempotencyKey: var raw })
            return await next(message, cancellationToken);

        if (!IdempotencyKey.IsValid(raw)) // ValidationBehavior đã chặn trước transaction; giữ lại phòng khi thiếu behavior đó
            return FailureOf<TResponse>.From(IdempotencyKey.InvalidError);

        var key = $"{currentUser.UserId}|{typeof(TMessage).FullName}|{raw}";
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(message)));
        if (await store.ReserveAsync<TResponse>(key, hash, cancellationToken) is { } first)
            return first.RequestHash == hash
                ? first.Response
                : FailureOf<TResponse>.From(Error.Conflict(KeyReusedCode, "Idempotency-Key đã dùng cho một yêu cầu khác."));

        var response = await next(message, cancellationToken);
        if (response.IsSuccess)
            await store.CompleteAsync(key, response, cancellationToken);
        return response;
    }
}

/// <summary>Quy tắc dạng của <c>Idempotency-Key</c>, dùng chung cho middleware và behavior.</summary>
public static class IdempotencyKey
{
    /// <summary>Tên header HTTP.</summary>
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Độ dài tối thiểu (UUID không gạch = 32) để khoá không đoán được.</summary>
    public const int MinLength = 16;

    /// <summary>Độ dài tối đa.</summary>
    public const int MaxLength = 128;

    /// <summary>Mã lỗi khi khoá thiếu hoặc sai dạng.</summary>
    public const string InvalidKeyCode = "Idempotency.InvalidKey";

    /// <summary>Lỗi <see cref="ErrorType.Validation"/> khi khoá thiếu hoặc sai dạng.</summary>
    public static readonly Error InvalidError = Error.Validation(InvalidKeyCode, "Idempotency-Key phải dài 16–128 ký tự, không khoảng trắng.");

    /// <summary>Kiểm khoá đúng dạng: rỗng sẽ gộp mọi lệnh cùng loại; ngắn thì đoán được kết quả của khách.</summary>
    /// <param name="key">Khoá.</param>
    /// <returns><c>true</c> nếu hợp lệ.</returns>
    public static bool IsValid(string? key) =>
        key is { Length: >= MinLength and <= MaxLength } && !key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c));
}
