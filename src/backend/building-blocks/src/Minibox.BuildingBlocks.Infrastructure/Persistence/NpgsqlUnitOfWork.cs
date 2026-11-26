using System.Text.Json;
using Dapper;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Npgsql;
using Wolverine;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;
using Wolverine.RDBMS;
using Wolverine.Runtime;

namespace Minibox.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Chuyển domain event thành integration event (kiểu trong <c>*.Contracts</c>) để đưa vào outbox.</summary>
public interface IIntegrationEventMapper
{
    /// <summary>Ánh xạ một domain event.</summary>
    /// <param name="domainEvent">Domain event vừa phát sinh.</param>
    /// <returns>Integration event cần phát, hoặc <c>null</c> nếu event chỉ dùng nội bộ.</returns>
    object? Map(IDomainEvent domainEvent);
}

/// <summary>Unit of Work trên một <see cref="NpgsqlConnection"/> + transaction, outbox Wolverine enlist cùng transaction (spec §2.1).</summary>
/// <remarks>Scoped theo command. <see cref="CommitAsync"/>: với mỗi domain event của aggregate đã thêm/cập nhật → integration event
/// vào outbox + một dòng <c>audit_log</c> → commit → đẩy outbox ra RabbitMQ. Lỗi trước commit → rollback cả dữ liệu, outbox, audit.
/// Bảng <c>audit_log</c> theo mẫu <c>database/templates/audit-log.sql</c>.</remarks>
/// <param name="dataSource">Nguồn kết nối bằng role app của service.</param>
/// <param name="bus">Message bus Wolverine của scope.</param>
/// <param name="store">Message store PostgreSQL của Wolverine (outbox trong schema service).</param>
/// <param name="currentUser">Người gọi, ghi vào cột <c>actor</c>.</param>
/// <param name="clock">Đồng hồ cho <c>occurred_at</c>.</param>
/// <param name="mapper">Ánh xạ domain → integration event; không có thì không phát gì.</param>
public sealed class NpgsqlUnitOfWork(
    NpgsqlDataSource dataSource,
    IMessageBus bus,
    IMessageStore store,
    ICurrentUser currentUser,
    TimeProvider clock,
    IIntegrationEventMapper? mapper = null) : IUnitOfWork, IAsyncDisposable
{
    const string AuditSql = """
        INSERT INTO audit_log (id, occurred_at, actor, action, aggregate_type, aggregate_id, payload)
        VALUES (@Id, @OccurredAt, @Actor, @Action, @AggregateType, @AggregateId, @Payload::jsonb)
        """;

    readonly HashSet<IAggregateRoot> _tracked = new(ReferenceEqualityComparer.Instance);
    NpgsqlConnection? _connection;
    NpgsqlTransaction? _transaction;

    /// <summary>Kết nối dùng chung cho mọi repository trong command.</summary>
    /// <exception cref="InvalidOperationException">Chưa gọi <see cref="BeginAsync"/>.</exception>
    public NpgsqlConnection Connection => _connection ?? throw new InvalidOperationException("Unit of Work chưa BeginAsync.");

    /// <summary>Transaction đang mở.</summary>
    /// <exception cref="InvalidOperationException">Chưa gọi <see cref="BeginAsync"/>.</exception>
    public NpgsqlTransaction Transaction => _transaction ?? throw new InvalidOperationException("Unit of Work chưa BeginAsync.");

    /// <summary>Ghi nhận aggregate đã thêm/cập nhật để <see cref="CommitAsync"/> xử lý domain event của nó.</summary>
    /// <param name="aggregate">Aggregate.</param>
    public void Track(IAggregateRoot aggregate) => _tracked.Add(aggregate);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Transaction đã mở.</exception>
    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null) throw new InvalidOperationException("Unit of Work đã mở transaction.");
        _connection = await dataSource.OpenConnectionAsync(cancellationToken);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken);
        await ((MessageContext)bus).EnlistInOutboxAsync(new DatabaseEnvelopeTransaction((IMessageDatabase)store, _transaction));
    }

    /// <inheritdoc/>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        var transaction = Transaction;
        var now = clock.GetUtcNow();
        var actor = currentUser.UserId ?? "system";
        foreach (var aggregate in _tracked)
        {
            foreach (var e in aggregate.DomainEvents)
            {
                if (mapper?.Map(e) is { } integrationEvent)
                    await bus.PublishAsync(integrationEvent);
                await Connection.ExecuteAsync(new CommandDefinition(AuditSql, new
                {
                    Id = Guid.CreateVersion7(now),
                    OccurredAt = now,
                    Actor = actor,
                    Action = e.GetType().Name,
                    AggregateType = aggregate.GetType().Name,
                    AggregateId = aggregate.Id.ToString(),
                    Payload = JsonSerializer.Serialize(e, e.GetType()),
                }, transaction, cancellationToken: cancellationToken));
            }
            aggregate.ClearDomainEvents();
        }
        _tracked.Clear();

        await transaction.CommitAsync(cancellationToken);
        await CloseAsync();
        // Sau commit mới đẩy ra RabbitMQ; tiến trình chết giữa chừng thì agent durability của Wolverine gửi lại từ outbox.
        await ((MessageContext)bus).FlushOutgoingMessagesAsync();
    }

    /// <inheritdoc/>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        _tracked.Clear();
        if (_transaction is not null)
            await _transaction.RollbackAsync(cancellationToken);
        await CloseAsync();
    }

    /// <summary>Đóng transaction (chưa commit thì PostgreSQL tự rollback) và trả kết nối về pool.</summary>
    /// <returns>Task hoàn tất khi đã đóng.</returns>
    public ValueTask DisposeAsync() => new(CloseAsync());

    async Task CloseAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _transaction = null;
        _connection = null;
    }
}
