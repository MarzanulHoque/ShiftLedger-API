using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Jobs;

public record DeleteJobCommand(Guid Id) : IRequest;

public class DeleteJobCommandHandler(IAppDbContext db) : IRequestHandler<DeleteJobCommand>
{
    public async Task Handle(DeleteJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule J4: Remove maps to a soft delete via the SaveChanges interceptor (ServiceJob is ISoftDeletable).
        db.ServiceJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
    }
}
