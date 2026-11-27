using Minibox.BuildingBlocks.Application;
using Minibox.Identity.Domain.Aggregates.Accounts;
using Minibox.Identity.Domain.Aggregates.LoginThrottles;
using Minibox.Identity.Domain.Aggregates.RefreshTokens;
using Minibox.Identity.Domain.Aggregates.Roles;

namespace Minibox.Identity.Application.Abstractions;

/// <summary>Repository của <see cref="Account"/> (kèm MFA, mã khôi phục, phân công).</summary>
public interface IAccountRepository : IRepository<Account, Guid>
{
    /// <summary>Tìm tài khoản theo email hoặc username đã chuẩn hoá (<see cref="Account.NormalizeLogin"/>).</summary>
    /// <param name="login">Định danh đã chuẩn hoá, không rỗng.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Tài khoản, hoặc <c>null</c>.</returns>
    Task<Account?> FindByLoginAsync(string login, CancellationToken cancellationToken = default);
}

/// <summary>Repository của <see cref="LoginThrottle"/>.</summary>
public interface ILoginThrottleRepository : IRepository<LoginThrottle, string>
{
    /// <summary>Khoá tuần tự theo <paramref name="key"/> tới hết transaction (kể cả khi chưa có dòng), rồi nạp bộ đếm.</summary>
    /// <param name="key">Khoá đếm.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Bộ đếm, hoặc <c>null</c> nếu chưa từng sai.</returns>
    /// <remarks>Gọi theo cùng thứ tự khoá ở mọi lệnh (định danh trước, IP sau) để không deadlock.</remarks>
    Task<LoginThrottle?> LockAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Repository của <see cref="RefreshToken"/>.</summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken, byte[]>
{
    /// <summary>Thu hồi mọi token chưa thu hồi của phiên (cập nhật hàng loạt).</summary>
    /// <param name="familyId">Phiên.</param>
    /// <param name="now">Thời điểm thu hồi.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã ghi trong transaction hiện tại.</returns>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>Đọc danh mục vai trò (chỉ đọc; vai trò do migration/seed quản).</summary>
public interface IRoleRepository
{
    /// <summary>Nạp vai trò kèm mã quyền.</summary>
    /// <param name="code">Mã vai trò.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Vai trò, hoặc <c>null</c> nếu không tồn tại.</returns>
    Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Đọc Id → mã của mọi vai trò (cho ảnh chụp audit).</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Từ điển Id → mã.</returns>
    Task<IReadOnlyDictionary<Guid, string>> CodesAsync(CancellationToken cancellationToken = default);
}
