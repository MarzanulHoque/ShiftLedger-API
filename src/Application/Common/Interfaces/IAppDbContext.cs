namespace ShiftLedger.Application.Common.Interfaces;

// Abstraction the Application layer uses to reach persistence without depending on EF Core directly.
// A DbSet<T> property is added here as each entity is introduced (P1-5 AuditLog, P1-8 OrgSettings, …).
// Implemented by AppDbContext in Infrastructure (P1-3).
public interface IAppDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
