using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Lưu/nạp <see cref="Account"/> trong Unit of Work: domain event (<see cref="AccountRegistered"/>) vào audit_log và outbox cùng transaction.</summary>
/// <param name="db">DbContext identity của command.</param>
public sealed class AccountRepository(DbContext db) : EfRepository<Account, Guid>(db)
{
    /// <summary>Giá trị cột <c>kind</c> và trường <c>kind</c> của sự kiện; tường minh để đổi tên enum không làm lệch DB/hợp đồng.</summary>
    /// <param name="kind">Loại tài khoản.</param>
    /// <returns><c>customer</c> hoặc <c>staff</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Giá trị enum lạ.</exception>
    public static string KindText(AccountKind kind) => kind switch
    {
        AccountKind.Customer => "customer",
        AccountKind.Staff => "staff",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Đọc giá trị cột <c>kind</c>.</summary>
    /// <param name="kind"><c>customer</c> hoặc <c>staff</c>.</param>
    /// <returns>Loại tài khoản.</returns>
    /// <exception cref="InvalidOperationException">Giá trị lạ trong DB.</exception>
    public static AccountKind ParseKind(string kind) => kind switch
    {
        "customer" => AccountKind.Customer,
        "staff" => AccountKind.Staff,
        _ => throw new InvalidOperationException($"kind không hợp lệ: {kind}"),
    };
}
