using Mediator;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Behaviors;

/// <summary>Bọc command trong transaction của <see cref="IUnitOfWork"/>: thành công → commit; <see cref="Result"/> thất bại
/// hoặc exception → rollback toàn bộ (dữ liệu, outbox, audit). Query đi thẳng.</summary>
/// <remarks>Lỗi có <see cref="Error.CommitChanges"/> vẫn commit (trừ <see cref="IIdempotentCommand"/>: khoá idempotency
/// đã giữ chỗ chưa có kết quả, commit sẽ chặn client retry).</remarks>
/// <typeparam name="TMessage">Kiểu message.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
/// <param name="unitOfWork">Unit of Work của scope.</param>
public sealed class TransactionBehavior<TMessage, TResponse>(IUnitOfWork unitOfWork)
    : IPipelineBehavior<TMessage, TResponse> where TMessage : IMessage where TResponse : Result
{
    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is not IBaseCommand)
            return await next(message, cancellationToken);

        await unitOfWork.BeginAsync(cancellationToken);
        TResponse response;
        try
        {
            response = await next(message, cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }

        if (response.IsSuccess || (response.Error.CommitChanges && message is not IIdempotentCommand))
            await unitOfWork.CommitAsync(cancellationToken);
        else
            await unitOfWork.RollbackAsync(CancellationToken.None);
        return response;
    }
}
