using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Dashboards;

// Rule C2/C3 (P12): per-department comparison for the SuperAdmin cockpit — grouped from the exact
// same live source-table reads GetAdminDashboardQuery uses, just bucketed by department instead of
// summed into one consolidated view (same relationship GetBillingSummaryQuery has to GetBills).
public record DepartmentDashboardMetricsDto(
    Guid DepartmentId, string DepartmentName,
    int JobsReceivedToday, int OpenJobs, int ThroughputLast7Days,
    int UnpaidBills, decimal UnpaidTotal, decimal RevenueToday);

public record GetDashboardComparisonQuery(DateOnly? Date) : IRequest<IReadOnlyList<DepartmentDashboardMetricsDto>>;

// Rule RB3/RB4: a DepartmentAdmin only ever gets their own department's row; SuperAdmin sees every
// department (RB0) — same explicit-check pattern as every other P9/P10/P12 handler.
public class GetDashboardComparisonQueryHandler(IAppDbContext db, TimeProvider timeProvider, ICurrentUser currentUser)
    : IRequestHandler<GetDashboardComparisonQuery, IReadOnlyList<DepartmentDashboardMetricsDto>>
{
    public async Task<IReadOnlyList<DepartmentDashboardMetricsDto>> Handle(
        GetDashboardComparisonQuery request, CancellationToken cancellationToken)
    {
        var today = request.Date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var dayStartUtc = today.ToDateTime(TimeOnly.MinValue);
        var dayEndUtc = dayStartUtc.AddDays(1);
        // Throughput has no dedicated "completed at" timestamp (no such column exists), so it's
        // approximated as jobs received in the last 7 days that have since reached Completed or
        // Delivered — a rolling recent-volume proxy rather than a precise completion-date metric.
        var sevenDaysAgo = today.AddDays(-6);

        var jobs = db.ServiceJobs.AsNoTracking();
        if (!currentUser.IsSuperAdmin)
        {
            jobs = jobs.Where(j => j.DepartmentId == currentUser.DepartmentId);
        }

        var jobRows = await jobs
            .Select(j => new { j.DepartmentId, j.ReceivedDate, j.Status })
            .ToListAsync(cancellationToken);

        // Rule RB3/RB4: Bill has no department of its own — scope via an explicit join through
        // ServiceJobs, matching GetBills/GetBillingSummary/GetAdminDashboard (not a global filter).
        var bills =
            from b in db.Bills.AsNoTracking()
            join j in db.ServiceJobs.AsNoTracking() on b.ServiceJobId equals j.Id
            select new { Bill = b, j.DepartmentId };
        if (!currentUser.IsSuperAdmin)
        {
            bills = bills.Where(x => x.DepartmentId == currentUser.DepartmentId);
        }

        var billRows = await bills
            .Select(x => new
            {
                x.DepartmentId,
                x.Bill.IsPaid,
                x.Bill.PaidAtUtc,
                Total = db.BillLineItems.Where(l => l.BillId == x.Bill.Id)
                    .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        var departmentNames = await db.Departments.AsNoTracking()
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        var departmentIds = jobRows.Select(j => j.DepartmentId)
            .Concat(billRows.Select(b => b.DepartmentId))
            .Distinct();

        return departmentIds
            .Select(deptId =>
            {
                var deptJobs = jobRows.Where(j => j.DepartmentId == deptId).ToList();
                var deptBills = billRows.Where(b => b.DepartmentId == deptId).ToList();
                return new DepartmentDashboardMetricsDto(
                    deptId,
                    departmentNames.GetValueOrDefault(deptId, "Unknown"),
                    deptJobs.Count(j => j.ReceivedDate == today),
                    deptJobs.Count(j => j.Status != JobStatus.Delivered),
                    deptJobs.Count(j => j.ReceivedDate >= sevenDaysAgo &&
                        (j.Status == JobStatus.Completed || j.Status == JobStatus.Delivered)),
                    deptBills.Count(b => !b.IsPaid),
                    deptBills.Where(b => !b.IsPaid).Sum(b => b.Total),
                    deptBills.Where(b => b.IsPaid && b.PaidAtUtc >= dayStartUtc && b.PaidAtUtc < dayEndUtc).Sum(b => b.Total));
            })
            .OrderBy(d => d.DepartmentName)
            .ToList();
    }
}
