using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;

namespace Minibox.Catalog.Infrastructure.Persistence;

/// <summary>DbContext của service catalog (schema <c>catalog</c>); mapping khớp DDL ở <c>database/schemas/catalog/</c>.</summary>
/// <remarks>Gồm bảng outbox/inbox Wolverine (<c>MapWolverineEnvelopeStorage</c>, DDL ở migration <c>0003_wolverine.sql</c>) để outbox ghi cùng transaction.</remarks>
/// <param name="options">Tuỳ chọn EF Core (Npgsql, role app).</param>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    /// <summary>Schema của service.</summary>
    public const string Schema = "catalog";

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapWolverineEnvelopeStorage(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
        modelBuilder.UseSnakeCaseNames();
    }
}
