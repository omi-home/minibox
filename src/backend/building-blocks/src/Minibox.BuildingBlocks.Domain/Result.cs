namespace Minibox.BuildingBlocks.Domain;

/// <summary>Kết quả thao tác nghiệp vụ không có giá trị: thành công, hoặc thất bại kèm <see cref="Domain.Error"/>.</summary>
public class Result
{
    readonly Error? _error;

    /// <summary>Khởi tạo; <paramref name="error"/> <c>null</c> nghĩa là thành công.</summary>
    /// <param name="error">Lỗi khi thất bại.</param>
    protected Result(Error? error) => _error = error;

    /// <summary>Thao tác thành công.</summary>
    public bool IsSuccess => _error is null;

    /// <summary>Thao tác thất bại.</summary>
    public bool IsFailure => _error is not null;

    /// <summary>Lỗi của kết quả thất bại.</summary>
    /// <exception cref="InvalidOperationException">Kết quả thành công.</exception>
    public Error Error => _error ?? throw new InvalidOperationException("Result thành công không có Error.");

    /// <summary>Tạo kết quả thành công.</summary>
    /// <returns>Kết quả thành công.</returns>
    public static Result Success() => new(null);

    /// <summary>Tạo kết quả thất bại.</summary>
    /// <param name="error">Lỗi nghiệp vụ.</param>
    /// <returns>Kết quả thất bại.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> là <c>null</c>.</exception>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    /// <summary>Chuyển lỗi thành kết quả thất bại.</summary>
    /// <param name="error">Lỗi nghiệp vụ.</param>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Kết quả thao tác nghiệp vụ có giá trị khi thành công.</summary>
/// <typeparam name="T">Kiểu giá trị.</typeparam>
public sealed class Result<T> : Result
{
    readonly T? _value;

    Result(T value) : base(null) => _value = value;

    Result(Error error) : base(error) { }

    /// <summary>Giá trị của kết quả thành công.</summary>
    /// <exception cref="InvalidOperationException">Kết quả thất bại.</exception>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Result thất bại ({Error.Code}) không có Value.");

    /// <summary>Chuyển giá trị thành kết quả thành công.</summary>
    /// <param name="value">Giá trị.</param>
    public static implicit operator Result<T>(T value) => new(value);

    /// <summary>Chuyển lỗi thành kết quả thất bại.</summary>
    /// <param name="error">Lỗi nghiệp vụ, không <c>null</c>.</param>
    public static implicit operator Result<T>(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));
}
