using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;

namespace ShiftLedger.Application.Bills;

// Rule BL2: per-department roll-up for the standalone billing section. Grouped from the exact
// same per-bill totals GetBills uses, so a SuperAdmin's consolidated total can never drift from
// the sum of these rows (Rule C2) — there is no separate "consolidated" computation.
public record DepartmentBillingSummaryDto(
    Guid DepartmentId, string DepartmentName, int TotalCount,
    int UnpaidCount, decimal UnpaidTotal, int PaidCount, decimal PaidTotal, decimal GrandTotal);

public record GetBillingSummaryQuery : IRequest<IReadOnlyList<DepartmentBillingSummaryDto>>;

public class GetBillingSummaryQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetBillingSummaryQuery, IReadOnlyList<DepartmentBillingSummaryDto>>
{
    public async Task<IReadOnlyList<DepartmentBillingSummaryDto>> Handle(
        GetBillingSummaryQuery request, CancellationToken cancellationToken)
    {
        var query =
            from b in db.Bills.AsNoTracking()
            join j in db.ServiceJobs.AsNoTracking() on b.ServiceJobId equals j.Id
            select new { b.Id, b.IsPaid, j.DepartmentId };

        // Rule RB3/RB4: a DepartmentAdmin only ever sees their own department's roll-up row;
        // SuperAdmin sees every department (RB0).
        if (!currentUser.IsSuperAdmin)
        {
            query = query.Where(x => x.DepartmentId == currentUser.DepartmentId);
        }

        // Grouped aggregation over a per-bill total subquery doesn't translate cleanly to SQL, so
        // pull the flat (department, paid, total) rows and group in memory — the shop's bill volume
        // makes this negligible, and it keeps the same per-bill total math GetBills uses (no drift).
        var flat = await query
            .Select(x => new
            {
                x.DepartmentId,
                x.IsPaid,
                Total = db.BillLineItems.Where(l => l.BillId == x.Id)
                    .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        var departmentNames = await db.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        return flat
            .GroupBy(x => x.DepartmentId)
            .Select(g => new DepartmentBillingSummaryDto(
                g.Key,
                departmentNames.GetValueOrDefault(g.Key, "Unknown"),
                g.Count(),
                g.Count(x => !x.IsPaid),
                g.Where(x => !x.IsPaid).Sum(x => x.Total),
                g.Count(x => x.IsPaid),
                g.Where(x => x.IsPaid).Sum(x => x.Total),
                g.Sum(x => x.Total)))
            .OrderBy(d => d.DepartmentName)
            .ToList();
    }
}
