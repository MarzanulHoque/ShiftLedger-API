using FluentAssertions;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Dashboards;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Persistence.Configurations;
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
            before = await new GetAdminDashboardQueryHandler(ctx, TimeProvider.System, TestCurrentUser.SuperAdmin(Guid.NewGuid()))
                .Handle(new GetAdminDashboardQuery(null), default);
        }

        await using (var ctx = fixture.CreateContext())
        {
            var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            var jobId = await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(scopeAdmin))
                .Handle(new CreateJobCommand("Dashboard job", null, "Cannondale Trail", null, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            var billId = await new CreateBillCommandHandler(ctx, scopeAdmin, TimeProvider.System).Handle(new CreateBillCommand(jobId), default);
            await new AddLineItemCommandHandler(ctx, scopeAdmin)
                .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Dashboard labor", 1m, 777m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), scopeAdmin)
                .Handle(new SetBillPaidCommand(billId, true), default);
        }

        await using var verify = fixture.CreateContext();
        var after = await new GetAdminDashboardQueryHandler(verify, TimeProvider.System, TestCurrentUser.SuperAdmin(Guid.NewGuid()))
            .Handle(new GetAdminDashboardQuery(null), default);

        after.JobsReceivedToday.Should().Be(before.JobsReceivedToday + 1);
        after.BillsPaidToday.Should().Be(before.BillsPaidToday + 1);
        after.RevenueToday.Should().Be(before.RevenueToday + 777m);
    }

    // Rule RB3/RB4 (P12): a DepartmentAdmin's dashboard only ever reflects their own department —
    // asserted as before/after deltas (like the C3 test above) so this isn't thrown off by other
    // tests sharing the same day's data; the delta itself proves the Bike Wash bill didn't leak in.
    [Fact]
    public async Task AdminDashboard_DepartmentAdmin_SeesOnlyOwnDepartment_RB3()
    {
        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);

        AdminDashboardDto before;
        await using (var ctx = fixture.CreateContext())
        {
            before = await new GetAdminDashboardQueryHandler(ctx, TimeProvider.System, deptAdmin)
                .Handle(new GetAdminDashboardQuery(null), default);
        }

        Guid mechanicsMechanicId;
        await using (var ctx = fixture.CreateContext())
        {
            mechanicsMechanicId = await new CreateUserCommandHandler(ctx, new PasswordHasher(), superAdmin, TestDepartmentScope.For(superAdmin))
                .Handle(new CreateUserCommand("Dash Dept Mech", $"p12-dash-mech-{Guid.NewGuid()}@test.local", "Secret#123",
                    Role.Employee, DepartmentConfiguration.MechanicsId), default);

            var jobs = new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(superAdmin));
            var mechanicsJobId = await jobs.Handle(
                new CreateJobCommand($"P12 mechanics job {Guid.NewGuid()}", null, "Test bike", null, mechanicsMechanicId, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            var washJobId = await jobs.Handle(
                new CreateJobCommand($"P12 wash job {Guid.NewGuid()}", null, "Test bike", null, null, null, null,
                    DepartmentConfiguration.BikeWashId), default);

            var mechanicsBillId = await new CreateBillCommandHandler(ctx, superAdmin, TimeProvider.System).Handle(new CreateBillCommand(mechanicsJobId), default);
            await new AddLineItemCommandHandler(ctx, superAdmin)
                .Handle(new AddLineItemCommand(mechanicsBillId, LineItemType.Labor, "Mechanics work", 1m, 500m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), superAdmin)
                .Handle(new SetBillPaidCommand(mechanicsBillId, true), default);

            var washBillId = await new CreateBillCommandHandler(ctx, superAdmin, TimeProvider.System).Handle(new CreateBillCommand(washJobId), default);
            await new AddLineItemCommandHandler(ctx, superAdmin)
                .Handle(new AddLineItemCommand(washBillId, LineItemType.Labor, "Wash work", 1m, 300m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), superAdmin)
                .Handle(new SetBillPaidCommand(washBillId, true), default);
        }

        await using var verify = fixture.CreateContext();
        var after = await new GetAdminDashboardQueryHandler(verify, TimeProvider.System, deptAdmin)
            .Handle(new GetAdminDashboardQuery(null), default);

        // Only the Mechanics-department bill (500) counts — the 300 Bike Wash bill must not leak in.
        (after.RevenueToday - before.RevenueToday).Should().Be(500m);
        (after.BillsPaidToday - before.BillsPaidToday).Should().Be(1);
        after.MechanicWorkload.Should().Contain(w => w.MechanicId == mechanicsMechanicId);
    }

    // Rule RB3/RB4 (P12): the comparison rollup splits by department the same way the consolidated
    // dashboard sums them — asserted via delta so it isn't thrown off by other tests' same-day data.
    [Fact]
    public async Task DashboardComparison_SplitsPerDepartment_SuperAdminSeesBoth_DepartmentAdminSeesOwnOnly_RB3()
    {
        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());

        IReadOnlyList<DepartmentDashboardMetricsDto> before;
        await using (var ctx = fixture.CreateContext())
        {
            before = await new GetDashboardComparisonQueryHandler(ctx, TimeProvider.System, superAdmin)
                .Handle(new GetDashboardComparisonQuery(null), default);
        }
        var beforeMechanics = before.FirstOrDefault(d => d.DepartmentId == DepartmentConfiguration.MechanicsId)?.JobsReceivedToday ?? 0;
        var beforeWash = before.FirstOrDefault(d => d.DepartmentId == DepartmentConfiguration.BikeWashId)?.JobsReceivedToday ?? 0;

        await using (var ctx = fixture.CreateContext())
        {
            var jobs = new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(superAdmin));
            await jobs.Handle(
                new CreateJobCommand($"P12 comparison mechanics job {Guid.NewGuid()}", null, "Test bike", null, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            await jobs.Handle(
                new CreateJobCommand($"P12 comparison wash job {Guid.NewGuid()}", null, "Test bike", null, null, null, null,
                    DepartmentConfiguration.BikeWashId), default);
        }

        await using var verify = fixture.CreateContext();
        var afterSuper = await new GetDashboardComparisonQueryHandler(verify, TimeProvider.System, superAdmin)
            .Handle(new GetDashboardComparisonQuery(null), default);
        var afterMechanics = afterSuper.First(d => d.DepartmentId == DepartmentConfiguration.MechanicsId).JobsReceivedToday;
        var afterWash = afterSuper.First(d => d.DepartmentId == DepartmentConfiguration.BikeWashId).JobsReceivedToday;

        (afterMechanics - beforeMechanics).Should().Be(1);
        (afterWash - beforeWash).Should().Be(1);

        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        var deptAdminView = await new GetDashboardComparisonQueryHandler(verify, TimeProvider.System, deptAdmin)
            .Handle(new GetDashboardComparisonQuery(null), default);

        deptAdminView.Should().ContainSingle();
        deptAdminView.Single().DepartmentId.Should().Be(DepartmentConfiguration.MechanicsId);
    }

    // Rule R2: /dashboard/me only ever contains the caller's own jobs.
    [Fact]
    public async Task MyDashboard_ContainsOnlyOwnJobs_R2()
    {
        Guid mechanicId, otherMechanicId, myJobId;
        await using (var setup = fixture.CreateContext())
        {
            var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            var users = new CreateUserCommandHandler(setup, new PasswordHasher(), scopeAdmin, TestDepartmentScope.For(scopeAdmin));
            mechanicId = await users.Handle(
                new CreateUserCommand("Dash Mech", "p6-dash-mech@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);
            otherMechanicId = await users.Handle(
                new CreateUserCommand("Dash Other", "p6-dash-other@test.local", "Secret#123", Role.Employee, DepartmentConfiguration.MechanicsId), default);

            var jobs = new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(scopeAdmin));
            myJobId = await jobs.Handle(
                new CreateJobCommand("My dash job", null, "Merida Big Nine", null, mechanicId, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            await jobs.Handle(
                new CreateJobCommand("Other dash job", null, "Scott Aspect", null, otherMechanicId, null, null,
                    DepartmentConfiguration.MechanicsId), default);
        }

        await using var verify = fixture.CreateContext();
        var dashboard = await new GetMyDashboardQueryHandler(verify, TestCurrentUser.Employee(mechanicId))
            .Handle(new GetMyDashboardQuery(), default);

        dashboard.MyOpenJobs.Should().Contain(j => j.Id == myJobId);
        dashboard.MyOpenJobs.Should().OnlyContain(j => j.AssignedMechanicId == mechanicId);
        dashboard.MyJobsByStatus.Sum(s => s.Count).Should().Be(1);
    }
}
