namespace Minibox.BuildingBlocks.Domain;

/// <summary>Sự kiện nghiệp vụ phát sinh trong aggregate, xử lý nội bộ trong cùng transaction với thay đổi.</summary>
public interface IDomainEvent;

/// <summary>Gốc aggregate: ranh giới nhất quán duy nhất được nạp/lưu qua repository; gom domain event chờ Unit of Work xử lý.</summary>
/// <typeparam name="TId">Kiểu định danh.</typeparam>
/// <param name="id">Định danh của aggregate.</param>
public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id) where TId : notnull
{
    readonly List<IDomainEvent> _events = [];

    /// <summary>Domain event đã phát sinh, theo thứ tự ghi nhận, chưa được xử lý.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;

    /// <summary>Ghi nhận một domain event.</summary>
    /// <param name="domainEvent">Sự kiện vừa phát sinh.</param>
    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);

    /// <summary>Xoá các domain event đã ghi nhận; Unit of Work gọi sau khi đã chuyển chúng vào outbox.</summary>
    public void ClearDomainEvents() => _events.Clear();
}
