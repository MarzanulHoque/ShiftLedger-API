using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Dashboards;

// The owner's day view. Every number is a live aggregate over the source tables (Rules C2/C3) —
// nothing here is cached or stored. "Today" is the caller's local calendar date, passed by the
// client (T8); it defaults to the UTC date when omitted.
public record StatusCountDto(JobStatus Status, int Count);
public record MechanicWorkloadDto(Guid MechanicId, string MechanicName, int OpenJobs);
public record AdminDashboardDto(
    DateOnly Date,
    int JobsReceivedToday,
    IReadOnlyList<StatusCountDto> JobsByStatus,
    IReadOnlyList<MechanicWorkloadDto> MechanicWorkload,
    int UnpaidBills,
    decimal UnpaidTotal,
    int BillsPaidToday,
    decimal RevenueToday);

public record GetAdminDashboardQuery(DateOnly? Date) : IRequest<AdminDashboardDto>;

// Rule RB3/RB4: a DepartmentAdmin's dashboard is scoped to their own department; SuperAdmin sees
// the org-wide consolidated view (RB0) — same explicit-check pattern as every other P9/P10 handler
// (no global EF filter — see AppDbContext.OnModelCreating for why that pattern is unsafe).
public class GetAdminDashboardQueryHandler(IAppDbContext db, TimeProvider timeProvider, ICurrentUser currentUser)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var today = request.Date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var dayStartUtc = today.ToDateTime(TimeOnly.MinValue);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var jobs = db.ServiceJobs.AsNoTracking();
        if (!currentUser.IsSuperAdmin)
        {
            jobs = jobs.Where(j => j.DepartmentId == currentUser.DepartmentId);
        }

        var jobsReceivedToday = await jobs.CountAsync(j => j.ReceivedDate == today, cancellationToken);

        var jobsByStatus = await jobs
            .GroupBy(j => j.Status)
            .Select(g => new StatusCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        // Open workload per mechanic (Delivered = out the door, no longer workload).
        var workloadCounts = await jobs
            .Where(j => j.AssignedMechanicId != null && j.Status != JobStatus.Delivered)
            .GroupBy(j => j.AssignedMechanicId!.Value)
            .Select(g => new { MechanicId = g.Key, OpenJobs = g.Count() })
            .ToListAsync(cancellationToken);
        var mechanicIds = workloadCounts.Select(w => w.MechanicId).ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => mechanicIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);
        var workload = workloadCounts
            .Select(w => new MechanicWorkloadDto(w.MechanicId, names.GetValueOrDefault(w.MechanicId, "Unknown"), w.OpenJobs))
            .OrderByDescending(w => w.OpenJobs)
            .ToList();

        // Rule RB3/RB4: Bill has no department of its own — scope via an explicit join through
        // ServiceJobs, matching GetBills/GetBillingSummary (not a global EF filter).
        var bills =
            from b in db.Bills.AsNoTracking()
            join j in db.ServiceJobs.AsNoTracking() on b.ServiceJobId equals j.Id
            select new { Bill = b, j.DepartmentId };
        if (!currentUser.IsSuperAdmin)
        {
            bills = bills.Where(x => x.DepartmentId == currentUser.DepartmentId);
        }

        var unpaidBills = await bills.CountAsync(x => !x.Bill.IsPaid, cancellationToken);
        var unpaidTotal = await bills
            .Where(x => !x.Bill.IsPaid)
            .Select(x => db.BillLineItems.Where(l => l.BillId == x.Bill.Id)
                .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m)
            .SumAsync(cancellationToken);

        var billsPaidToday = await bills
            .CountAsync(x => x.Bill.IsPaid && x.Bill.PaidAtUtc >= dayStartUtc && x.Bill.PaidAtUtc < dayEndUtc, cancellationToken);
        var revenueToday = await bills
            .Where(x => x.Bill.IsPaid && x.Bill.PaidAtUtc >= dayStartUtc && x.Bill.PaidAtUtc < dayEndUtc)
            .Select(x => db.BillLineItems.Where(l => l.BillId == x.Bill.Id)
                .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m)
            .SumAsync(cancellationToken);

        return new AdminDashboardDto(
            today, jobsReceivedToday, jobsByStatus, workload,
            unpaidBills, unpaidTotal, billsPaidToday, revenueToday);
    }
}
