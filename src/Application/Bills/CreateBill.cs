using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Entities;

namespace ShiftLedger.Application.Bills;

// Create the bill for a job (Admin). Rule B1: a job has exactly one bill.
public record CreateBillCommand(Guid ServiceJobId) : IRequest<Guid>;

public class CreateBillCommandHandler(IAppDbContext db) : IRequestHandler<CreateBillCommand, Guid>
{
    public async Task<Guid> Handle(CreateBillCommand request, CancellationToken cancellationToken)
    {
        _ = await db.ServiceJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == request.ServiceJobId, cancellationToken)
            ?? throw new NotFoundException("Service job not found.");

        // Rule B1: reject a second bill (the unique ServiceJobId index is the DB-level backstop).
        if (await db.Bills.AnyAsync(b => b.ServiceJobId == request.ServiceJobId, cancellationToken))
        {
            throw new BusinessRuleException("This job already has a bill.");
        }

        var bill = new Bill { ServiceJobId = request.ServiceJobId };
        db.Bills.Add(bill);
        await db.SaveChangesAsync(cancellationToken);
        return bill.Id;
    }
}
