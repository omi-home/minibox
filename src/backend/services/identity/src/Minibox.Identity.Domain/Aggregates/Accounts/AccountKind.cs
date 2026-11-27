namespace Minibox.Identity.Domain.Aggregates.Accounts;

/// <summary>Loại tài khoản (cột <c>kind</c>, claim <c>kind</c> của access token).</summary>
public enum AccountKind
{
    /// <summary>Khách mua hàng.</summary>
    Customer,
    /// <summary>Nhân viên vận hành.</summary>
    Staff,
}

/// <summary>Mã chữ của <see cref="AccountKind"/>; tường minh để đổi tên enum không làm lệch DB, token hay hợp đồng sự kiện.</summary>
public static class AccountKindCodes
{
    /// <summary>Đổi loại tài khoản sang mã chữ.</summary>
    /// <param name="kind">Loại tài khoản.</param>
    /// <returns><c>customer</c> hoặc <c>staff</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Giá trị enum lạ.</exception>
    public static string ToCode(this AccountKind kind) => kind switch
    {
        AccountKind.Customer => "customer",
        AccountKind.Staff => "staff",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Đọc mã chữ của loại tài khoản.</summary>
    /// <param name="code"><c>customer</c> hoặc <c>staff</c>.</param>
    /// <returns>Loại tài khoản.</returns>
    /// <exception cref="InvalidOperationException">Mã lạ (dữ liệu hỏng).</exception>
    public static AccountKind Parse(string code) => code switch
    {
        "customer" => AccountKind.Customer,
        "staff" => AccountKind.Staff,
        _ => throw new InvalidOperationException($"kind không hợp lệ: {code}"),
    };
}
