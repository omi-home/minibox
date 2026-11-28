using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Wolverine;
using Wolverine.RabbitMQ;
using Wolverine.RabbitMQ.Internal;

namespace Minibox.BuildingBlocks.Infrastructure.Localization;

/// <summary>Thân <c>localization.LocaleChanged.v1</c> phía consumer (hợp đồng <c>contracts/events/localization.LocaleChanged.v1.json</c>).</summary>
/// <param name="Code">Mã BCP 47.</param>
/// <param name="Name">Tên tự gọi.</param>
/// <param name="Enabled">Đang bật.</param>
/// <param name="SortOrder">Thứ tự hiển thị.</param>
/// <param name="FallbackCode">Locale rơi về; <c>null</c> = <c>vi</c>.</param>
/// <remarks>Building block không tham chiếu project service, nên khai lại record và gắn cùng tên message Wolverine với
/// <c>Minibox.Localization.Contracts.Events.LocaleChangedMessage</c> (test ở localization giữ hai bên khớp).</remarks>
[Wolverine.Attributes.MessageIdentity(LocaleReplicaSetup.LocaleChangedMessageType)]
public sealed record LocaleChangedMessage(string Code, string Name, bool Enabled, int SortOrder, string? FallbackCode);

/// <summary>Dòng bảng <c>locale_replica</c> (mẫu <c>database/templates/locale-replica.sql</c>): bản sao một locale của localization.</summary>
public sealed class LocaleReplicaRow
{
    /// <summary>Mã BCP 47, khoá chính.</summary>
    public string Code { get; set; } = "";

    /// <summary>Tên tự gọi.</summary>
    public string Name { get; set; } = "";

    /// <summary>Đang bật.</summary>
    public bool Enabled { get; set; }

    /// <summary>Thứ tự hiển thị.</summary>
    public int SortOrder { get; set; }

    /// <summary>Locale rơi về; <c>null</c> = <c>vi</c>.</summary>
    public string? FallbackCode { get; set; }

    /// <summary>Lúc nhận sự kiện gần nhất.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Ánh xạ <see cref="LocaleReplicaRow"/> vào bảng <c>locale_replica</c> trong schema mặc định của DbContext service.</summary>
public sealed class LocaleReplicaConfiguration : IEntityTypeConfiguration<LocaleReplicaRow>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LocaleReplicaRow> builder)
    {
        builder.ToTable("locale_replica");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.Enabled).HasColumnName("enabled");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.FallbackCode).HasColumnName("fallback_code");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
    }
}

/// <summary>Ghi bản sao locale khi nhận <see cref="LocaleChangedMessage"/>.</summary>
public interface ILocaleReplicaWriter
{
    /// <summary>Ghi đè dòng theo <c>code</c> bằng trạng thái đầy đủ trong sự kiện.</summary>
    /// <param name="changed">Sự kiện từ localization.</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã lưu.</returns>
    Task ApplyAsync(LocaleChangedMessage changed, CancellationToken cancellationToken = default);
}

/// <summary>Bản sao locale trên DbContext của service: đọc chuỗi fallback và ghi khi nhận sự kiện.</summary>
/// <typeparam name="TContext">DbContext của service, đã áp <see cref="LocaleReplicaConfiguration"/>.</typeparam>
/// <param name="db">DbContext.</param>
/// <param name="time">Đồng hồ.</param>
public sealed class EfLocaleReplica<TContext>(TContext db, TimeProvider time) : ILocaleReplica, ILocaleReplicaWriter where TContext : DbContext
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ChainAsync(string locale, CancellationToken cancellationToken = default)
    {
        var all = await db.Set<LocaleReplicaRow>().AsNoTracking()
            .Select(l => new { l.Code, l.Enabled, l.FallbackCode })
            .ToListAsync(cancellationToken);
        if (!all.Any(l => l.Code == locale && l.Enabled))
            return [LocaleChain.Default];
        return LocaleChain.Build(locale, all.ToDictionary(l => l.Code, l => l.FallbackCode)) ?? [locale, LocaleChain.Default];
    }

    /// <inheritdoc/>
    /// <remarks>ponytail: ghi đè theo thứ tự nhận; sự kiện không có version nên hai lần sửa cùng locale đến ngược thứ tự sẽ giữ bản cũ hơn.
    /// Thêm <c>version</c> vào <c>LocaleChanged.v2</c> nếu điều này xảy ra thật.</remarks>
    public async Task ApplyAsync(LocaleChangedMessage changed, CancellationToken cancellationToken = default)
    {
        var row = await db.Set<LocaleReplicaRow>().FindAsync([changed.Code], cancellationToken);
        if (row is null)
            db.Add(row = new LocaleReplicaRow { Code = changed.Code });
        row.Name = changed.Name;
        row.Enabled = changed.Enabled;
        row.SortOrder = changed.SortOrder;
        row.FallbackCode = changed.FallbackCode;
        row.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Handler Wolverine của <see cref="LocaleChangedMessage"/>; inbox bền nên message trùng chỉ xử lý một lần.</summary>
public static class LocaleChangedHandler
{
    /// <summary>Cập nhật bản sao locale.</summary>
    /// <param name="changed">Sự kiện.</param>
    /// <param name="writer">Bộ ghi bản sao (đăng ký bởi <see cref="LocaleReplicaSetup.AddLocaleReplica{TContext}"/>).</param>
    /// <param name="cancellationToken">Huỷ thao tác.</param>
    /// <returns>Task hoàn tất khi đã lưu.</returns>
    public static Task Handle(LocaleChangedMessage changed, ILocaleReplicaWriter writer, CancellationToken cancellationToken) =>
        writer.ApplyAsync(changed, cancellationToken);
}

/// <summary>Đăng ký bản sao locale cho service có bảng <c>*_translation</c>.</summary>
public static class LocaleReplicaSetup
{
    /// <summary>Exchange fanout, durable mà localization phát <c>localization.LocaleChanged.v1</c>.</summary>
    public const string LocaleChangedExchange = "localization.LocaleChanged.v1";

    /// <summary>Tên message Wolverine của sự kiện: tên đầy đủ của <c>LocaleChangedV1</c> ở <c>Minibox.Localization.Contracts</c>.</summary>
    public const string LocaleChangedMessageType = "Minibox.Localization.Contracts.Events.LocaleChangedV1";

    /// <summary>Đăng ký <see cref="ILocaleReplica"/> và <see cref="ILocaleReplicaWriter"/> trên DbContext của service.</summary>
    /// <typeparam name="TContext">DbContext đã áp <see cref="LocaleReplicaConfiguration"/> trong <c>OnModelCreating</c>.</typeparam>
    /// <param name="services">DI container; cần sẵn <see cref="TimeProvider"/>.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddLocaleReplica<TContext>(this IServiceCollection services) where TContext : DbContext =>
        // Kiểu cụ thể, không factory lambda: Wolverine từ chối service location khi dựng handler.
        services
            .AddScoped<ILocaleReplica, EfLocaleReplica<TContext>>()
            .AddScoped<ILocaleReplicaWriter, EfLocaleReplica<TContext>>();

    /// <summary>Nghe <c>localization.LocaleChanged.v1</c>: queue bền <c>&lt;service&gt;.localization.LocaleChanged.v1</c> bind vào exchange
    /// fanout của localization, xử lý bằng <see cref="LocaleChangedHandler"/>.</summary>
    /// <param name="rabbit">Cấu hình RabbitMQ trả về từ <c>UseMiniboxMessaging</c>.</param>
    /// <param name="options">Tuỳ chọn Wolverine.</param>
    /// <param name="service">Tên service (schema); hằng số, không lấy từ input.</param>
    /// <returns><paramref name="rabbit"/> để khai báo tiếp.</returns>
    public static RabbitMqTransportExpression ReceiveLocaleChanges(this RabbitMqTransportExpression rabbit, WolverineOptions options, string service)
    {
        var queue = $"{service}.{LocaleChangedExchange}";
        rabbit.BindExchange(LocaleChangedExchange, ExchangeType.Fanout).ToQueue(queue);
        options.ListenToRabbitQueue(queue);
        options.Discovery.IncludeType(typeof(LocaleChangedHandler));
        return rabbit;
    }
}
