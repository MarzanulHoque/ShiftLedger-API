using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Jobs;

// A job's audit trail (Rule A1): the append-only AuditLog rows for this ServiceJob, oldest first.
public record JobHistoryEntryDto(
    string Action, Guid? ChangedById, DateTime ChangedAtUtc, string? OldValuesJson, string? NewValuesJson);

public record GetJobHistoryQuery(Guid JobId) : IRequest<IReadOnlyList<JobHistoryEntryDto>>;

public class GetJobHistoryQueryHandler(IAppDbContext db)
    : IRequestHandler<GetJobHistoryQuery, IReadOnlyList<JobHistoryEntryDto>>
{
    public async Task<IReadOnlyList<JobHistoryEntryDto>> Handle(GetJobHistoryQuery request, CancellationToken cancellationToken)
    {
        var entityName = nameof(ServiceJob);
        var entityId = request.JobId.ToString();

        return await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == entityName && a.EntityId == entityId)
            .OrderBy(a => a.ChangedAtUtc)
            .Select(a => new JobHistoryEntryDto(a.Action, a.ChangedById, a.ChangedAtUtc, a.OldValuesJson, a.NewValuesJson))
            .ToListAsync(cancellationToken);
    }
}
