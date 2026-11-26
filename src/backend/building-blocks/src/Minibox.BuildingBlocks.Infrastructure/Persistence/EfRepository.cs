using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Repository EF Core chung: đọc/khoá theo khoá chính, thêm, cập nhật có kiểm cột <c>version</c> (optimistic concurrency).</summary>
/// <remarks>Entity phải được cấu hình bằng <see cref="PersistenceSetup.IsAggregateRoot{T}"/> (cột <c>version</c> là concurrency token).
/// Mọi thao tác chạy trên <see cref="DbContext"/> và transaction của <see cref="EfUnitOfWork{TDb}"/>; <see cref="AddAsync"/> và
/// <see cref="UpdateAsync"/> ghi xuống DB ngay (chưa commit) để lỗi ràng buộc/xung đột lộ ra tại chỗ gọi.</remarks>
/// <typeparam name="TAggregate">Kiểu aggregate root.</typeparam>
/// <typeparam name="TId">Kiểu định danh.</typeparam>
/// <param name="db">DbContext của service (scoped, cùng instance với Unit of Work).</param>
public abstract class EfRepository<TAggregate, TId>(DbContext db) : IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId> where TId : notnull
{
    /// <summary>Mã lỗi khi phiên bản đã cũ hoặc dòng không còn.</summary>
    public const string ConflictCode = "Concurrency.VersionConflict";

    /// <summary>DbContext của command, cho truy vấn LINQ đặc thù ở repository riêng.</summary>
    protected DbContext Db => db;

    /// <summary>Tập aggregate trong DbContext.</summary>
    protected DbSet<TAggregate> Set => db.Set<TAggregate>();

    /// <inheritdoc/>
    public async Task<TAggregate?> GetAsync(TId id, CancellationToken cancellationToken = default) =>
        await Set.FindAsync([id], cancellationToken);

    /// <inheritdoc/>
    public Task<TAggregate?> GetForUpdateAsync(TId id, CancellationToken cancellationToken = default)
    {
        // Tên bảng/cột lấy từ model EF (hằng số của mapping), giá trị id là tham số.
        var entity = db.Model.FindEntityType(typeof(TAggregate))!;
        var key = entity.FindPrimaryKey()!.Properties.Single().GetColumnName();
        var table = entity.GetSchema() is { } schema ? $"\"{schema}\".\"{entity.GetTableName()}\"" : $"\"{entity.GetTableName()}\"";
        var sql = $"SELECT * FROM {table} WHERE \"{key}\" = {{0}} FOR UPDATE";
        return Set.FromSqlRaw(sql, id).SingleOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        Set.Add(aggregate);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(TAggregate aggregate, long expectedVersion, CancellationToken cancellationToken = default)
    {
        var entry = db.Entry(aggregate);
        if (entry.State == EntityState.Detached) entry.State = EntityState.Modified;
        var version = entry.Property<long>(PersistenceSetup.VersionProperty);
        version.OriginalValue = expectedVersion;
        version.CurrentValue = expectedVersion + 1;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            entry.State = EntityState.Detached;
            return Error.Conflict(ConflictCode, $"{typeof(TAggregate).Name} đã bị thay đổi hoặc không còn; tải lại rồi thử lại.");
        }
    }
}
