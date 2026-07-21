using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Jobs;

public record GetJobQuery(Guid Id) : IRequest<JobDto>;

public class GetJobQueryHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetJobQuery, JobDto>
{
    public async Task<JobDto> Handle(GetJobQuery request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule R2/R3: a mechanic can only open a job assigned to them.
        if (!currentUser.IsAdmin && job.AssignedMechanicId != currentUser.UserId)
        {
            throw new ForbiddenException();
        }

        return new JobDto(
            job.Id, job.JobNumber, job.DepartmentId, job.Title, job.Description, job.BikeModel, job.Status,
            job.Priority, job.AssignedMechanicId, job.ReceivedDate, job.DueDate);
    }
}
