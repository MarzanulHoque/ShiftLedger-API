using FluentAssertions;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Bills;

[Collection("Database")]
public class BillingTests(IntegrationTestFixture fixture)
{
    private static async Task<Guid> CreateJobAsync(AppDbContext ctx) =>
        await new CreateJobCommandHandler(ctx, TimeProvider.System).Handle(
            new CreateJobCommand("Tune-up", null, "Giant Escape 3", JobPriority.Medium, null, null, null), default);

    private async Task<(Guid JobId, Guid BillId)> CreateJobWithBillAsync()
    {
        await using var ctx = fixture.CreateContext();
        var jobId = await CreateJobAsync(ctx);
        var billId = await new CreateBillCommandHandler(ctx).Handle(new CreateBillCommand(jobId), default);
        return (jobId, billId);
    }

    // Rule B1: a job has exactly one bill — creating a second is rejected.
    [Fact]
    public async Task CreateBill_SecondBillForJob_Rejected_B1()
    {
        var (jobId, _) = await CreateJobWithBillAsync();

        await using var ctx = fixture.CreateContext();
        var again = () => new CreateBillCommandHandler(ctx).Handle(new CreateBillCommand(jobId), default);
        await again.Should().ThrowAsync<BusinessRuleException>();
    }

    // Rule B2/C2: the total is computed from the lines and follows every line change.
    [Fact]
    public async Task BillTotal_ComputedFromLines_FollowsChanges_B2()
    {
        var (jobId, billId) = await CreateJobWithBillAsync();

        Guid laborId;
        await using (var ctx = fixture.CreateContext())
        {
            var add = new AddLineItemCommandHandler(ctx);
            laborId = await add.Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Brake adjustment", 1m, 500m), default);
            await add.Handle(new AddLineItemCommand(billId, LineItemType.Part, "Brake pad set", 2m, 300m), default);
        }

        await using (var verify = fixture.CreateContext())
        {
            var bill = await new GetJobBillQueryHandler(verify).Handle(new GetJobBillQuery(jobId), default);
            bill.Total.Should().Be(1100m);
            bill.Lines.Should().HaveCount(2);
        }

        // Change the labor line -> the computed total moves with it (no stored copy to drift).
        await using (var ctx = fixture.CreateContext())
        {
            await new UpdateLineItemCommandHandler(ctx).Handle(
                new UpdateLineItemCommand(billId, laborId, LineItemType.Labor, "Brake adjustment", 2m, 500m), default);
        }

        await using (var verify = fixture.CreateContext())
        {
            var bill = await new GetJobBillQueryHandler(verify).Handle(new GetJobBillQuery(jobId), default);
            bill.Total.Should().Be(1600m);
        }
    }

    // Rule B3: once paid, the bill is locked — add/update/delete of lines are all rejected.
    [Fact]
    public async Task PaidBill_IsEditLocked_B3()
    {
        var (_, billId) = await CreateJobWithBillAsync();

        Guid lineId;
        await using (var ctx = fixture.CreateContext())
        {
            lineId = await new AddLineItemCommandHandler(ctx)
                .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Full service", 1m, 1500m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System)
                .Handle(new SetBillPaidCommand(billId, true), default);
        }

        await using var locked = fixture.CreateContext();

        var addLine = () => new AddLineItemCommandHandler(locked)
            .Handle(new AddLineItemCommand(billId, LineItemType.Part, "Chain", 1m, 250m), default);
        await addLine.Should().ThrowAsync<BusinessRuleException>();

        var updateLine = () => new UpdateLineItemCommandHandler(locked)
            .Handle(new UpdateLineItemCommand(billId, lineId, LineItemType.Labor, "Full service", 2m, 1500m), default);
        await updateLine.Should().ThrowAsync<BusinessRuleException>();

        var deleteLine = () => new DeleteLineItemCommandHandler(locked)
            .Handle(new DeleteLineItemCommand(billId, lineId), default);
        await deleteLine.Should().ThrowAsync<BusinessRuleException>();
    }

    // Rule B4: paying stamps PaidAtUtc; reopening clears it and unlocks edits (B3 correction path).
    [Fact]
    public async Task SetPaid_StampsPaidAtUtc_ReopenClears_B4()
    {
        var (jobId, billId) = await CreateJobWithBillAsync();

        await using (var ctx = fixture.CreateContext())
        {
            await new AddLineItemCommandHandler(ctx)
                .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Wheel truing", 1m, 400m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System)
                .Handle(new SetBillPaidCommand(billId, true), default);
        }

        await using (var verify = fixture.CreateContext())
        {
            var bill = await new GetJobBillQueryHandler(verify).Handle(new GetJobBillQuery(jobId), default);
            bill.IsPaid.Should().BeTrue();
            bill.PaidAtUtc.Should().NotBeNull();
        }

        await using (var ctx = fixture.CreateContext())
        {
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System)
                .Handle(new SetBillPaidCommand(billId, false), default);
            // Unlocked again: an edit now succeeds.
            await new AddLineItemCommandHandler(ctx)
                .Handle(new AddLineItemCommand(billId, LineItemType.Part, "Spoke", 2m, 15m), default);
        }

        await using (var verify = fixture.CreateContext())
        {
            var bill = await new GetJobBillQueryHandler(verify).Handle(new GetJobBillQuery(jobId), default);
            bill.IsPaid.Should().BeFalse();
            bill.PaidAtUtc.Should().BeNull();
            bill.Lines.Should().HaveCount(2);
        }
    }

    // Rule B4 guard: an empty bill cannot be marked paid.
    [Fact]
    public async Task SetPaid_EmptyBill_Rejected_B4()
    {
        var (_, billId) = await CreateJobWithBillAsync();

        await using var ctx = fixture.CreateContext();
        var pay = () => new SetBillPaidCommandHandler(ctx, TimeProvider.System)
            .Handle(new SetBillPaidCommand(billId, true), default);
        await pay.Should().ThrowAsync<BusinessRuleException>();
    }

    // GetBills: the paid/unpaid filter and the computed totals both come from live source rows.
    [Fact]
    public async Task GetBills_FiltersByPaid_WithComputedTotals_B2()
    {
        var (_, unpaidBillId) = await CreateJobWithBillAsync();
        var (_, paidBillId) = await CreateJobWithBillAsync();

        await using (var ctx = fixture.CreateContext())
        {
            var add = new AddLineItemCommandHandler(ctx);
            await add.Handle(new AddLineItemCommand(unpaidBillId, LineItemType.Labor, "Gear indexing", 1m, 350m), default);
            await add.Handle(new AddLineItemCommand(paidBillId, LineItemType.Labor, "Flat fix", 1m, 200m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System)
                .Handle(new SetBillPaidCommand(paidBillId, true), default);
        }

        await using var verify = fixture.CreateContext();

        var unpaid = await new GetBillsQueryHandler(verify).Handle(new GetBillsQuery(false), default);
        unpaid.Should().Contain(b => b.Id == unpaidBillId && b.Total == 350m);
        unpaid.Should().NotContain(b => b.Id == paidBillId);

        var paid = await new GetBillsQueryHandler(verify).Handle(new GetBillsQuery(true), default);
        paid.Should().Contain(b => b.Id == paidBillId && b.Total == 200m);
    }
}
