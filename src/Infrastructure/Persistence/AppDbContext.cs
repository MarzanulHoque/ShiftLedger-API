using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Common;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider)
    : DbContext(options), IAppDbContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges()
    {
        CaptureChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        CaptureChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void CaptureChanges()
    {
        RegenerateConcurrencyTokens();
        WriteAuditLogs();
    }

    // Rule C1: bump the concurrency token on every modified entity so a stale update fails (409).
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

    // Rules A1/A2: append one immutable AuditLog row per changed entity, saved in the same transaction.
    private void WriteAuditLogs()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var logs = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            var (action, oldValues, newValues) = entry.State switch
            {
                EntityState.Added => ("Created", null, CurrentValues(entry)),
                EntityState.Deleted => ("Deleted", OriginalValues(entry), (Dictionary<string, object?>?)null),
                EntityState.Modified => ("Modified", ModifiedOriginals(entry), ModifiedCurrents(entry)),
                _ => (null, null, null),
            };
            if (action is null) continue;

            logs.Add(new AuditLog
            {
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = entry.Entity.Id.ToString(),
                Action = action,
                ChangedById = null, // populated from the current user once auth exists (P2+)
                ChangedAtUtc = now,
                OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            });
        }

        if (logs.Count > 0)
        {
            Set<AuditLog>().AddRange(logs);
        }
    }

    // RowVersion is excluded from audit values — its churn is noise, not a business change.
    private static bool IsAuditable(string propertyName) => propertyName != nameof(BaseEntity.RowVersion);

    private static Dictionary<string, object?> CurrentValues(EntityEntry entry) =>
        entry.CurrentValues.Properties.Where(p => IsAuditable(p.Name))
             .ToDictionary(p => p.Name, p => entry.CurrentValues[p]);

    private static Dictionary<string, object?> OriginalValues(EntityEntry entry) =>
        entry.OriginalValues.Properties.Where(p => IsAuditable(p.Name))
             .ToDictionary(p => p.Name, p => entry.OriginalValues[p]);

    private static Dictionary<string, object?> ModifiedCurrents(EntityEntry entry) =>
        entry.Properties.Where(p => p.IsModified && IsAuditable(p.Metadata.Name))
             .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

    private static Dictionary<string, object?> ModifiedOriginals(EntityEntry entry) =>
        entry.Properties.Where(p => p.IsModified && IsAuditable(p.Metadata.Name))
             .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
