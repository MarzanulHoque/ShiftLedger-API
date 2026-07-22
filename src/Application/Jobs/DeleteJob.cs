using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Jobs;

public record DeleteJobCommand(Guid Id) : IRequest;

public class DeleteJobCommandHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<DeleteJobCommand>
{
    public async Task Handle(DeleteJobCommand request, CancellationToken cancellationToken)
    {
        var job = await db.ServiceJobs.FirstOrDefaultAsync(j => j.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule RB3/RB4: a DepartmentAdmin cannot delete another department's job. SuperAdmin bypasses (RB0).
        if (!currentUser.IsSuperAdmin && job.DepartmentId != currentUser.DepartmentId)
        {
            throw new NotFoundException("Service job not found.");
        }

        // A paid bill is an immutable financial record (Rule B3/immutability) — the job it
        // documents must stay resolvable (history, invoice re-download), so deletion is blocked
        // outright rather than leaving a dangling reference. An unpaid *draft* bill isn't a
        // settled financial record yet, so it's safe to cascade away with the job instead of
        // orphaning it — either path keeps every bill's job reference resolvable, closing the
        // gap that previously let a bill outlive its job.
        var bill = await db.Bills.FirstOrDefaultAsync(b => b.ServiceJobId == job.Id, cancellationToken);
        if (bill is not null)
        {
            if (bill.IsPaid)
            {
                throw new BusinessRuleException(
                    "This job has a paid bill and cannot be deleted — paid bills are immutable financial records.");
            }

            db.Bills.Remove(bill); // soft-deleted via the SaveChanges interceptor, same as ServiceJob below
        }

        // Rule J4: Remove maps to a soft delete via the SaveChanges interceptor (ServiceJob is ISoftDeletable).
        // Both removals above are staged on the same context and committed in the one
        // SaveChangesAsync call below, so they succeed or fail together — no partial state
        // where the bill is gone but the job remains, or vice versa.
        db.ServiceJobs.Remove(job);
        await db.SaveChangesAsync(cancellationToken);
    }
}
