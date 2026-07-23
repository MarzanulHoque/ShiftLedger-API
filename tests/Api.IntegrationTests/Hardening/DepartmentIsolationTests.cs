using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Persistence.Configurations;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Hardening;

// P9: department-boundary enforcement across Jobs, Bills and Users. RuleHardeningTests covers the
// data-integrity locks (B3/A2/C1); this file covers the RBAC/department-scope layer added in P9.
[Collection("Database")]
public class DepartmentIsolationTests(IntegrationTestFixture fixture)
{
    // Title carries a fresh Guid: GetJobs orders by ReceivedDate then Title, and every job in this
    // file shares today's date, so a repeated literal title would tie-break nondeterministically
    // against unrelated tests' rows sharing this DB (see ServiceJobTests.GetJobs_Paginated_NoOverlap).
    private static async Task<Guid> CreateJobAsync(AppDbContext ctx, Guid departmentId, Guid? mechanicId = null)
    {
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        return await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(admin))
            .Handle(new CreateJobCommand($"Isolation job {Guid.NewGuid()}", null, "Test bike", JobPriority.Medium, mechanicId, null, null, departmentId), default);
    }

    private static async Task<Guid> CreateMechanicAsync(AppDbContext ctx, Guid departmentId, string email)
    {
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        return await new CreateUserCommandHandler(ctx, new PasswordHasher(), admin, TestDepartmentScope.For(admin))
            .Handle(new CreateUserCommand("Test Mechanic", email, "Secret#123", Role.Employee, departmentId), default);
    }

    // Rule RB3/RB4: a DepartmentAdmin cannot create a job in another department.
    [Fact]
    public async Task CreateJob_DepartmentAdmin_CrossDepartment_Rejected_RB3()
    {
        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        await using var ctx = fixture.CreateContext(deptAdmin);

        var act = () => new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(deptAdmin))
            .Handle(new CreateJobCommand("Cross-dept job", null, "Test bike", JobPriority.Medium, null, null, null,
                DepartmentConfiguration.BikeWashId), default);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // Rule RB3: the read-side department filter means a job in another department reads as "not found".
    [Fact]
    public async Task GetJob_DepartmentAdmin_CrossDepartment_NotFound_RB3()
    {
        Guid jobId;
        await using (var setup = fixture.CreateContext())
        {
            jobId = await CreateJobAsync(setup, DepartmentConfiguration.BikeWashId);
        }

        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        await using var ctx = fixture.CreateContext(deptAdmin);
        var act = () => new GetJobQueryHandler(ctx, deptAdmin).Handle(new GetJobQuery(jobId), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // Rule RB0: SuperAdmin reads and writes across both departments without restriction.
    [Fact]
    public async Task Jobs_SuperAdmin_AccessesBothDepartments_RB0()
    {
        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());

        Guid mechanicsJobId, washJobId;
        await using (var ctx = fixture.CreateContext(superAdmin))
        {
            mechanicsJobId = await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(superAdmin))
                .Handle(new CreateJobCommand("Mechanics job", null, "Test bike", JobPriority.Medium, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            washJobId = await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(superAdmin))
                .Handle(new CreateJobCommand("Wash job", null, "Test bike", JobPriority.Medium, null, null, null,
                    DepartmentConfiguration.BikeWashId), default);
        }

        await using var verify = fixture.CreateContext(superAdmin);
        (await new GetJobQueryHandler(verify, superAdmin).Handle(new GetJobQuery(mechanicsJobId), default)).Id.Should().Be(mechanicsJobId);
        (await new GetJobQueryHandler(verify, superAdmin).Handle(new GetJobQuery(washJobId), default)).Id.Should().Be(washJobId);
    }

    // Rule J2 (extended for RB3): a mechanic can only be assigned to a job in their own department.
    [Fact]
    public async Task AssignMechanic_RejectsMechanicFromOtherDepartment_J2()
    {
        Guid jobId, mechanicId;
        await using (var setup = fixture.CreateContext())
        {
            jobId = await CreateJobAsync(setup, DepartmentConfiguration.MechanicsId);
            mechanicId = await CreateMechanicAsync(setup, DepartmentConfiguration.BikeWashId, "wash-mech@test.local");
        }

        await using var ctx = fixture.CreateContext();
        var act = () => new AssignMechanicCommandHandler(ctx, TestNotifiers.For(ctx), TestCurrentUser.SuperAdmin(Guid.NewGuid()))
            .Handle(new AssignMechanicCommand(jobId, mechanicId), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    // Rule RB3/RB4: Bill/BillLineItem have no department of their own — access is derived from the
    // parent job, which is department-filtered. A DepartmentAdmin from another department gets 404.
    [Fact]
    public async Task Bills_DepartmentAdmin_CrossDepartment_NotFound_RB3()
    {
        Guid billId;
        await using (var setup = fixture.CreateContext())
        {
            var jobId = await CreateJobAsync(setup, DepartmentConfiguration.BikeWashId);
            var setupAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            billId = await new CreateBillCommandHandler(setup, setupAdmin, TimeProvider.System).Handle(new CreateBillCommand(jobId), default);
        }

        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        await using var ctx = fixture.CreateContext(deptAdmin);

        var addLine = () => new AddLineItemCommandHandler(ctx, deptAdmin)
            .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Cross-dept line", 1m, 100m), default);
        await addLine.Should().ThrowAsync<NotFoundException>();

        var setPaid = () => new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), deptAdmin)
            .Handle(new SetBillPaidCommand(billId, true), default);
        await setPaid.Should().ThrowAsync<NotFoundException>();
    }

    // Rule RB3: GetBills only ever lists bills for the caller's own department; SuperAdmin sees all.
    [Fact]
    public async Task GetBills_DepartmentAdmin_SeesOnlyOwnDepartmentBills_RB3()
    {
        Guid mechanicsBillId, washBillId;
        await using (var setup = fixture.CreateContext())
        {
            var mechanicsJobId = await CreateJobAsync(setup, DepartmentConfiguration.MechanicsId);
            var washJobId = await CreateJobAsync(setup, DepartmentConfiguration.BikeWashId);
            var setupAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            mechanicsBillId = await new CreateBillCommandHandler(setup, setupAdmin, TimeProvider.System).Handle(new CreateBillCommand(mechanicsJobId), default);
            washBillId = await new CreateBillCommandHandler(setup, setupAdmin, TimeProvider.System).Handle(new CreateBillCommand(washJobId), default);
        }

        var deptAdmin = TestCurrentUser.DepartmentAdmin(Guid.NewGuid(), DepartmentConfiguration.MechanicsId);
        await using var ctx = fixture.CreateContext(deptAdmin);
        var bills = await new GetBillsQueryHandler(ctx, deptAdmin).Handle(new GetBillsQuery(null, null, 1, 100), default);

        bills.Items.Should().Contain(b => b.Id == mechanicsBillId);
        bills.Items.Should().NotContain(b => b.Id == washBillId);
    }

    // Rule BL2: an explicit department filter on GetBills works for a SuperAdmin (narrowing the
    // consolidated list) and is a no-op for a DepartmentAdmin who is already scoped to it.
    [Fact]
    public async Task GetBills_DepartmentFilter_NarrowsConsolidatedList_BL2()
    {
        Guid mechanicsBillId, washBillId;
        await using (var setup = fixture.CreateContext())
        {
            var mechanicsJobId = await CreateJobAsync(setup, DepartmentConfiguration.MechanicsId);
            var washJobId = await CreateJobAsync(setup, DepartmentConfiguration.BikeWashId);
            var setupAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            mechanicsBillId = await new CreateBillCommandHandler(setup, setupAdmin, TimeProvider.System).Handle(new CreateBillCommand(mechanicsJobId), default);
            washBillId = await new CreateBillCommandHandler(setup, setupAdmin, TimeProvider.System).Handle(new CreateBillCommand(washJobId), default);
        }

        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        await using var ctx = fixture.CreateContext(superAdmin);
        var filtered = await new GetBillsQueryHandler(ctx, superAdmin)
            .Handle(new GetBillsQuery(null, DepartmentConfiguration.MechanicsId, 1, 100), default);

        filtered.Items.Should().Contain(b => b.Id == mechanicsBillId);
        filtered.Items.Should().NotContain(b => b.Id == washBillId);
    }

    // Rule BL2/C2: the SuperAdmin's consolidated total across all bills must equal the sum of the
    // per-department roll-up rows — computed independently here so the test would actually catch drift.
    [Fact]
    public async Task GetBillingSummary_ConsolidatedTotal_MatchesSumOfPerDepartmentTotals_BL2()
    {
        await using (var setup = fixture.CreateContext())
        {
            var setupAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            var mechanicsJobId = await CreateJobAsync(setup, DepartmentConfiguration.MechanicsId);
            var washJobId = await CreateJobAsync(setup, DepartmentConfiguration.BikeWashId);
            var mechanicsBillId = await new CreateBillCommandHandler(setup, setupAdmin, TimeProvider.System).Handle(new CreateBillCommand(mechanicsJobId), default);
            var washBillId = await new CreateBillCommandHandler(setup, setupAdmin, TimeProvider.System).Handle(new CreateBillCommand(washJobId), default);
            await new AddLineItemCommandHandler(setup, setupAdmin)
                .Handle(new AddLineItemCommand(mechanicsBillId, LineItemType.Labor, "Tune-up", 1m, 400m), default);
            await new AddLineItemCommandHandler(setup, setupAdmin)
                .Handle(new AddLineItemCommand(washBillId, LineItemType.Part, "Wash kit", 2m, 25m), default);
        }

        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        await using var ctx = fixture.CreateContext(superAdmin);

        var summary = await new GetBillingSummaryQueryHandler(ctx, superAdmin).Handle(new GetBillingSummaryQuery(), default);
        var consolidated = await new GetBillsQueryHandler(ctx, superAdmin).Handle(new GetBillsQuery(null, null, 1, 1000), default);

        summary.Sum(d => d.GrandTotal).Should().Be(consolidated.Items.Sum(b => b.Total));
        summary.Sum(d => d.TotalCount).Should().Be(consolidated.Items.Count);
    }

    // Rule B3 + RB0: the paid-bill edit lock holds even for the Super Admin — RB0's bypass never
    // touches data-integrity locks.
    [Fact]
    public async Task PaidBill_IsEditLocked_HoldsForSuperAdmin_B3()
    {
        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        Guid billId;
        await using (var ctx = fixture.CreateContext(superAdmin))
        {
            var jobId = await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(superAdmin))
                .Handle(new CreateJobCommand("Locked bill job", null, "Test bike", JobPriority.Medium, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            billId = await new CreateBillCommandHandler(ctx, superAdmin, TimeProvider.System).Handle(new CreateBillCommand(jobId), default);
            await new AddLineItemCommandHandler(ctx, superAdmin)
                .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Full service", 1m, 1000m), default);
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), superAdmin)
                .Handle(new SetBillPaidCommand(billId, true), default);
        }

        await using var locked = fixture.CreateContext(superAdmin);
        var act = () => new AddLineItemCommandHandler(locked, superAdmin)
            .Handle(new AddLineItemCommand(billId, LineItemType.Part, "Chain", 1m, 50m), default);

        await act.Should().ThrowAsync<BusinessRuleException>();
    }
}
