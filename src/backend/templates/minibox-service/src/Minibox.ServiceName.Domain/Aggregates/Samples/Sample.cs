using Minibox.BuildingBlocks.Domain;
using Minibox.ServiceName.Domain.Errors;

namespace Minibox.ServiceName.Domain.Aggregates.Samples;

/// <summary>Aggregate mẫu của template (bảng <c>sample</c>); thay bằng aggregate thật của service.</summary>
public sealed class Sample : AggregateRoot<Guid>
{
    /// <summary>Độ dài tối đa của <see cref="Name"/>.</summary>
    public const int NameMaxLength = 200;

    Sample(Guid id, string name) : base(id) => Name = name;

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; private set; }

    /// <summary>Tạo bản ghi mới với Id UUIDv7; bất biến của aggregate kiểm tại đây, không phụ thuộc validator ở Application.</summary>
    /// <param name="name">Tên hiển thị, không rỗng, tối đa <see cref="NameMaxLength"/> ký tự.</param>
    /// <returns>Aggregate mới; lỗi <see cref="SampleErrors.NameInvalid"/> nếu tên rỗng hoặc quá dài.</returns>
    public static Result<Sample> Create(string name) =>
        string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength
            ? SampleErrors.NameInvalid
            : new Sample(Guid.CreateVersion7(), name.Trim());
}
