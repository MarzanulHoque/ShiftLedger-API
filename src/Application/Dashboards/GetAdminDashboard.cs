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

public class GetAdminDashboardQueryHandler(IAppDbContext db, TimeProvider timeProvider)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var today = request.Date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var dayStartUtc = today.ToDateTime(TimeOnly.MinValue);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var jobsReceivedToday = await db.ServiceJobs.CountAsync(j => j.ReceivedDate == today, cancellationToken);

        var jobsByStatus = await db.ServiceJobs
            .GroupBy(j => j.Status)
            .Select(g => new StatusCountDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        // Open workload per mechanic (Delivered = out the door, no longer workload).
        var workloadCounts = await db.ServiceJobs
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

        var unpaidBills = await db.Bills.CountAsync(b => !b.IsPaid, cancellationToken);
        var unpaidTotal = await db.BillLineItems
            .Where(l => db.Bills.Any(b => b.Id == l.BillId && !b.IsPaid))
            .SumAsync(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2), cancellationToken) ?? 0m;

        var billsPaidToday = await db.Bills
            .CountAsync(b => b.IsPaid && b.PaidAtUtc >= dayStartUtc && b.PaidAtUtc < dayEndUtc, cancellationToken);
        var revenueToday = await db.BillLineItems
            .Where(l => db.Bills.Any(b =>
                b.Id == l.BillId && b.IsPaid && b.PaidAtUtc >= dayStartUtc && b.PaidAtUtc < dayEndUtc))
            .SumAsync(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2), cancellationToken) ?? 0m;

        return new AdminDashboardDto(
            today, jobsReceivedToday, jobsByStatus, workload,
            unpaidBills, unpaidTotal, billsPaidToday, revenueToday);
    }
}
