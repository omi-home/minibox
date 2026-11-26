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

/// <summary>Lưu kết quả thành công theo khoá idempotency.</summary>
public interface IIdempotencyStore
{
    /// <summary>Lấy kết quả đã lưu.</summary>
    /// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
    /// <param name="key">Khoá đã gắn người gọi + loại lệnh.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Kết quả lần đầu, hoặc <c>null</c> nếu chưa có.</returns>
    ValueTask<TResponse?> GetAsync<TResponse>(string key, CancellationToken cancellationToken = default) where TResponse : Result;

    /// <summary>Lưu kết quả thành công.</summary>
    /// <typeparam name="TResponse">Kiểu kết quả.</typeparam>
    /// <param name="key">Khoá đã gắn người gọi + loại lệnh.</param>
    /// <param name="response">Kết quả.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã lưu.</returns>
    ValueTask SaveAsync<TResponse>(string key, TResponse response, CancellationToken cancellationToken = default) where TResponse : Result;
}
