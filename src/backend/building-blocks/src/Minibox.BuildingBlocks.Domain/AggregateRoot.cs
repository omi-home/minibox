namespace Minibox.BuildingBlocks.Domain;

/// <summary>Sự kiện nghiệp vụ phát sinh trong aggregate, xử lý nội bộ trong cùng transaction với thay đổi.</summary>
public interface IDomainEvent;

/// <summary>Gốc aggregate không kèm kiểu Id, để Unit of Work gom domain event của mọi aggregate đã thay đổi.</summary>
public interface IAggregateRoot
{
    /// <summary>Định danh của aggregate.</summary>
    object Id { get; }

    /// <summary>Domain event chưa được xử lý.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Xoá các domain event đã xử lý.</summary>
    void ClearDomainEvents();
}

/// <summary>Gốc aggregate: ranh giới nhất quán duy nhất được nạp/lưu qua repository; gom domain event chờ Unit of Work xử lý.</summary>
/// <typeparam name="TId">Kiểu định danh.</typeparam>
/// <param name="id">Định danh của aggregate.</param>
public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id), IAggregateRoot where TId : notnull
{
    object IAggregateRoot.Id => Id;

    readonly List<IDomainEvent> _events = [];

    /// <summary>Domain event đã phát sinh, theo thứ tự ghi nhận, chưa được xử lý.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    /// <summary>Ghi nhận một domain event.</summary>
    /// <param name="domainEvent">Sự kiện vừa phát sinh.</param>
    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    /// <summary>Xoá các domain event đã ghi nhận; Unit of Work gọi sau khi đã chuyển chúng vào outbox.</summary>
    public void ClearDomainEvents() => _events.Clear();
}
