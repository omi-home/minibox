using Minibox.BuildingBlocks.Domain;
using Minibox.ServiceName.Domain.Aggregates.Samples;

namespace Minibox.ServiceName.Domain.Errors;

/// <summary>Lỗi nghiệp vụ của <see cref="Sample"/>; mã ổn định để client và ProblemDetails dựa vào.</summary>
public static class SampleErrors
{
    /// <summary>Tên rỗng hoặc dài quá <see cref="Sample.NameMaxLength"/> ký tự.</summary>
    public static readonly Error NameInvalid =
        Error.Validation("Sample.NameInvalid", $"Tên không được rỗng và tối đa {Sample.NameMaxLength} ký tự.");
}
