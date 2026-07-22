using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Jobs;

// Advance (or, for an Admin, correct) a job's status. Rule J1 governs which moves are legal.
public record ChangeJobStatusCommand(Guid Id, JobStatus NewStatus) : IRequest;

public class ChangeJobStatusCommandHandler(IAppDbContext db, ICurrentUser currentUser, INotifier notifier)
    : IRequestHandler<ChangeJobStatusCommand>
{
    public async Task Handle(ChangeJobStatusCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule RB3/RB4: a job outside the caller's department reads as "not found". SuperAdmin bypasses (RB0).
        if (!currentUser.IsSuperAdmin && job.DepartmentId != currentUser.DepartmentId)
        {
            throw new NotFoundException("Service job not found.");
        }

        // Rule R2/R3: only an Admin or the assigned mechanic may move a job.
        if (!currentUser.IsAdmin && job.AssignedMechanicId != currentUser.UserId)
        {
            throw new ForbiddenException();
        }

        // Rule J1: reject illegal transitions (skips/jumps; back-steps unless Admin).
        if (!JobStatusFlow.CanTransition(job.Status, request.NewStatus, currentUser.IsAdmin))
        {
            throw new BusinessRuleException($"Cannot change status from {job.Status} to {request.NewStatus}.");
        }

        job.Status = request.NewStatus;
        await db.SaveChangesAsync(cancellationToken);

        // Notify the assigned mechanic when someone else (the owner) moved their job. The owner
        // watches the dashboard, so mechanic-made changes raise no notification in v1.
        if (job.AssignedMechanicId is { } mechanic && mechanic != currentUser.UserId)
        {
            await notifier.NotifyAsync(mechanic, "JobStatusChanged",
                $"Job '{job.Title}' moved to {request.NewStatus}.", cancellationToken);
        }
    }
}
