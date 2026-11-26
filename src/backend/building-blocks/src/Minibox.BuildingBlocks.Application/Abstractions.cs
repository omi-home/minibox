using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Application;

/// <summary>Repository chung cho aggregate root (spec §5.4); không có <c>IQueryable</c>/<c>Find(predicate)</c> —
/// truy vấn đặc thù đặt trong repository riêng kế thừa interface này.</summary>
/// <typeparam name="TAggregate">Kiểu aggregate root.</typeparam>
/// <typeparam name="TId">Kiểu định danh.</typeparam>
public interface IRepository<TAggregate, in TId> where TAggregate : AggregateRoot<TId> where TId : notnull
{
    /// <summary>Nạp aggregate để đọc.</summary>
    /// <param name="id">Định danh.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Aggregate, hoặc <c>null</c> nếu không có.</returns>
    Task<TAggregate?> GetAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Nạp aggregate và khoá dòng (<c>SELECT … FOR UPDATE</c>) tới hết transaction của Unit of Work.</summary>
    /// <param name="id">Định danh.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Aggregate, hoặc <c>null</c> nếu không có.</returns>
    Task<TAggregate?> GetForUpdateAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>Thêm aggregate mới.</summary>
    /// <param name="aggregate">Aggregate.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã ghi trong transaction hiện tại.</returns>
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>Ghi đè aggregate nếu phiên bản trong DB còn bằng <paramref name="expectedVersion"/>, rồi tăng phiên bản.</summary>
    /// <param name="aggregate">Aggregate đã thay đổi.</param>
    /// <param name="expectedVersion">Phiên bản lúc nạp (hoặc client gửi lên).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Thành công, hoặc lỗi <see cref="ErrorType.Conflict"/> khi phiên bản đã cũ (không ghi gì).</returns>
    Task<Result> UpdateAsync(TAggregate aggregate, long expectedVersion, CancellationToken cancellationToken = default);
}

/// <summary>Unit of Work của một command: một kết nối + transaction dùng chung cho mọi repository.</summary>
public interface IUnitOfWork
{
    /// <summary>Mở kết nối và transaction.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi transaction đã mở.</returns>
    Task BeginAsync(CancellationToken cancellationToken = default);

    /// <summary>Domain event → integration event vào outbox → ghi audit → commit, tất cả trong cùng transaction.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã commit.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Huỷ mọi thay đổi (dữ liệu, outbox, audit) của transaction.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã rollback.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>Người gọi hiện tại (lấy từ access token).</summary>
public interface ICurrentUser
{
    /// <summary>Claim <c>sub</c>; <c>null</c> với khách chưa đăng nhập.</summary>
    string? UserId { get; }
}

/// <summary>Quyết định người gọi hiện tại có quyền trong phạm vi hay không (claim <c>perms</c>).</summary>
public interface IAuthorizer
{
    /// <summary>Kiểm quyền.</summary>
    /// <param name="permission">Mã quyền.</param>
    /// <param name="scope">Phạm vi.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns><c>true</c> nếu được phép.</returns>
    ValueTask<bool> HasPermissionAsync(string permission, string scope, CancellationToken cancellationToken = default);
}

/// <summary>Kiểm quyền trong handler sau khi tải bản ghi (phạm vi dữ liệu, spec §3.6).</summary>
public static class AuthorizerExtensions
{
    /// <summary>Đòi người gọi có quyền trong phạm vi của bản ghi.</summary>
    /// <param name="authorizer">Bộ kiểm quyền.</param>
    /// <param name="permission">Mã quyền.</param>
    /// <param name="scope">Phạm vi của bản ghi (<c>wh:2</c>, <c>farm:1</c>…); quyền ở phạm vi <c>*</c> cũng đạt.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Thành công nếu được phép; lỗi <see cref="ErrorType.Forbidden"/> mã
    /// <see cref="Behaviors.AuthorizationBehavior{TMessage, TResponse}.ErrorCode"/> nếu không.</returns>
    public static async ValueTask<Result> EnsureAsync(this IAuthorizer authorizer, string permission, string scope, CancellationToken cancellationToken = default) =>
        await authorizer.HasPermissionAsync(permission, scope, cancellationToken)
            ? Result.Success()
            : Error.Forbidden(Behaviors.AuthorizationBehavior<Mediator.IMessage, Result>.ErrorCode, "Không có quyền thực hiện thao tác."); // không lộ mã quyền/phạm vi cho client
}

/// <summary>Bản ghi idempotency đã hoàn tất của lần gửi đầu.</summary>
/// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
/// <param name="RequestHash">Hash nội dung lệnh lần đầu.</param>
/// <param name="Response">Kết quả thành công lần đầu.</param>
public sealed record IdempotencyRecord<TResponse>(string RequestHash, TResponse Response) where TResponse : Result;

/// <summary>Giữ chỗ và lưu kết quả theo khoá idempotency, trong transaction của <see cref="IUnitOfWork"/>.</summary>
/// <remarks>Giữ chỗ và lưu cùng transaction với dữ liệu nghiệp vụ: lệnh thất bại/rollback thì khoá cũng mất (client retry được);
/// hai request trùng khoá chạy song song thì request sau chờ request trước commit rồi nhận bản ghi của nó.</remarks>
public interface IIdempotencyStore
{
    /// <summary>Giữ chỗ khoá cho lần gửi này.</summary>
    /// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
    /// <param name="key">Khoá đã gắn người gọi + loại lệnh.</param>
    /// <param name="requestHash">Hash nội dung lệnh.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns><c>null</c> nếu giữ chỗ được (lần đầu); ngược lại bản ghi lần đầu.</returns>
    ValueTask<IdempotencyRecord<TResponse>?> ReserveAsync<TResponse>(string key, string requestHash, CancellationToken cancellationToken = default)
        where TResponse : Result;

    /// <summary>Lưu kết quả thành công vào khoá đã giữ chỗ.</summary>
    /// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
    /// <param name="key">Khoá đã giữ chỗ.</param>
    /// <param name="response">Kết quả thành công.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã ghi (chưa commit).</returns>
    ValueTask CompleteAsync<TResponse>(string key, TResponse response, CancellationToken cancellationToken = default) where TResponse : Result;
}
