using FluentAssertions;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Persistence.Configurations;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Jobs;

[Collection("Database")]
public class ServiceJobTests(IntegrationTestFixture fixture)
{
    // Rule RB1: the Super Admin is seeded once, never provisioned via CreateUserCommandHandler — a
    // test "admin" is inserted directly, mirroring DbSeeder. Everyone else goes through the handler.
    private static async Task<Guid> CreateUserAsync(AppDbContext ctx, string email, Role role)
    {
        if (role == Role.SuperAdmin)
        {
            var admin = new User { FullName = "Test Admin", Email = email, PasswordHash = "n/a", Role = Role.SuperAdmin };
            ctx.Users.Add(admin);
            await ctx.SaveChangesAsync();
            return admin.Id;
        }

        var actingAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        return await new CreateUserCommandHandler(ctx, new PasswordHasher(), actingAdmin, TestDepartmentScope.For(actingAdmin))
            .Handle(new CreateUserCommand("Test User", email, "Secret#123", role, DepartmentConfiguration.MechanicsId), default);
    }

    private static CreateJobCommand NewJob(Guid? mechanicId = null) =>
        new("Brake service", "Squeaky front brake", "Trek FX 2", JobPriority.Medium, mechanicId, null, null,
            DepartmentConfiguration.MechanicsId);

    // Rule A1: creating a job writes a 'Created' audit row stamped with the acting user.
    [Fact]
    public async Task CreateJob_WritesAuditRow_StampedWithActingUser_A1()
    {
        Guid adminId;
        await using (var setup = fixture.CreateContext())
        {
            adminId = await CreateUserAsync(setup, "p4-admin-audit@test.local", Role.SuperAdmin);
        }

        Guid jobId;
        await using (var ctx = fixture.CreateContext(TestCurrentUser.SuperAdmin(adminId)))
        {
            jobId = await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(TestCurrentUser.SuperAdmin(Guid.NewGuid()))).Handle(NewJob(), default);
        }

        await using var verify = fixture.CreateContext();
        var history = await new GetJobHistoryQueryHandler(verify, TestCurrentUser.SuperAdmin(adminId)).Handle(new GetJobHistoryQuery(jobId), default);
        history.Should().ContainSingle(h => h.Action == "Created")
            .Which.ChangedById.Should().Be(adminId);
    }

    // Rule J1: advancing one step is allowed; an illegal jump is rejected (422 via BusinessRuleException).
    [Fact]
    public async Task ChangeStatus_EnforcesFlow_J1()
    {
        Guid adminId, jobId;
        await using (var setup = fixture.CreateContext())
        {
            adminId = await CreateUserAsync(setup, "p4-admin-flow@test.local", Role.SuperAdmin);
            jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(TestCurrentUser.SuperAdmin(Guid.NewGuid()))).Handle(NewJob(), default);
        }

        var admin = TestCurrentUser.SuperAdmin(adminId);

        await using (var ctx = fixture.CreateContext(admin))
        {
            var jump = () => new ChangeJobStatusCommandHandler(ctx, admin, TestNotifiers.For(ctx))
                .Handle(new ChangeJobStatusCommand(jobId, JobStatus.Delivered), default);
            await jump.Should().ThrowAsync<BusinessRuleException>();
        }

        await using (var ctx = fixture.CreateContext(admin))
        {
            await new ChangeJobStatusCommandHandler(ctx, admin, TestNotifiers.For(ctx))
                .Handle(new ChangeJobStatusCommand(jobId, JobStatus.InProgress), default);
        }

        await using var verify = fixture.CreateContext();
        var job = await new GetJobQueryHandler(verify, admin).Handle(new GetJobQuery(jobId), default);
        job.Status.Should().Be(JobStatus.InProgress);
    }

    // Rule J2: only an Employee-role user can be assigned; assigning an Admin is rejected.
    [Fact]
    public async Task AssignMechanic_RejectsNonEmployee_J2()
    {
        Guid adminId, mechanicId, jobId;
        await using (var setup = fixture.CreateContext())
        {
            adminId = await CreateUserAsync(setup, "p4-admin-assign@test.local", Role.SuperAdmin);
            mechanicId = await CreateUserAsync(setup, "p4-mech-assign@test.local", Role.Employee);
            jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(TestCurrentUser.SuperAdmin(Guid.NewGuid()))).Handle(NewJob(), default);
        }

        await using (var ctx = fixture.CreateContext())
        {
            var assignAdmin = () => new AssignMechanicCommandHandler(ctx, TestNotifiers.For(ctx), TestCurrentUser.SuperAdmin(Guid.NewGuid()))
                .Handle(new AssignMechanicCommand(jobId, adminId), default);
            await assignAdmin.Should().ThrowAsync<BusinessRuleException>();
        }

        await using (var ctx = fixture.CreateContext())
        {
            await new AssignMechanicCommandHandler(ctx, TestNotifiers.For(ctx), TestCurrentUser.SuperAdmin(Guid.NewGuid()))
                .Handle(new AssignMechanicCommand(jobId, mechanicId), default);
        }
    }

    // Rule R2/R3: a mechanic sees only their own jobs, and cannot open another mechanic's job.
    [Fact]
    public async Task Mechanic_SeesOnlyOwnJobs_R2R3()
    {
        Guid mech1, mech2, jobForMech1;
        await using (var setup = fixture.CreateContext())
        {
            mech1 = await CreateUserAsync(setup, "p4-mech1@test.local", Role.Employee);
            mech2 = await CreateUserAsync(setup, "p4-mech2@test.local", Role.Employee);
            jobForMech1 = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(TestCurrentUser.SuperAdmin(Guid.NewGuid()))).Handle(NewJob(mech1), default);
        }

        await using var verify = fixture.CreateContext();

        // Both mechanics are in Mechanics (same as the job) — CreateUserAsync always provisions
        // there — so the department check passes and the R2/R3 ownership check is what's on trial.
        var mech1AsUser = TestCurrentUser.Employee(mech1, DepartmentConfiguration.MechanicsId);
        var mech2AsUser = TestCurrentUser.Employee(mech2, DepartmentConfiguration.MechanicsId);

        var mech1Jobs = await new GetJobsQueryHandler(verify, mech1AsUser)
            .Handle(new GetJobsQuery(null, null, 1, 100), default);
        mech1Jobs.Items.Should().Contain(j => j.Id == jobForMech1);

        var mech2Jobs = await new GetJobsQueryHandler(verify, mech2AsUser)
            .Handle(new GetJobsQuery(null, null, 1, 100), default);
        mech2Jobs.Items.Should().NotContain(j => j.Id == jobForMech1);

        var openOthers = () => new GetJobQueryHandler(verify, mech2AsUser)
            .Handle(new GetJobQuery(jobForMech1), default);
        await openOthers.Should().ThrowAsync<ForbiddenException>();
    }

    // Rule J4: a deleted job is soft-deleted and excluded from listings.
    [Fact]
    public async Task DeleteJob_SoftDeletes_ExcludedFromList_J4()
    {
        Guid adminId, jobId;
        await using (var setup = fixture.CreateContext())
        {
            adminId = await CreateUserAsync(setup, "p4-admin-del@test.local", Role.SuperAdmin);
            jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(TestCurrentUser.SuperAdmin(Guid.NewGuid()))).Handle(NewJob(), default);
            await new DeleteJobCommandHandler(setup, TestCurrentUser.SuperAdmin(adminId)).Handle(new DeleteJobCommand(jobId), default);
        }

        await using var verify = fixture.CreateContext();
        var jobs = await new GetJobsQueryHandler(verify, TestCurrentUser.SuperAdmin(adminId))
            .Handle(new GetJobsQuery(null, null, 1, 100), default);
        jobs.Items.Should().NotContain(j => j.Id == jobId);
    }

    // NFR: list endpoints are paginated — pages don't overlap and totalCount spans the whole set.
    [Fact]
    public async Task GetJobs_Paginated_NoOverlap()
    {
        Guid adminId;
        await using (var setup = fixture.CreateContext())
        {
            adminId = await CreateUserAsync(setup, "p7-admin-paging@test.local", Role.SuperAdmin);
            var jobs = new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(TestCurrentUser.SuperAdmin(Guid.NewGuid())));
            for (var i = 0; i < 3; i++)
            {
                await jobs.Handle(NewJob() with { Title = $"Paging job {i}" }, default);
            }
        }

        await using var verify = fixture.CreateContext();
        var admin = TestCurrentUser.SuperAdmin(adminId);
        var page1 = await new GetJobsQueryHandler(verify, admin).Handle(new GetJobsQuery(null, null, 1, 2), default);
        var page2 = await new GetJobsQueryHandler(verify, admin).Handle(new GetJobsQuery(null, null, 2, 2), default);

        page1.Items.Should().HaveCount(2);
        page1.TotalCount.Should().BeGreaterThanOrEqualTo(3).And.Be(page2.TotalCount);
        page2.Items.Select(j => j.Id).Should().NotIntersectWith(page1.Items.Select(j => j.Id));
    }

    // Comments: add then read back, scoped to a user who can see the job.
    [Fact]
    public async Task AddComment_ThenList_ReturnsComment()
    {
        Guid adminId, jobId;
        await using (var setup = fixture.CreateContext())
        {
            adminId = await CreateUserAsync(setup, "p4-admin-comment@test.local", Role.SuperAdmin);
            jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(TestCurrentUser.SuperAdmin(Guid.NewGuid()))).Handle(NewJob(), default);
        }

        var admin = TestCurrentUser.SuperAdmin(adminId);
        await using (var ctx = fixture.CreateContext(admin))
        {
            await new AddJobCommentCommandHandler(ctx, admin, TimeProvider.System)
                .Handle(new AddJobCommentCommand(jobId, "Waiting on a brake pad."), default);
        }

        await using var verify = fixture.CreateContext();
        var comments = await new GetJobCommentsQueryHandler(verify, admin)
            .Handle(new GetJobCommentsQuery(jobId), default);
        comments.Should().ContainSingle(c => c.Body == "Waiting on a brake pad." && c.AuthorId == adminId);
    }
}
