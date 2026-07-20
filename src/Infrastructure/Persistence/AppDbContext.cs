using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Common;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Infrastructure.Persistence;

// currentUser is optional so the design-time / test constructions (new AppDbContext(options, clock))
// still work; when the DI container supplies it, audit rows are stamped with the acting user (A1).
public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider, ICurrentUser? currentUser = null)
    : DbContext(options), IAppDbContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OrgSettings> OrgSettings => Set<OrgSettings>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ServiceJob> ServiceJobs => Set<ServiceJob>();
    public DbSet<JobComment> JobComments => Set<JobComment>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillLineItem> BillLineItems => Set<BillLineItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<PayRate> PayRates => Set<PayRate>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

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
        EnforceAuditLogAppendOnly();    // A2 — reject any update/delete of an audit row
        WriteAuditLogs();               // A1/A2 — capture the semantic Added/Modified/Deleted first
        ApplySoftDeletes();             // convert Deleted -> Modified for ISoftDeletable
        RegenerateConcurrencyTokens();  // C1 — bump token on all Modified (incl. just-soft-deleted)
    }

    // Rule A2: AuditLog is append-only — no code path may ever update or delete a row.
    private void EnforceAuditLogAppendOnly()
    {
        foreach (var entry in ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("AuditLog is append-only (Rule A2) — rows are never updated or deleted.");
            }
        }
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

    // Soft delete: a deleted ISoftDeletable becomes an update that hides the row instead of removing it.
    private void ApplySoftDeletes()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAtUtc = now;
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
                ChangedById = currentUser?.UserId, // the acting user, from the JWT (Rule A1)
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

    // Excluded from audit values: RowVersion (churn is noise, not a business change) and secret
    // material — hashes never belong in audit JSON (docs/09: no secrets in logs).
    private static readonly string[] NonAuditableProperties =
        [nameof(BaseEntity.RowVersion), "PasswordHash", "TokenHash", "ReplacedByTokenHash"];

    private static bool IsAuditable(string propertyName) => !NonAuditableProperties.Contains(propertyName);

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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money: every decimal maps to DECIMAL(18,2). Never float/double.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Hide soft-deleted rows globally for every ISoftDeletable entity.
            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                ApplySoftDeleteFilterMethod.MakeGenericMethod(clrType).Invoke(null, [modelBuilder]);
            }

            // Rule C1: RowVersion is the optimistic-concurrency token on every BaseEntity.
            if (typeof(BaseEntity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Property(nameof(BaseEntity.RowVersion)).IsConcurrencyToken();
            }

            // Enums persist as their string name (VARCHAR), never MySQL's native ENUM.
            foreach (var property in entityType.GetProperties())
            {
                var propertyType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (propertyType.IsEnum)
                {
                    modelBuilder.Entity(clrType).Property(property.Name).HasConversion<string>().HasMaxLength(32);
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private static readonly MethodInfo ApplySoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }
}
