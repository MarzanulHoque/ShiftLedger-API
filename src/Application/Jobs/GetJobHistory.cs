using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Jobs;

// A job's audit trail (Rule A1): the append-only AuditLog rows for this ServiceJob, oldest first.
public record JobHistoryEntryDto(
    string Action, Guid? ChangedById, DateTime ChangedAtUtc, string? OldValuesJson, string? NewValuesJson);

public record GetJobHistoryQuery(Guid JobId) : IRequest<IReadOnlyList<JobHistoryEntryDto>>;

public class GetJobHistoryQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetJobHistoryQuery, IReadOnlyList<JobHistoryEntryDto>>
{
    public async Task<IReadOnlyList<JobHistoryEntryDto>> Handle(GetJobHistoryQuery request, CancellationToken cancellationToken)
    {
        // Rule RB3: AuditLog carries no department of its own, so confirm the job is still
        // reachable — and in the caller's department — before returning its history. A job outside
        // the caller's department reads as "not found", same as GetJob. SuperAdmin bypasses (RB0).
        var job = await db.ServiceJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");
        if (!currentUser.IsSuperAdmin && job.DepartmentId != currentUser.DepartmentId)
        {
            throw new NotFoundException("Service job not found.");
        }

        var entityName = nameof(ServiceJob);
        var entityId = request.JobId.ToString();

        return await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderBy(a => a.ChangedAtUtc)
            .Select(a => new JobHistoryEntryDto(a.Action, a.ChangedById, a.ChangedAtUtc, a.OldValuesJson, a.NewValuesJson))
            .ToListAsync(cancellationToken);
    }
}
