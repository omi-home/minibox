using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;

namespace Minibox.ServiceName.Infrastructure.Persistence;

/// <summary>DbContext của service servicename (schema <c>servicename</c>); mapping khớp DDL ở <c>database/schemas/servicename/</c>.</summary>
/// <remarks>Khi bật outbox Wolverine: gọi thêm <c>modelBuilder.MapWolverineEnvelopeStorage(Schema)</c> và đăng ký bằng
/// <see cref="PersistenceSetup.AddMiniboxPersistence{TDb}"/> thay cho <c>AddDbContext</c>.</remarks>
/// <param name="options">Tuỳ chọn EF Core (Npgsql, role app).</param>
public sealed class ServiceNameDbContext(DbContextOptions<ServiceNameDbContext> options) : DbContext(options)
{
    /// <summary>Schema của service.</summary>
    public const string Schema = "servicename";

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceNameDbContext).Assembly);
        modelBuilder.UseSnakeCaseNames();
    }
}
