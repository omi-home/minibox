namespace Minibox.Identity.Application.Abstractions;

/// <summary>Kết quả so khớp mật khẩu với bản băm đã lưu.</summary>
public enum PasswordVerification
{
    /// <summary>Sai mật khẩu hoặc bản băm không hợp lệ.</summary>
    Failed,
    /// <summary>Đúng mật khẩu.</summary>
    Success,
    /// <summary>Đúng mật khẩu nhưng bản băm dùng tham số cũ: băm lại và lưu khi đăng nhập.</summary>
    SuccessRehashNeeded,
}

/// <summary>Băm và kiểm mật khẩu (spec §3.1); cài đặt không bao giờ log mật khẩu hay bản băm.</summary>
public interface IPasswordHasher
{
    /// <summary>Băm mật khẩu với salt ngẫu nhiên và tham số hiện hành.</summary>
    /// <param name="password">Mật khẩu đã qua <c>PasswordPolicy</c>.</param>
    /// <returns>Chuỗi PHC tự mô tả thuật toán, tham số và salt.</returns>
    string Hash(string password);

    /// <summary>So khớp mật khẩu với bản băm đã lưu, thời gian không phụ thuộc vị trí sai.</summary>
    /// <param name="hash">Bản băm đã lưu.</param>
    /// <param name="password">Mật khẩu người dùng nhập.</param>
    /// <returns>Kết quả so khớp; bản băm hỏng trả <see cref="PasswordVerification.Failed"/>.</returns>
    PasswordVerification Verify(string hash, string password);
}
