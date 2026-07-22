using FluentAssertions;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Reports;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Persistence.Configurations;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Reports;

[Collection("Database")]
public class ReportTests(IntegrationTestFixture fixture)
{
    private async Task<Guid> CreateBilledJobAsync(string title, decimal amount, bool paid)
    {
        await using var ctx = fixture.CreateContext();
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        var jobId = await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(admin))
            .Handle(new CreateJobCommand(title, null, "Test bike", null, null, null, null,
                DepartmentConfiguration.MechanicsId), default);
        var billId = await new CreateBillCommandHandler(ctx, admin).Handle(new CreateBillCommand(jobId), default);
        await new AddLineItemCommandHandler(ctx, admin)
            .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Service", 1m, amount), default);
        if (paid)
        {
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), admin)
                .Handle(new SetBillPaidCommand(billId, true), default);
        }
        return jobId;
    }

    private async Task<decimal> TodayRevenueFromReportAsync()
    {
        await using var ctx = fixture.CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var report = await new GetReportQueryHandler(ctx)
            .Handle(new GetReportQuery(ReportType.Revenue, today, today, null, null), default);
        // Columns: Date | Bills paid | Revenue
        return report.Rows.Sum(r => (decimal)r[2]!);
    }

    // Rule C2: the revenue report equals the paid bills in the source tables — no copy to drift.
    [Fact]
    public async Task RevenueReport_MatchesPaidSourceBills_C2()
    {
        var before = await TodayRevenueFromReportAsync();

        await CreateBilledJobAsync("Revenue report job A", 111m, paid: true);
        await CreateBilledJobAsync("Revenue report job B", 222m, paid: true);

        var after = await TodayRevenueFromReportAsync();
        (after - before).Should().Be(333m);
    }

    // The unpaid-bills report lists exactly the unpaid ones.
    [Fact]
    public async Task UnpaidBillsReport_ListsOnlyUnpaid()
    {
        var unpaidTitle = $"Unpaid job {Guid.NewGuid():N}";
        var paidTitle = $"Paid job {Guid.NewGuid():N}";
        await CreateBilledJobAsync(unpaidTitle, 150m, paid: false);
        await CreateBilledJobAsync(paidTitle, 250m, paid: true);

        await using var ctx = fixture.CreateContext();
        var report = await new GetReportQueryHandler(ctx)
            .Handle(new GetReportQuery(ReportType.UnpaidBills, null, null, null, null), default);

        var jobTitles = report.Rows.Select(r => (string?)r[0]).ToList();
        jobTitles.Should().Contain(unpaidTitle);
        jobTitles.Should().NotContain(paidTitle);
    }

    // The jobs report respects the status filter.
    [Fact]
    public async Task JobsReport_FiltersByStatus()
    {
        var title = $"Jobs report job {Guid.NewGuid():N}";
        await CreateBilledJobAsync(title, 100m, paid: false); // status = Received

        await using var ctx = fixture.CreateContext();
        var handler = new GetReportQueryHandler(ctx);

        var received = await handler.Handle(
            new GetReportQuery(ReportType.Jobs, null, null, null, JobStatus.Received), default);
        received.Rows.Select(r => (string?)r[0]).Should().Contain(title);

        var delivered = await handler.Handle(
            new GetReportQuery(ReportType.Jobs, null, null, null, JobStatus.Delivered), default);
        delivered.Rows.Select(r => (string?)r[0]).Should().NotContain(title);
    }
}
