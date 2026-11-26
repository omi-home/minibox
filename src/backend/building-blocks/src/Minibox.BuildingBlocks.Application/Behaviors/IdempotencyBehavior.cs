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
    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is not IIdempotentCommand { IdempotencyKey: var raw })
            return await next(message, cancellationToken);

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
