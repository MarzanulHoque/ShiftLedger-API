using FluentAssertions;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Dashboards;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Dashboards;

[Collection("Database")]
public class DashboardTests(IntegrationTestFixture fixture)
{
    // Rule C3: the dashboard is a live read — a just-committed job and a just-paid bill show up
    // immediately. Asserted as before/after deltas so the test is independent of other data.
    [Fact]
    public async Task AdminDashboard_ReflectsJustCommittedWrites_C3()
    {
        AdminDashboardDto before;
        await using (var ctx = fixture.CreateContext())
        {
            before = await new GetAdminDashboardQueryHandler(ctx, TimeProvider.System)
                .Handle(new GetAdminDashboardQuery(null), default);
        }

        await using (var ctx = fixture.CreateContext())
        {
            var jobId = await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx))
                .Handle(new CreateJobCommand("Dashboard job", null, "Cannondale Trail", null, null, null, null), default);
            var billId = await new CreateBillCommandHandler(ctx).Handle(new CreateBillCommand(jobId), default);
            await new AddLineItemCommandHandler(ctx)
                .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Dashboard labor", 1m, 777m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx))
                .Handle(new SetBillPaidCommand(billId, true), default);
        }

        await using var verify = fixture.CreateContext();
        var after = await new GetAdminDashboardQueryHandler(verify, TimeProvider.System)
            .Handle(new GetAdminDashboardQuery(null), default);

        after.JobsReceivedToday.Should().Be(before.JobsReceivedToday + 1);
        after.BillsPaidToday.Should().Be(before.BillsPaidToday + 1);
        after.RevenueToday.Should().Be(before.RevenueToday + 777m);
    }

    // Rule R2: /dashboard/me only ever contains the caller's own jobs.
    [Fact]
    public async Task MyDashboard_ContainsOnlyOwnJobs_R2()
    {
        Guid mechanicId, otherMechanicId, myJobId;
        await using (var setup = fixture.CreateContext())
        {
            var users = new CreateUserCommandHandler(setup, new PasswordHasher());
            mechanicId = await users.Handle(
                new CreateUserCommand("Dash Mech", "p6-dash-mech@test.local", "Secret#123", Role.Employee, null), default);
            otherMechanicId = await users.Handle(
                new CreateUserCommand("Dash Other", "p6-dash-other@test.local", "Secret#123", Role.Employee, null), default);

            var jobs = new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup));
            myJobId = await jobs.Handle(
                new CreateJobCommand("My dash job", null, "Merida Big Nine", null, mechanicId, null, null), default);
            await jobs.Handle(
                new CreateJobCommand("Other dash job", null, "Scott Aspect", null, otherMechanicId, null, null), default);
        }

        await using var verify = fixture.CreateContext();
        var dashboard = await new GetMyDashboardQueryHandler(verify, TestCurrentUser.Employee(mechanicId))
            .Handle(new GetMyDashboardQuery(), default);

        dashboard.MyOpenJobs.Should().Contain(j => j.Id == myJobId);
        dashboard.MyOpenJobs.Should().OnlyContain(j => j.AssignedMechanicId == mechanicId);
        dashboard.MyJobsByStatus.Sum(s => s.Count).Should().Be(1);
    }
}
