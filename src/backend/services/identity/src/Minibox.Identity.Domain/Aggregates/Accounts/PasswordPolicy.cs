using System.Collections.Frozen;
using Minibox.BuildingBlocks.Domain;
using Minibox.Identity.Domain.Errors;

namespace Minibox.Identity.Domain.Aggregates.Accounts;

/// <summary>Chính sách mật khẩu (spec §3.1): tối thiểu 10 ký tự, không nằm trong danh sách mật khẩu phổ biến.</summary>
/// <remarks>Danh sách nhúng sẵn trong assembly (<c>common-passwords.txt</c>, chữ thường, chỉ mục ≥ 10 ký tự), kiểm offline.</remarks>
public static class PasswordPolicy
{
    /// <summary>Độ dài tối thiểu tính theo ký tự.</summary>
    public const int MinLength = 10;

    /// <summary>Độ dài tối đa, chặn băm chuỗi cực dài (DoS); áp dụng cả lúc đăng ký lẫn đăng nhập.</summary>
    public const int MaxLength = 128;


    static readonly FrozenSet<string> Common = LoadCommon();

    /// <summary>Kiểm mật khẩu mới trước khi băm.</summary>
    /// <param name="password">Mật khẩu người dùng nhập; <c>null</c> coi như rỗng.</param>
    /// <returns>Thành công nếu đạt; lỗi <see cref="IdentityErrors.PasswordTooShort"/>, <see cref="IdentityErrors.PasswordTooLong"/> hoặc <see cref="IdentityErrors.PasswordTooCommon"/>.</returns>
    public static Result Validate(string? password)
    {
        password ??= "";
        if (password.Length < MinLength)
            return IdentityErrors.PasswordTooShort;
        if (password.Length > MaxLength)
            return IdentityErrors.PasswordTooLong;
        if (Common.Contains(password.ToLowerInvariant()))
            return IdentityErrors.PasswordTooCommon;
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
