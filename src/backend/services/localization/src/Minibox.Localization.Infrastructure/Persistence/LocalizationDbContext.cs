using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;

namespace Minibox.Localization.Infrastructure.Persistence;

/// <summary>DbContext của service localization (schema <c>localization</c>); mapping khớp DDL ở <c>database/schemas/localization/</c>.</summary>
/// <remarks>Gồm bảng outbox/inbox Wolverine (<c>MapWolverineEnvelopeStorage</c>, DDL ở migration <c>0004_wolverine.sql</c>) để outbox ghi cùng transaction.</remarks>
/// <param name="options">Tuỳ chọn EF Core (Npgsql, role app).</param>
public sealed class LocalizationDbContext(DbContextOptions<LocalizationDbContext> options) : DbContext(options)
{
    /// <summary>Schema của service.</summary>
    public const string Schema = "localization";

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapWolverineEnvelopeStorage(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LocalizationDbContext).Assembly);
        modelBuilder.UseSnakeCaseNames();
    }
}
