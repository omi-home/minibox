using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Wolverine.EntityFrameworkCore;

namespace Minibox.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Chuyển domain event thành integration event (kiểu trong <c>*.Contracts</c>) để đưa vào outbox.</summary>
public interface IIntegrationEventMapper
{
    /// <summary>Ánh xạ một domain event.</summary>
    /// <param name="domainEvent">Domain event vừa phát sinh.</param>
    /// <returns>Integration event cần phát, hoặc <c>null</c> nếu event chỉ dùng nội bộ.</returns>
    object? Map(IDomainEvent domainEvent);
}

/// <summary>Unit of Work trên <see cref="DbContext"/> của service: một transaction cho dữ liệu, outbox Wolverine và audit (spec §2.1, §5.4).</summary>
/// <remarks>Scoped theo command. <see cref="CommitAsync"/>: lưu thay đổi còn chờ → với mỗi domain event của aggregate đang được
/// <see cref="DbContext"/> theo dõi: integration event vào outbox + một dòng <c>audit_log</c> → commit → đẩy outbox ra RabbitMQ.
/// Lỗi trước khi commit xong → rollback cả dữ liệu, outbox, audit. Bảng <c>audit_log</c> theo mẫu <c>database/templates/audit-log.sql</c>;
/// DbContext phải gọi <c>MapWolverineEnvelopeStorage(schema)</c>.</remarks>
/// <typeparam name="TDb">DbContext của service.</typeparam>
/// <param name="outbox">Outbox Wolverine gắn với DbContext của scope.</param>
/// <param name="currentUser">Người gọi, ghi vào cột <c>actor</c>.</param>
/// <param name="clock">Đồng hồ cho <c>occurred_at</c>.</param>
/// <param name="mapper">Ánh xạ domain → integration event; không có thì không phát gì.</param>
public sealed class EfUnitOfWork<TDb>(
    IDbContextOutbox<TDb> outbox,
    ICurrentUser currentUser,
    TimeProvider clock,
    IIntegrationEventMapper? mapper = null) : IUnitOfWork where TDb : DbContext
{
    IDbContextTransaction? _transaction;

    DbContext Db => outbox.DbContext;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Transaction đã mở.</exception>
    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null) throw new InvalidOperationException("Unit of Work đã mở transaction.");
        _transaction = await Db.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Chưa gọi <see cref="BeginAsync"/>.</exception>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var transaction = _transaction ?? throw new InvalidOperationException("Unit of Work chưa BeginAsync.");
        try
        {
            await Db.SaveChangesAsync(cancellationToken);
            var now = clock.GetUtcNow();
            var actor = currentUser.UserId ?? "system";
            foreach (var aggregate in Db.ChangeTracker.Entries<IAggregateRoot>().Select(e => e.Entity).ToList())
            {
                foreach (var e in aggregate.DomainEvents)
                {
                    if (mapper?.Map(e) is { } integrationEvent)
                        await outbox.PublishAsync(integrationEvent);
                    var (id, action, type, aggregateId, payload) =
                        (Guid.CreateVersion7(now), e.GetType().Name, aggregate.GetType().Name, aggregate.Id.ToString(), JsonSerializer.Serialize(e, e.GetType()));
                    await Db.Database.ExecuteSqlAsync($"""
                        INSERT INTO audit_log (id, occurred_at, actor, action, aggregate_type, aggregate_id, payload)
                        VALUES ({id}, {now}, {actor}, {action}, {type}, {aggregateId}, {payload}::jsonb)
                        """, cancellationToken);
                }
                aggregate.ClearDomainEvents();
            }
            await Db.SaveChangesAsync(cancellationToken); // envelope outbox
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Dispose transaction chưa commit = rollback (kể cả khi COMMIT lỗi và transaction đã kết thúc, không gọi RollbackAsync được).
            Db.ChangeTracker.Clear();
            await CloseAsync();
            throw;
        }
        await CloseAsync();
        // Sau commit mới đẩy ra RabbitMQ; tiến trình chết giữa chừng thì agent durability của Wolverine gửi lại từ outbox.
        await outbox.FlushOutgoingMessagesAsync();
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        Db.ChangeTracker.Clear();
        if (_transaction is not null)
        {
            try { await _transaction.RollbackAsync(cancellationToken); }
            finally { await CloseAsync(); }
        }
    }

    async Task CloseAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        _transaction = null;
    }
}
