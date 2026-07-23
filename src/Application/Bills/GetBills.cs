using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Common.Models;

namespace ShiftLedger.Application.Bills;

// Admin list of bills (filter by paid/unpaid/department), each with its computed total (Rule B2).
public record BillSummaryDto(Guid Id, int BillNumber, Guid ServiceJobId, Guid DepartmentId, bool IsPaid, DateTime? PaidAtUtc, decimal Total);

// Rule BL1/BL2: the standalone billing section's list, independent of any single job.
public record GetBillsQuery(bool? IsPaid, Guid? DepartmentId, int? Page = null, int? PageSize = null)
    : IRequest<PagedResult<BillSummaryDto>>;

public class GetBillsQueryHandler(IAppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetBillsQuery, PagedResult<BillSummaryDto>>
{
    public async Task<PagedResult<BillSummaryDto>> Handle(GetBillsQuery request, CancellationToken cancellationToken)
    {
        // Rule RB3/RB4: Bill has no department of its own (this codebase never adds navigation
        // properties — see P8/P9 notes), so department comes from an explicit join through
        // ServiceJobs, NOT a global EF filter (see AppDbContext.OnModelCreating for why that
        // pattern is unsafe).
        var query =
            from b in db.Bills.AsNoTracking()
            join j in db.ServiceJobs.AsNoTracking() on b.ServiceJobId equals j.Id
            select new { Bill = b, j.DepartmentId };

        // Rule RB3/RB4: a DepartmentAdmin only ever sees their own department's bills; SuperAdmin
        // sees every department (RB0), optionally narrowed by the explicit filter below (Rule BL2).
        if (!currentUser.IsSuperAdmin)
        {
            query = query.Where(x => x.DepartmentId == currentUser.DepartmentId);
        }

        if (request.DepartmentId is { } departmentId)
        {
            query = query.Where(x => x.DepartmentId == departmentId);
        }

        if (request.IsPaid is { } isPaid)
        {
            query = query.Where(x => x.Bill.IsPaid == isPaid);
        }

        // The total is a live aggregate over the line items — stored nowhere (Rules B2/C2).
        return await query
            .OrderByDescending(x => x.Bill.Id) // UUIDv7: newest first
            .Select(x => new BillSummaryDto(
                x.Bill.Id, x.Bill.BillNumber, x.Bill.ServiceJobId, x.DepartmentId, x.Bill.IsPaid, x.Bill.PaidAtUtc,
                db.BillLineItems.Where(l => l.BillId == x.Bill.Id)
                    .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m))
            .ToPagedResultAsync(request.Page, request.PageSize, cancellationToken);
    }
}
