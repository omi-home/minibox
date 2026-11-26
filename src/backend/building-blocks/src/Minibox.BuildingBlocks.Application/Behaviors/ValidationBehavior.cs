using FluentValidation;
using Mediator;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Behaviors;

/// <summary>Chạy mọi <see cref="IValidator{T}"/> của message; lỗi → <see cref="ErrorType.Validation"/>, không gọi tầng trong
/// (chưa mở transaction nên không ghi gì). <see cref="IIdempotentCommand"/> có khoá sai dạng → <see cref="IdempotencyKey.InvalidError"/>.</summary>
/// <typeparam name="TMessage">Kiểu message.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
/// <param name="validators">Validator đăng ký cho <typeparamref name="TMessage"/>.</param>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse> where TMessage : IMessage where TResponse : Result
{
    /// <summary>Mã lỗi khi dữ liệu vào không hợp lệ.</summary>
    public const string ErrorCode = "Validation.Failed";

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is IIdempotentCommand { IdempotencyKey: var key } && !IdempotencyKey.IsValid(key))
            return FailureOf<TResponse>.From(IdempotencyKey.InvalidError);

        var failures = new List<string>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, cancellationToken);
            failures.AddRange(result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        }
        return failures.Count == 0
            ? await next(message, cancellationToken)
            : FailureOf<TResponse>.From(Error.Validation(ErrorCode, string.Join("; ", failures)));
    }
}
