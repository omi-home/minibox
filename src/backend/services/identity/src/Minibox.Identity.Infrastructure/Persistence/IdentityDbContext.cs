using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;

namespace Minibox.Identity.Infrastructure.Persistence;

/// <summary>DbContext của service identity (schema <c>identity</c>); mapping khớp DDL ở <c>database/schemas/identity/</c>.</summary>
/// <remarks>Gồm bảng outbox/inbox Wolverine (<c>MapWolverineEnvelopeStorage</c>) để outbox ghi cùng transaction.</remarks>
/// <param name="options">Tuỳ chọn EF Core (Npgsql, role app).</param>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    /// <summary>Schema của service.</summary>
    public const string Schema = "identity";

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.MapWolverineEnvelopeStorage(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        modelBuilder.UseSnakeCaseNames();
    }
}
