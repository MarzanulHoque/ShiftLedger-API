using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Bills;

public record DeleteLineItemCommand(Guid BillId, Guid LineId) : IRequest;

public class DeleteLineItemCommandHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<DeleteLineItemCommand>
{
    public async Task Handle(DeleteLineItemCommand request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Bill not found.");

        await BillGuards.EnsureDepartmentAccessAsync(db, currentUser, bill.ServiceJobId, cancellationToken); // Rule RB3/RB4
        BillGuards.EnsureEditable(bill); // Rule B3

        var line = await db.BillLineItems
                .FirstOrDefaultAsync(l => l.Id == request.LineId && l.BillId == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Line item not found.");

        // Hard delete: a line on an unpaid bill is a draft, not history (locking starts at Paid — B3).
        db.BillLineItems.Remove(line);
        await db.SaveChangesAsync(cancellationToken);
    }
}
