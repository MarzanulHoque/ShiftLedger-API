using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Jobs;

// Assign or reassign a job's mechanic (Admin). Rule J2: the assignee must be an Employee-role user.
public record AssignMechanicCommand(Guid Id, Guid MechanicId) : IRequest;

public class AssignMechanicCommandHandler(IAppDbContext db, INotifier notifier, ICurrentUser currentUser) : IRequestHandler<AssignMechanicCommand>
{
    public async Task Handle(AssignMechanicCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule RB3/RB4: a DepartmentAdmin cannot assign within another department's job. SuperAdmin bypasses (RB0).
        if (!currentUser.IsSuperAdmin && job.DepartmentId != currentUser.DepartmentId)
        {
            throw new NotFoundException("Service job not found.");
        }

        await CreateJobCommandHandler.EnsureMechanicAsync(db, request.MechanicId, job.DepartmentId, cancellationToken);

        var isReassignment = job.AssignedMechanicId != request.MechanicId;
        job.AssignedMechanicId = request.MechanicId;
        await db.SaveChangesAsync(cancellationToken);

        if (isReassignment)
        {
            await notifier.NotifyAsync(request.MechanicId, "JobAssigned", $"Job assigned to you: {job.Title}", cancellationToken);
        }
    }
}
