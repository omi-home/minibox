namespace Minibox.BuildingBlocks.Domain;

/// <summary>Đối tượng có định danh: hai entity bằng nhau khi cùng kiểu cụ thể và cùng <see cref="Id"/>.</summary>
/// <typeparam name="TId">Kiểu định danh.</typeparam>
/// <param name="id">Định danh, không đổi trong suốt vòng đời entity.</param>
public abstract class Entity<TId>(TId id) : IEquatable<Entity<TId>> where TId : notnull
{
    /// <summary>Định danh của entity.</summary>
    public TId Id { get; } = id;

    /// <inheritdoc/>
    public bool Equals(Entity<TId>? other) =>
        other is not null && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>So sánh bằng theo kiểu cụ thể và <see cref="Id"/>.</summary>
    /// <param name="left">Entity bên trái.</param>
    /// <param name="right">Entity bên phải.</param>
    /// <returns><c>true</c> nếu cùng kiểu và cùng Id, hoặc cả hai <c>null</c>.</returns>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    /// <summary>Phủ định của <c>==</c>.</summary>
    /// <param name="left">Entity bên trái.</param>
    /// <param name="right">Entity bên phải.</param>
    /// <returns><c>true</c> nếu khác kiểu hoặc khác Id.</returns>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
