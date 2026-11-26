using Dapper;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Repository Dapper chung: đọc/khoá theo <c>id</c>, thêm, cập nhật có kiểm cột <c>version</c> (optimistic concurrency).</summary>
/// <remarks>Bảng phải có cột <c>id</c> và <c>version bigint NOT NULL DEFAULT 0</c>. Mọi câu lệnh chạy trên kết nối + transaction của
/// <see cref="NpgsqlUnitOfWork"/>. <see cref="Table"/>, <see cref="InsertSql"/>, <see cref="UpdateSetSql"/> là hằng số của lớp con,
/// không bao giờ ghép từ input. Cột snake_case khớp thuộc tính PascalCase của <typeparamref name="TRow"/>.</remarks>
/// <typeparam name="TAggregate">Kiểu aggregate root.</typeparam>
/// <typeparam name="TId">Kiểu định danh.</typeparam>
/// <typeparam name="TRow">Kiểu dòng Dapper đọc lên.</typeparam>
/// <param name="unitOfWork">Unit of Work của command.</param>
public abstract class DapperRepository<TAggregate, TId, TRow>(NpgsqlUnitOfWork unitOfWork) : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId> where TId : notnull
{
    /// <summary>Mã lỗi khi phiên bản đã cũ hoặc dòng không còn.</summary>
    public const string ConflictCode = "Concurrency.VersionConflict";

    static DapperRepository() => DefaultTypeMap.MatchNamesWithUnderscores = true;

    /// <summary>Tên bảng (trong schema của service, theo <c>search_path</c> của role).</summary>
    protected abstract string Table { get; }

    /// <summary>Câu <c>INSERT</c> dùng tham số từ <see cref="ToParameters"/>.</summary>
    protected abstract string InsertSql { get; }

    /// <summary>Vế <c>SET</c> của câu <c>UPDATE</c> (không gồm <c>version</c>), ví dụ <c>name = @Name</c>.</summary>
    protected abstract string UpdateSetSql { get; }

    /// <summary>Unit of Work của command, cho truy vấn đặc thù ở repository riêng.</summary>
    protected NpgsqlUnitOfWork UnitOfWork => unitOfWork;

    /// <summary>Dựng aggregate từ dòng dữ liệu.</summary>
    /// <param name="row">Dòng đọc được.</param>
    /// <returns>Aggregate.</returns>
    protected abstract TAggregate ToAggregate(TRow row);

    /// <summary>Tham số cho <see cref="InsertSql"/>/<see cref="UpdateSetSql"/>; phải có thuộc tính <c>Id</c>.</summary>
    /// <param name="aggregate">Aggregate.</param>
    /// <returns>Đối tượng tham số Dapper.</returns>
    protected abstract object ToParameters(TAggregate aggregate);

    /// <inheritdoc/>
    public Task<TAggregate?> GetAsync(TId id, CancellationToken cancellationToken = default) => LoadAsync(id, "", cancellationToken);

    /// <inheritdoc/>
    public Task<TAggregate?> GetForUpdateAsync(TId id, CancellationToken cancellationToken = default) => LoadAsync(id, " FOR UPDATE", cancellationToken);

    /// <inheritdoc/>
    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(InsertSql, ToParameters(aggregate), cancellationToken);
        unitOfWork.Track(aggregate);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(TAggregate aggregate, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters(ToParameters(aggregate));
        parameters.Add("ExpectedVersion", expectedVersion);
        var rows = await ExecuteAsync(
            $"UPDATE {Table} SET {UpdateSetSql}, version = version + 1 WHERE id = @Id AND version = @ExpectedVersion", parameters, cancellationToken);
        if (rows == 0)
            return Error.Conflict(ConflictCode, $"{typeof(TAggregate).Name} đã bị thay đổi hoặc không còn; tải lại rồi thử lại.");
        unitOfWork.Track(aggregate);
        return Result.Success();
    }

    async Task<TAggregate?> LoadAsync(TId id, string lockClause, CancellationToken cancellationToken)
    {
        var row = await unitOfWork.Connection.QuerySingleOrDefaultAsync<TRow>(new CommandDefinition(
            $"SELECT * FROM {Table} WHERE id = @Id{lockClause}", new { Id = id }, unitOfWork.Transaction, cancellationToken: cancellationToken));
        return row is null ? null : ToAggregate(row);
    }

    Task<int> ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken) =>
        unitOfWork.Connection.ExecuteAsync(new CommandDefinition(sql, parameters, unitOfWork.Transaction, cancellationToken: cancellationToken));
}
