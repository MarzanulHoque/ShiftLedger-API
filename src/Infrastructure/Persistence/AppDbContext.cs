using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Common;

namespace ShiftLedger.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public override int SaveChanges()
    {
        RegenerateConcurrencyTokens();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RegenerateConcurrencyTokens();
        return base.SaveChangesAsync(cancellationToken);
    }

    // Rule C1: bump the concurrency token on every modified entity so a stale update fails (409).
    // RowVersion is marked IsConcurrencyToken in the global EF config (P1-7).
    private void RegenerateConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid();
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up IEntityTypeConfiguration<T> classes as entities are added per phase.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
