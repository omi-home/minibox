using Minibox.BuildingBlocks.Application;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Aggregates.Messages;

namespace Minibox.Localization.Application.Abstractions;

/// <summary>Repository của <see cref="Locale"/>.</summary>
public interface ILocaleRepository : IRepository<Locale, string>
{
    /// <summary>Khoá tuần tự mọi thay đổi fallback tới hết transaction rồi đọc fallback hiện tại của mọi locale.</summary>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Mã locale → mã fallback (<c>null</c> = <see cref="Locale.Default"/>).</returns>
    /// <remarks>Khoá để hai lệnh song song (ja → ko và ko → ja) không cùng lọt kiểm vòng.</remarks>
    Task<IReadOnlyDictionary<string, string?>> LockFallbacksAsync(CancellationToken cancellationToken = default);
}

/// <summary>Repository của <see cref="UiMessage"/>.</summary>
public interface IUiMessageRepository : IRepository<UiMessage, Guid>
{
    /// <summary>Nạp chuỗi theo khoá tự nhiên để sửa.</summary>
    /// <param name="namespace">Nhóm.</param>
    /// <param name="key">Khoá.</param>
    /// <param name="localeCode">Locale.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Chuỗi, hoặc <c>null</c>.</returns>
    Task<UiMessage?> FindAsync(string @namespace, string key, string localeCode, CancellationToken cancellationToken = default);

    /// <summary>Nạp các chuỗi có bản nháp chưa xuất bản của (namespace, locale).</summary>
    /// <param name="namespace">Nhóm.</param>
    /// <param name="localeCode">Locale.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Chuỗi cần xuất bản, sắp theo key.</returns>
    Task<IReadOnlyList<UiMessage>> ListPendingAsync(string @namespace, string localeCode, CancellationToken cancellationToken = default);
}
