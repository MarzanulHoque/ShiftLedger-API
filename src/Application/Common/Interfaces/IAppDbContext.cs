using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Common.Interfaces;

// Persistence contract for the Application layer. A DbSet<T> is added per entity as phases introduce them.
// Implemented by AppDbContext (Infrastructure).
public interface IAppDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OrgSettings> OrgSettings { get; }
    DbSet<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
