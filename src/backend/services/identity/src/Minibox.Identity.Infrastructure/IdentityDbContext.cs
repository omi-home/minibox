using Microsoft.EntityFrameworkCore;
using Minibox.BuildingBlocks.Infrastructure.Persistence;
using Minibox.Identity.Domain.Accounts;
using Minibox.Identity.Infrastructure.Accounts;
using Wolverine.EntityFrameworkCore;

namespace Minibox.Identity.Infrastructure;

/// <summary>DbContext của service identity (schema <c>identity</c>); mapping khớp DDL ở <c>database/schemas/identity/</c>.</summary>
/// <param name="options">Tuỳ chọn EF Core (Npgsql, role app).</param>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    /// <summary>Schema của service.</summary>
    public const string Schema = "identity";

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Account>(e =>
        {
            e.IsAggregateRoot();
            e.Property(x => x.Kind).HasConversion(k => AccountRepository.KindText(k), s => AccountRepository.ParseKind(s));
            e.Property(x => x.Email);
            e.Property(x => x.Username);
            e.Property(x => x.PasswordHash);
        });
        modelBuilder.MapWolverineEnvelopeStorage(Schema);
        modelBuilder.UseSnakeCaseNames();
    }
}
