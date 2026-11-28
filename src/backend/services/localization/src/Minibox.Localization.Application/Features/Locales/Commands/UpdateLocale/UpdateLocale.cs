using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Errors;
using Minibox.Localization.Domain.Services;

namespace Minibox.Localization.Application.Features.Locales.Commands.UpdateLocale;

/// <summary>Lệnh sửa ngôn ngữ: tên, bật/tắt, thứ tự, fallback.</summary>
/// <param name="Code">Mã locale cần sửa.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="Enabled">Bật cho người dùng chọn.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <c>vi</c>.</param>
/// <param name="Version">Phiên bản lúc tải (optimistic concurrency).</param>
public sealed record UpdateLocale(string Code, string Name, bool Enabled, int SortOrder, string? FallbackCode, long Version)
    : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => LocalizationPermissions.LocaleManage;
}

/// <summary>Sửa <see cref="Locale"/> qua aggregate, kiểm fallback không vòng; phát <c>localization.LocaleChanged.v1</c> qua outbox.</summary>
/// <param name="locales">Repository locale.</param>
public sealed class UpdateLocaleHandler(ILocaleRepository locales) : ICommandHandler<UpdateLocale>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi <see cref="LocalizationErrors.LocaleNotFound"/>, lỗi của <see cref="Locale.Update"/>,
    /// <see cref="LocalizationErrors.LocaleFallbackNotFound"/>, <see cref="LocalizationErrors.LocaleFallbackCycle"/> hoặc xung đột phiên bản.</returns>
    public async ValueTask<Result> Handle(UpdateLocale command, CancellationToken cancellationToken)
    {
        var fallbacks = await locales.LockFallbacksAsync(cancellationToken);
        var locale = await locales.GetAsync(command.Code, cancellationToken);
        if (locale is null)
            return LocalizationErrors.LocaleNotFound;
        var updated = locale.Update(command.Name, command.Enabled, command.SortOrder, command.FallbackCode);
        if (updated.IsFailure)
            return updated;
        var valid = LocaleFallback.Validate(locale.Id, command.FallbackCode, fallbacks);
        if (valid.IsFailure)
            return valid;
        return await locales.UpdateAsync(locale, command.Version, cancellationToken);
    }
}
