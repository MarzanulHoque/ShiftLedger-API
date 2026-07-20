using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Bills;

public record InvoiceLineDto(LineItemType Type, string Description, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public record InvoiceDto(
    Guid BillId,
    int BillNumber,
    int? JobNumber,
    string JobTitle,
    string BikeModel,
    string? MechanicName,
    DateOnly ReceivedDate,
    DateOnly? DueDate,
    IReadOnlyList<InvoiceLineDto> Lines,
    decimal Total,
    bool IsPaid,
    DateTime? PaidAtUtc,
    string CurrencyCode);

public record GetBillInvoiceQuery(Guid BillId) : IRequest<InvoiceDto>;

// An invoice is a snapshot of a Bill for handing to the customer — it must still render even if
// the job was later (soft-)deleted, since the bill itself is an immutable financial record that
// outlives the job's own lifecycle state. IgnoreQueryFilters() on the job lookup is deliberate.
public class GetBillInvoiceQueryHandler(IAppDbContext db) : IRequestHandler<GetBillInvoiceQuery, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(GetBillInvoiceQuery request, CancellationToken cancellationToken)
    {
        var bill = await db.Bills.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundException("Bill not found.");

        var lines = await db.BillLineItems.AsNoTracking()
            .Where(l => l.BillId == bill.Id)
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);

        var job = await db.ServiceJobs.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(j => j.Id == bill.ServiceJobId, cancellationToken);

        string? mechanicName = null;
        if (job?.AssignedMechanicId is { } mechanicId)
        {
            mechanicName = await db.Users.AsNoTracking()
                .Where(u => u.Id == mechanicId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var orgSettings = await db.OrgSettings.AsNoTracking().FirstAsync(cancellationToken);

        var lineDtos = lines
            .Select(l => new InvoiceLineDto(l.Type, l.Description, l.Quantity, l.UnitPrice, BillMath.LineTotal(l.Quantity, l.UnitPrice)))
            .ToList();

        return new InvoiceDto(
            bill.Id,
            bill.BillNumber,
            job?.JobNumber,
            job?.Title ?? "(deleted job)",
            job?.BikeModel ?? "—",
            mechanicName,
            job?.ReceivedDate ?? DateOnly.FromDateTime(bill.PaidAtUtc ?? DateTime.UtcNow),
            job?.DueDate,
            lineDtos,
            BillMath.Total(lines),
            bill.IsPaid,
            bill.PaidAtUtc,
            orgSettings.CurrencyCode);
    }
}
