using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Minibox.BuildingBlocks.Application;
using Minibox.BuildingBlocks.Domain;
using Wolverine.EntityFrameworkCore;

namespace Minibox.BuildingBlocks.Infrastructure.Persistence;

/// <summary>Đăng ký và quy ước mapping EF Core chung cho mọi service (spec §4.x, CR #427).</summary>
public static partial class PersistenceSetup
{
    /// <summary>Tên thuộc tính EF của cột <c>version</c> (thuộc tính CLR nếu aggregate có, không thì shadow property).</summary>
    public const string VersionProperty = "Version";

    /// <summary>Đăng ký <typeparamref name="TDb"/> (Npgsql, tích hợp outbox Wolverine), <see cref="EfUnitOfWork{TDb}"/> làm
    /// <see cref="IUnitOfWork"/>, <see cref="PostgresIdempotencyStore"/> và <see cref="DbContext"/> trỏ về cùng instance scoped.</summary>
    /// <remarks>Cần Wolverine đã bật qua <c>UseMiniboxMessaging</c>. Không tạo/migrate schema (DbUp quản DDL).</remarks>
    /// <typeparam name="TDb">DbContext của service.</typeparam>
    /// <param name="services">DI container.</param>
    /// <param name="connectionString">Chuỗi kết nối role app (chỉ DML), không dùng chuỗi migrator.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddMiniboxPersistence<TDb>(this IServiceCollection services, string connectionString) where TDb : DbContext
    {
        services.AddDbContextWithWolverineIntegration<TDb>(o => o.UseNpgsql(connectionString));
        return services
            .AddScoped<DbContext>(sp => sp.GetRequiredService<TDb>())
            .AddScoped<EfUnitOfWork<TDb>>()
            .AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EfUnitOfWork<TDb>>())
            .AddScoped<IIdempotencyStore, PostgresIdempotencyStore>();
    }

    /// <summary>Cấu hình entity là aggregate root: khoá chính <c>Id</c>, bỏ qua danh sách domain event, cột <c>version</c> là concurrency token.</summary>
    /// <typeparam name="T">Kiểu aggregate.</typeparam>
    /// <param name="builder">Builder của entity.</param>
    /// <returns><paramref name="builder"/>.</returns>
    public static EntityTypeBuilder<T> IsAggregateRoot<T>(this EntityTypeBuilder<T> builder) where T : class, IAggregateRoot
    {
        builder.HasKey(nameof(IAggregateRoot.Id)); // Id chỉ có getter (Entity<TId>): EF ghi qua backing field
        builder.Ignore(x => x.DomainEvents);
        builder.Property<long>(VersionProperty).HasColumnName("version").IsConcurrencyToken();
        return builder;
    }

    /// <summary>Đặt tên bảng/cột <c>snake_case</c> cho mọi entity chưa đặt tên tường minh; gọi cuối <c>OnModelCreating</c>.</summary>
    /// <param name="modelBuilder">Model builder.</param>
    /// <returns><paramref name="modelBuilder"/>.</returns>
    public static ModelBuilder UseSnakeCaseNames(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.GetTableName() is { } table && table == entity.GetDefaultTableName()) entity.SetTableName(SnakeCase(table));
            foreach (var property in entity.GetProperties())
                if (property.GetColumnName() == property.Name) property.SetColumnName(SnakeCase(property.Name));
        }
        return modelBuilder;
    }

    static string SnakeCase(string name) => UpperAfterLower().Replace(name, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex UpperAfterLower();
}
