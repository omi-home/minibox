using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application;

/// <summary>Lệnh ghi không trả giá trị; đi qua aggregate + repository + Unit of Work, chạy trong transaction.</summary>
public interface ICommand : Mediator.ICommand<Result>;

/// <summary>Lệnh ghi trả giá trị khi thành công; chạy trong transaction.</summary>
/// <typeparam name="TValue">Kiểu giá trị trả về.</typeparam>
public interface ICommand<TValue> : Mediator.ICommand<Result<TValue>>;

/// <summary>Truy vấn chỉ đọc; không qua repository, không mở transaction.</summary>
/// <typeparam name="TValue">Kiểu DTO trả về.</typeparam>
public interface IQuery<TValue> : Mediator.IQuery<Result<TValue>>;

/// <summary>Xử lý <see cref="ICommand"/>.</summary>
/// <typeparam name="TCommand">Kiểu lệnh.</typeparam>
public interface ICommandHandler<in TCommand> : Mediator.ICommandHandler<TCommand, Result> where TCommand : ICommand;

/// <summary>Xử lý <see cref="ICommand{TValue}"/>.</summary>
/// <typeparam name="TCommand">Kiểu lệnh.</typeparam>
/// <typeparam name="TValue">Kiểu giá trị trả về.</typeparam>
public interface ICommandHandler<in TCommand, TValue> : Mediator.ICommandHandler<TCommand, Result<TValue>> where TCommand : ICommand<TValue>;

/// <summary>Xử lý <see cref="IQuery{TValue}"/>.</summary>
/// <typeparam name="TQuery">Kiểu truy vấn.</typeparam>
/// <typeparam name="TValue">Kiểu DTO trả về.</typeparam>
public interface IQueryHandler<in TQuery, TValue> : Mediator.IQueryHandler<TQuery, Result<TValue>> where TQuery : IQuery<TValue>;

/// <summary>Message cần quyền; <see cref="Behaviors.AuthorizationBehavior{TMessage, TResponse}"/> kiểm qua <see cref="IAuthorizer"/>.</summary>
public interface IRequirePermission
{
    /// <summary>Mã quyền, ví dụ <c>order.cancel</c>.</summary>
    string Permission { get; }

    /// <summary>Phạm vi quyền (<c>*</c>, <c>wh:2</c>, <c>farm:1</c>…); mặc định toàn hệ thống.</summary>
    string Scope => "*";
}

/// <summary>Lệnh mang <c>Idempotency-Key</c>: gửi lặp cùng khoá trả lại kết quả thành công lần đầu.</summary>
public interface IIdempotentCommand
{
    /// <summary>Khoá do client sinh, giữ nguyên khi retry.</summary>
    string IdempotencyKey { get; }
}
