using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Models;

namespace ShiftLedger.Application.Bills;

// Admin list of bills (filter by paid/unpaid), each with its computed total (Rule B2).
public record BillSummaryDto(Guid Id, int BillNumber, Guid ServiceJobId, bool IsPaid, DateTime? PaidAtUtc, decimal Total);

public record GetBillsQuery(bool? IsPaid, int? Page = null, int? PageSize = null)
    : IRequest<PagedResult<BillSummaryDto>>;

public class GetBillsQueryHandler(IAppDbContext db) : IRequestHandler<GetBillsQuery, PagedResult<BillSummaryDto>>
{
    public async Task<PagedResult<BillSummaryDto>> Handle(GetBillsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Bills.AsNoTracking();
        if (request.IsPaid is { } isPaid)
        {
            query = query.Where(b => b.IsPaid == isPaid);
        }

        // The total is a live aggregate over the line items — stored nowhere (Rules B2/C2).
        return await query
            .OrderByDescending(b => b.Id) // UUIDv7: newest first
            .Select(b => new BillSummaryDto(
                b.Id, b.BillNumber, b.ServiceJobId, b.IsPaid, b.PaidAtUtc,
                db.BillLineItems.Where(l => l.BillId == b.Id)
                    .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m))
            .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
    }
}
