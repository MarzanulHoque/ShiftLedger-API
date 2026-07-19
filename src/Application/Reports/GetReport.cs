using MediatR;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Domain.Enums;

namespace ShiftLedger.Application.Reports;

// The five v1 report types (docs/04 §3).
public enum ReportType
{
    Jobs,
    Revenue,
    UnpaidBills,
    BillingHistory,
    MechanicProductivity,
}

// One tabular shape for every report: the UI renders it as a table, and the same data feeds the
// PDF/Excel exporters. Values stay typed (dates, decimals) so exporters can format them.
public record ReportData(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);

// All reports are live reads over the source tables (Rules C2/C3). Date filters are calendar
// dates (T9): jobs filter on ReceivedDate; money reports filter on the UTC date of PaidAtUtc.
public record GetReportQuery(ReportType Type, DateOnly? From, DateOnly? To, Guid? MechanicId, JobStatus? Status)
    : IRequest<ReportData>;

public class GetReportQueryHandler(IAppDbContext db) : IRequestHandler<GetReportQuery, ReportData>
{
    public async Task<ReportData> Handle(GetReportQuery request, CancellationToken ct) => request.Type switch
    {
        ReportType.Jobs => await JobsReportAsync(request, ct),
        ReportType.Revenue => await RevenueReportAsync(request, ct),
        ReportType.UnpaidBills => await UnpaidBillsReportAsync(ct),
        ReportType.BillingHistory => await BillingHistoryReportAsync(request, ct),
        ReportType.MechanicProductivity => await MechanicProductivityReportAsync(request, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(request.Type)),
    };

    private async Task<ReportData> JobsReportAsync(GetReportQuery request, CancellationToken ct)
    {
        var query = db.ServiceJobs.AsNoTracking();
        if (request.From is { } from) query = query.Where(j => j.ReceivedDate >= from);
        if (request.To is { } to) query = query.Where(j => j.ReceivedDate <= to);
        if (request.Status is { } status) query = query.Where(j => j.Status == status);
        if (request.MechanicId is { } mechanicId) query = query.Where(j => j.AssignedMechanicId == mechanicId);

        var jobs = await query.OrderBy(j => j.ReceivedDate)
            .Select(j => new { j.Title, j.BikeModel, j.Status, j.AssignedMechanicId, j.ReceivedDate, j.DueDate })
            .ToListAsync(ct);
        var names = await MechanicNamesAsync(jobs.Select(j => j.AssignedMechanicId), ct);

        return new ReportData("Jobs",
            ["Title", "Bike model", "Status", "Mechanic", "Received", "Due"],
            jobs.Select(j => (IReadOnlyList<object?>)
                [j.Title, j.BikeModel, j.Status.ToString(), NameOf(names, j.AssignedMechanicId), j.ReceivedDate, j.DueDate])
                .ToList());
    }

    private async Task<ReportData> RevenueReportAsync(GetReportQuery request, CancellationToken ct)
    {
        // Per-bill (paid date, total), then grouped per calendar day in memory (report volumes are
        // small and reads are live — the deliberate v1 tradeoff in docs/01 NFRs).
        var query = db.Bills.AsNoTracking().Where(b => b.IsPaid && b.PaidAtUtc != null);
        if (request.From is { } from) query = query.Where(b => b.PaidAtUtc >= from.ToDateTime(TimeOnly.MinValue));
        if (request.To is { } to) query = query.Where(b => b.PaidAtUtc < to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var paid = await query
            .Select(b => new
            {
                b.PaidAtUtc,
                Total = db.BillLineItems.Where(l => l.BillId == b.Id)
                    .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m,
            })
            .ToListAsync(ct);

        var rows = paid
            .GroupBy(b => DateOnly.FromDateTime(b.PaidAtUtc!.Value))
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<object?>)[g.Key, g.Count(), g.Sum(b => b.Total)])
            .ToList();

        return new ReportData("Revenue", ["Date", "Bills paid", "Revenue"], rows);
    }

    private async Task<ReportData> UnpaidBillsReportAsync(CancellationToken ct)
    {
        var rows = await db.Bills.AsNoTracking()
            .Where(b => !b.IsPaid)
            .Join(db.ServiceJobs, b => b.ServiceJobId, j => j.Id,
                (b, j) => new
                {
                    j.Title,
                    j.BikeModel,
                    j.ReceivedDate,
                    Total = db.BillLineItems.Where(l => l.BillId == b.Id)
                        .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m,
                })
            .OrderBy(r => r.ReceivedDate)
            .ToListAsync(ct);

        return new ReportData("Unpaid bills",
            ["Job", "Bike model", "Received", "Outstanding"],
            rows.Select(r => (IReadOnlyList<object?>)[r.Title, r.BikeModel, r.ReceivedDate, r.Total]).ToList());
    }

    private async Task<ReportData> BillingHistoryReportAsync(GetReportQuery request, CancellationToken ct)
    {
        // Anchored on the job's ReceivedDate — a stable calendar date every bill has, paid or not.
        var query = db.Bills.AsNoTracking()
            .Join(db.ServiceJobs, b => b.ServiceJobId, j => j.Id, (b, j) => new { Bill = b, Job = j });
        if (request.From is { } from) query = query.Where(x => x.Job.ReceivedDate >= from);
        if (request.To is { } to) query = query.Where(x => x.Job.ReceivedDate <= to);

        var rows = await query
            .Select(x => new
            {
                x.Job.Title,
                x.Job.BikeModel,
                x.Job.ReceivedDate,
                x.Bill.IsPaid,
                x.Bill.PaidAtUtc,
                Total = db.BillLineItems.Where(l => l.BillId == x.Bill.Id)
                    .Sum(l => (decimal?)Math.Round(l.Quantity * l.UnitPrice, 2)) ?? 0m,
            })
            .OrderBy(r => r.ReceivedDate)
            .ToListAsync(ct);

        return new ReportData("Billing history",
            ["Job", "Bike model", "Received", "Total", "Status", "Paid at (UTC)"],
            rows.Select(r => (IReadOnlyList<object?>)
                [r.Title, r.BikeModel, r.ReceivedDate, r.Total, r.IsPaid ? "Paid" : "Unpaid", r.PaidAtUtc])
                .ToList());
    }

    private async Task<ReportData> MechanicProductivityReportAsync(GetReportQuery request, CancellationToken ct)
    {
        // Jobs received in the range, bucketed per mechanic by their current status.
        var query = db.ServiceJobs.AsNoTracking().Where(j => j.AssignedMechanicId != null);
        if (request.From is { } from) query = query.Where(j => j.ReceivedDate >= from);
        if (request.To is { } to) query = query.Where(j => j.ReceivedDate <= to);

        var stats = await query
            .GroupBy(j => j.AssignedMechanicId!.Value)
            .Select(g => new
            {
                MechanicId = g.Key,
                Jobs = g.Count(),
                Completed = g.Count(j => j.Status == JobStatus.Completed),
                Delivered = g.Count(j => j.Status == JobStatus.Delivered),
                Open = g.Count(j => j.Status == JobStatus.Received || j.Status == JobStatus.InProgress),
            })
            .ToListAsync(ct);
        var names = await MechanicNamesAsync(stats.Select(s => (Guid?)s.MechanicId), ct);

        return new ReportData("Mechanic productivity",
            ["Mechanic", "Jobs", "Completed", "Delivered", "Open"],
            stats.OrderByDescending(s => s.Delivered + s.Completed)
                .Select(s => (IReadOnlyList<object?>)
                    [NameOf(names, s.MechanicId), s.Jobs, s.Completed, s.Delivered, s.Open])
                .ToList());
    }

    private async Task<Dictionary<Guid, string>> MechanicNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        return wanted.Count == 0
            ? []
            : await db.Users.AsNoTracking()
                .Where(u => wanted.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static string NameOf(Dictionary<Guid, string> names, Guid? id) =>
        id is { } value ? names.GetValueOrDefault(value, "Unknown") : "Unassigned";
}
