using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Common.Interfaces;

// Persistence contract for the Application layer. A DbSet<T> is added per entity as phases introduce them.
// Implemented by AppDbContext (Infrastructure).
public interface IAppDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ShiftLedger.Domain.Entities.OrgSettings> OrgSettings { get; }
    DbSet<User> Users { get; }
    DbSet<Department> Departments { get; }
    DbSet<ServiceJob> ServiceJobs { get; }
    DbSet<JobComment> JobComments { get; }
    DbSet<Bill> Bills { get; }
    DbSet<BillLineItem> BillLineItems { get; }
    DbSet<EmployeeProfile> EmployeeProfiles { get; }
    DbSet<PayRate> PayRates { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
