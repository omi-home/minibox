using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;

namespace Minibox.ServiceName.Infrastructure.Persistence;

/// <summary>DbContext của service servicename (schema <c>servicename</c>); mapping khớp DDL ở <c>database/schemas/servicename/</c>.</summary>
/// <remarks>Gồm bảng outbox/inbox Wolverine (<c>MapWolverineEnvelopeStorage</c>, DDL ở migration <c>0004_wolverine.sql</c>) để outbox ghi cùng transaction.</remarks>
/// <param name="options">Tuỳ chọn EF Core (Npgsql, role app).</param>
public sealed class ServiceNameDbContext(DbContextOptions<ServiceNameDbContext> options) : DbContext(options)
{
    /// <summary>Schema của service.</summary>
    public const string Schema = "servicename";

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapWolverineEnvelopeStorage(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceNameDbContext).Assembly);
        modelBuilder.UseSnakeCaseNames();
    }
}
