using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Jobs;

public record AddJobCommentCommand(Guid JobId, string Body) : IRequest<Guid>;

public class AddJobCommentCommandValidator : AbstractValidator<AddJobCommentCommand>
{
    public AddJobCommentCommandValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
}

public class AddJobCommentCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider)
    : IRequestHandler<AddJobCommentCommand, Guid>
{
    public async Task<Guid> Handle(AddJobCommentCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.FirstOrDefaultAsync(j => j.Id == request.JobId, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule R2/R3: only an Admin or the assigned mechanic may comment on a job.
        if (!currentUser.IsAdmin && job.AssignedMechanicId != currentUser.UserId)
        {
            throw new ForbiddenException();
        }

        var comment = new JobComment
        {
            ServiceJobId = request.JobId,
            AuthorId = currentUser.UserId ?? throw new ForbiddenException(),
            Body = request.Body.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.JobComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
        return comment.Id;
    }
}
