using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Bills;

public record BillLineItemDto(
    Guid Id, LineItemType Type, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

// Total and line totals are computed on read (Rule B2), never persisted.
public record BillDto(
    Guid Id, int BillNumber, Guid ServiceJobId, bool IsPaid, DateTime? PaidAtUtc,
    IReadOnlyList<BillLineItemDto> Lines, decimal Total);

public record GetJobBillQuery(Guid ServiceJobId) : IRequest<BillDto>;

public class GetJobBillQueryHandler(IAppDbContext db) : IRequestHandler<GetJobBillQuery, BillDto>
{
    public async Task<BillDto> Handle(GetJobBillQuery request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking()
                .FirstOrDefaultAsync(b => b.ServiceJobId == request.ServiceJobId, cancellationToken)
            ?? throw new NotFoundException("This job has no bill yet.");

        var lines = await db.BillLineItems.AsNoTracking()
            .Where(l => l.BillId == bill.Id)
            .OrderBy(l => l.Id) // UUIDv7 ids are time-ordered, so this is insertion order
            .ToListAsync(cancellationToken);

        var lineDtos = lines
            .Select(l => new BillLineItemDto(
                l.Id, l.Type, l.Description, l.Quantity, l.UnitPrice, BillMath.LineTotal(l.Quantity, l.UnitPrice)))
            .ToList();

        return new BillDto(bill.Id, bill.BillNumber, bill.ServiceJobId, bill.IsPaid, bill.PaidAtUtc, lineDtos, BillMath.Total(lines));
    }
}
