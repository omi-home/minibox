using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Minibox.Localization.Application.Abstractions;
using Minibox.Localization.Domain.Aggregates.Locales;
using Minibox.Localization.Domain.Errors;
using Minibox.Localization.Domain.Services;

namespace Minibox.Localization.Application.Features.Locales.Commands.CreateLocale;

/// <summary>Lệnh thêm ngôn ngữ (ở trạng thái tắt; bật bằng <c>UpdateLocale</c> khi bản dịch đã sẵn sàng).</summary>
/// <param name="Code">Mã BCP 47.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <c>vi</c>.</param>
public sealed record CreateLocale(string Code, string Name, int SortOrder, string? FallbackCode) : ICommand, IRequirePermission
{
    /// <inheritdoc/>
    public string Permission => LocalizationPermissions.LocaleManage;
}

/// <summary>Tạo <see cref="Locale"/>, kiểm fallback tồn tại và không vòng; phát <c>localization.LocaleChanged.v1</c> qua outbox.</summary>
/// <param name="locales">Repository locale.</param>
public sealed class CreateLocaleHandler(ILocaleRepository locales) : ICommandHandler<CreateLocale>
{
    /// <inheritdoc/>
    /// <returns>Thành công; lỗi dạng dữ liệu của <see cref="Locale.Create"/>, <see cref="LocalizationErrors.LocaleExists"/>,
    /// <see cref="LocalizationErrors.LocaleFallbackNotFound"/> hoặc <see cref="LocalizationErrors.LocaleFallbackCycle"/>.</returns>
    public async ValueTask<Result> Handle(CreateLocale command, CancellationToken cancellationToken)
    {
        var created = Locale.Create(command.Code, command.Name, command.SortOrder, command.FallbackCode);
        if (created.IsFailure)
            return created.Error;
        var fallbacks = await locales.LockFallbacksAsync(cancellationToken);
        if (fallbacks.ContainsKey(command.Code))
            return LocalizationErrors.LocaleExists;
        var valid = LocaleFallback.Validate(command.Code, command.FallbackCode, fallbacks);
        if (valid.IsFailure)
            return valid;
        await locales.AddAsync(created.Value, cancellationToken);
        return Result.Success();
    }
}
