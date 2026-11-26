using System.Collections.Frozen;
using Minibox.BuildingBlocks.Domain;

namespace Minibox.Identity.Domain.Accounts;

/// <summary>Chính sách mật khẩu (spec §3.1): tối thiểu 10 ký tự, không nằm trong danh sách mật khẩu phổ biến.</summary>
/// <remarks>Danh sách nhúng sẵn trong assembly (<c>common-passwords.txt</c>, chữ thường, chỉ mục ≥ 10 ký tự), kiểm offline.</remarks>
public static class PasswordPolicy
{
    /// <summary>Độ dài tối thiểu tính theo ký tự.</summary>
    public const int MinLength = 10;

    /// <summary>Độ dài tối đa, chặn băm chuỗi cực dài (DoS); áp dụng cả lúc đăng ký lẫn đăng nhập.</summary>
    public const int MaxLength = 128;

    /// <summary>Mã lỗi khi mật khẩu ngắn hơn <see cref="MinLength"/>.</summary>
    public const string TooShortCode = "Identity.PasswordTooShort";

    /// <summary>Mã lỗi khi mật khẩu dài hơn <see cref="MaxLength"/>.</summary>
    public const string TooLongCode = "Identity.PasswordTooLong";

    /// <summary>Mã lỗi khi mật khẩu nằm trong danh sách phổ biến.</summary>
    public const string CommonCode = "Identity.PasswordTooCommon";

    static readonly FrozenSet<string> Common = LoadCommon();

    /// <summary>Kiểm mật khẩu mới trước khi băm.</summary>
    /// <param name="password">Mật khẩu người dùng nhập; <c>null</c> coi như rỗng.</param>
    /// <returns>Thành công nếu đạt; lỗi <see cref="TooShortCode"/>, <see cref="TooLongCode"/> hoặc <see cref="CommonCode"/> (Validation).</returns>
    public static Result Validate(string? password)
    {
        password ??= "";
        if (password.Length < MinLength)
            return Error.Validation(TooShortCode, $"Mật khẩu cần ít nhất {MinLength} ký tự.");
        if (password.Length > MaxLength)
            return Error.Validation(TooLongCode, $"Mật khẩu tối đa {MaxLength} ký tự.");
        if (Common.Contains(password.ToLowerInvariant()))
            return Error.Validation(CommonCode, "Mật khẩu quá phổ biến, hãy chọn mật khẩu khác.");
        return Result.Success();
    }

    static FrozenSet<string> LoadCommon()
    {
        using var stream = typeof(PasswordPolicy).Assembly.GetManifestResourceStream("common-passwords.txt")
            ?? throw new InvalidOperationException("Thiếu resource common-passwords.txt");
        using var reader = new StreamReader(stream);
        var set = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
            if (line.Length > 0) set.Add(line);
        return set.ToFrozenSet(StringComparer.Ordinal);
    }
}
