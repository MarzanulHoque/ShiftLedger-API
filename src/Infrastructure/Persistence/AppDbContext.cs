using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Picks up IEntityTypeConfiguration<T> classes as entities are added per phase.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
