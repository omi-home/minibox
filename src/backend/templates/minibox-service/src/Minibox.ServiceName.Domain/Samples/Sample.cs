using Minibox.BuildingBlocks.Domain;

namespace Minibox.ServiceName.Domain.Samples;

/// <summary>Aggregate mẫu của template (bảng <c>sample</c>); thay bằng aggregate thật của service.</summary>
public sealed class Sample : AggregateRoot<Guid>
{
    Sample(Guid id, string name) : base(id) => Name = name;

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; private set; }

    /// <summary>Tạo bản ghi mới với Id UUIDv7.</summary>
    /// <param name="name">Tên hiển thị.</param>
    /// <returns>Aggregate mới.</returns>
    public static Sample Create(string name) => new(Guid.CreateVersion7(), name);
}
