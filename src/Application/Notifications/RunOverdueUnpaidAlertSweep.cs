using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Notifications;

// Rule N3: a periodic sweep (invoked by OverdueUnpaidAlertHostedService) that raises a
// department + org-wide alert for jobs past their due date and bills unpaid beyond
// OrgSettings.UnpaidAlertDays. LastOverdueAlertAtUtc/LastUnpaidAlertAtUtc guard against
// re-raising the same alert on every tick — at most once per calendar day per job/bill.
public record RunOverdueUnpaidAlertSweepCommand : IRequest;

public class RunOverdueUnpaidAlertSweepCommandHandler(IAppDbContext db, INotifier notifier, TimeProvider timeProvider)
    : IRequestHandler<RunOverdueUnpaidAlertSweepCommand>
{
    public async Task Handle(RunOverdueUnpaidAlertSweepCommand request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var todayStartUtc = today.ToDateTime(TimeOnly.MinValue);

        var overdueJobs = await db.ServiceJobs
            .Where(j => j.DueDate != null && j.DueDate < today && j.Status != JobStatus.Delivered)
            .Where(j => j.LastOverdueAlertAtUtc == null || j.LastOverdueAlertAtUtc < todayStartUtc)
            .ToListAsync(cancellationToken);

        var orgSettings = await db.OrgSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var thresholdDays = orgSettings?.UnpaidAlertDays ?? 7;

        var unpaidCandidates = await (
            from b in db.Bills
            join j in db.ServiceJobs on b.ServiceJobId equals j.Id
            where !b.IsPaid && (b.LastUnpaidAlertAtUtc == null || b.LastUnpaidAlertAtUtc < todayStartUtc)
            select new { Bill = b, j.DepartmentId, j.Title }
        ).ToListAsync(cancellationToken);
        var unpaidBills = unpaidCandidates.Where(x => (now - x.Bill.CreatedAtUtc).TotalDays >= thresholdDays).ToList();

        // Stamp + save before notifying (Rule: never push for a change that failed to commit —
        // see INotifier's doc comment).
        foreach (var job in overdueJobs) job.LastOverdueAlertAtUtc = now;
        foreach (var row in unpaidBills) row.Bill.LastUnpaidAlertAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var job in overdueJobs)
        {
            await notifier.NotifyDepartmentAsync(job.DepartmentId, "JobOverdue", $"Job '{job.Title}' is overdue.", cancellationToken);
        }

        foreach (var row in unpaidBills)
        {
            await notifier.NotifyDepartmentAsync(row.DepartmentId, "BillUnpaid", $"Bill for job '{row.Title}' is still unpaid.", cancellationToken);
        }
    }
}
