using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Jobs;

public record JobCommentDto(Guid Id, Guid AuthorId, string Body, DateTime CreatedAtUtc);

public record GetJobCommentsQuery(Guid JobId) : IRequest<IReadOnlyList<JobCommentDto>>;

public class GetJobCommentsQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetJobCommentsQuery, IReadOnlyList<JobCommentDto>>
{
    public async Task<IReadOnlyList<JobCommentDto>> Handle(GetJobCommentsQuery request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule R2/R3: only an Admin or the assigned mechanic may read a job's comments.
        if (!currentUser.IsAdmin && job.AssignedMechanicId != currentUser.UserId)
        {
            throw new ForbiddenException();
        }

        return await db.JobComments.AsNoTracking()
            .Where(c => c.ServiceJobId == request.JobId)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new JobCommentDto(c.Id, c.AuthorId, c.Body, c.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
