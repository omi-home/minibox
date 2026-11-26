using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Domain.Accounts;

namespace Minibox.Identity.Infrastructure.Accounts;

/// <summary>Dòng bảng <c>account</c>.</summary>
public sealed class AccountRow
{
    /// <summary>Id tài khoản.</summary>
    public Guid Id { get; init; }

    /// <summary><c>customer</c> hoặc <c>staff</c>.</summary>
    public string Kind { get; init; } = "";

    /// <summary>Email hoặc <c>null</c>.</summary>
    public string? Email { get; init; }

    /// <summary>Username hoặc <c>null</c>.</summary>
    public string? Username { get; init; }

    /// <summary>Bản băm mật khẩu hoặc <c>null</c>.</summary>
    public string? PasswordHash { get; init; }

    /// <summary>Phiên bản (optimistic concurrency).</summary>
    public long Version { get; init; }
}

/// <summary>Lưu/nạp <see cref="Account"/> trong Unit of Work: domain event (<see cref="AccountRegistered"/>) vào audit_log và outbox cùng transaction.</summary>
/// <param name="unitOfWork">Unit of Work của command.</param>
public sealed class AccountRepository(NpgsqlUnitOfWork unitOfWork) : DapperRepository<Account, Guid, AccountRow>(unitOfWork)
{
    /// <inheritdoc/>
    protected override string Table => "account";

    /// <inheritdoc/>
    protected override string InsertSql =>
        "INSERT INTO account (id, kind, email, username, password_hash) VALUES (@Id, @Kind, @Email, @Username, @PasswordHash)";

    /// <inheritdoc/>
    protected override string UpdateSetSql => "email = @Email, username = @Username, password_hash = @PasswordHash";

    /// <inheritdoc/>
    protected override Account ToAggregate(AccountRow row) => Account.Load(row.Id, row.Kind switch
    {
        "customer" => AccountKind.Customer,
        "staff" => AccountKind.Staff,
        _ => throw new InvalidOperationException($"kind không hợp lệ: {row.Kind}"),
    }, row.Email, row.Username, row.PasswordHash, row.Version);

    /// <inheritdoc/>
    protected override object ToParameters(Account account) =>
        new { account.Id, Kind = KindText(account.Kind), account.Email, account.Username, account.PasswordHash };

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
}
