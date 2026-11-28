using Mediator;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application.Behaviors;

/// <summary>Message <see cref="IRequirePermission"/> mà người gọi không có quyền → <see cref="ErrorType.Forbidden"/>.</summary>
/// <typeparam name="TMessage">Kiểu message.</typeparam>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
/// <param name="authorizer">Bộ kiểm quyền.</param>
public sealed class AuthorizationBehavior<TMessage, TResponse>(IAuthorizer authorizer)
    : IPipelineBehavior<TMessage, TResponse> where TMessage : IMessage where TResponse : Result
{
    /// <summary>Mã lỗi khi thiếu quyền.</summary>
    public const string ErrorCode = "Auth.Forbidden";

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (message is IRequirePermission p && !await authorizer.HasPermissionAsync(p.Permission, p.Scope, cancellationToken))
            return FailureOf<TResponse>.From(Error.Forbidden(ErrorCode, $"Thiếu quyền {p.Permission} trên {p.Scope}."));
        return await next(message, cancellationToken);
    }
}
