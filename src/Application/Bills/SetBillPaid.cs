using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Bills;

// Rule B4: marking a bill Paid/Unpaid is an Admin (owner) action; Paid stamps PaidAtUtc.
// Flipping back to unpaid ("reopen") clears the stamp and unlocks edits (Rule B3 correction path).
public record SetBillPaidCommand(Guid BillId, bool IsPaid) : IRequest;

public class SetBillPaidCommandHandler(IAppDbContext db, TimeProvider timeProvider)
    : IRequestHandler<SetBillPaidCommand>
{
    public async Task Handle(SetBillPaidCommand request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Bill not found.");

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
    }
}
