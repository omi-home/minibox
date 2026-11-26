using Mediator;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Behaviors;

/// <summary><see cref="IIdempotentCommand"/> gửi lặp cùng khoá (cùng người gọi, cùng loại lệnh) trả lại kết quả thành công
/// lần đầu mà không chạy lại handler. Kết quả thất bại không lưu để client retry được.</summary>
/// <typeparam name="TMessage">Kiểu message.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
/// <param name="store">Kho kết quả theo khoá.</param>
/// <param name="currentUser">Người gọi; khoá gắn <c>sub</c> để người khác không đọc được kết quả.</param>
public sealed class IdempotencyBehavior<TMessage, TResponse>(IIdempotencyStore store, ICurrentUser currentUser)
    : IPipelineBehavior<TMessage, TResponse> where TMessage : IMessage where TResponse : Result
{
    /// <summary>Mã lỗi khi khoá thiếu hoặc sai dạng.</summary>
    public const string InvalidKeyCode = "Idempotency.InvalidKey";

    /// <summary>Độ dài tối thiểu (UUID không gạch = 32) để khoá không đoán được.</summary>
    public const int MinKeyLength = 16;

    /// <summary>Độ dài tối đa.</summary>
    public const int MaxKeyLength = 128;

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is not IIdempotentCommand { IdempotencyKey: var raw })
            return await next(message, cancellationToken);

        // Fail-closed: khoá rỗng sẽ gộp mọi lệnh cùng loại; khoá ngắn đoán được sẽ lộ kết quả của khách (UserId null).
        if (raw is not { Length: >= MinKeyLength and <= MaxKeyLength } || raw.Any(char.IsWhiteSpace) || raw.Any(char.IsControl))
            return FailureOf<TResponse>.From(Error.Validation(InvalidKeyCode, $"Idempotency-Key phải dài {MinKeyLength}–{MaxKeyLength} ký tự, không khoảng trắng."));

        var key = $"{currentUser.UserId}|{typeof(TMessage).FullName}|{raw}";
        if (await store.GetAsync<TResponse>(key, cancellationToken) is { } cached)
            return cached;

        // ponytail: hai request trùng khoá chạy song song có thể cùng lọt qua GetAsync; store ở #191 phải giữ chỗ khoá
        // (INSERT … ON CONFLICT) hoặc khoá advisory nếu cần chặn tuyệt đối.
        var response = await next(message, cancellationToken);
        if (response.IsSuccess)
            await store.SaveAsync(key, response, cancellationToken);
        return response;
    }
}
