using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Bills;

// Rule B4: marking a bill Paid/Unpaid is an Admin (owner) action; Paid stamps PaidAtUtc.
// Flipping back to unpaid ("reopen") clears the stamp and unlocks edits (Rule B3 correction path).
public record SetBillPaidCommand(Guid BillId, bool IsPaid) : IRequest;

public class SetBillPaidCommandHandler(IAppDbContext db, TimeProvider timeProvider, INotifier notifier, ICurrentUser currentUser)
    : IRequestHandler<SetBillPaidCommand>
{
    public async Task Handle(SetBillPaidCommand request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Bill not found.");

        await BillGuards.EnsureDepartmentAccessAsync(db, currentUser, bill.ServiceJobId, cancellationToken); // Rule RB3/RB4

        if (bill.IsPaid == request.IsPaid)
        {
            return; // idempotent — nothing to change or audit
        }

        if (request.IsPaid && !await db.BillLineItems.AnyAsync(l => l.BillId == bill.Id, cancellationToken))
        {
            throw new BusinessRuleException("An empty bill cannot be marked paid — add at least one line item.");
        }

        bill.IsPaid = request.IsPaid;
        bill.PaidAtUtc = request.IsPaid ? timeProvider.GetUtcNow().UtcDateTime : null;
        await db.SaveChangesAsync(cancellationToken);

        if (request.IsPaid)
        {
            var job = await db.ServiceJobs.AsNoTracking()
                .Where(j => j.Id == bill.ServiceJobId)
                .Select(j => new { j.Title, j.AssignedMechanicId, j.DepartmentId })
                .FirstOrDefaultAsync(cancellationToken);
            if (job?.AssignedMechanicId is { } mechanic)
            {
                await notifier.NotifyAsync(mechanic, "BillPaid",
                    $"Bill for job '{job.Title}' was marked paid.", cancellationToken);
            }

            // Rule N1: revenue event — the acting department's admin(s) and the org-wide
            // SuperAdmin cockpit, regardless of whether the job has an assigned mechanic.
            if (job is not null)
            {
                await notifier.NotifyDepartmentAsync(job.DepartmentId, "BillPaid",
                    $"Bill for job '{job.Title}' was marked paid.", cancellationToken);
            }
        }
    }
}
