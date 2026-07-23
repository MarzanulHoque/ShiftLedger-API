using FluentAssertions;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Notifications;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Persistence.Configurations;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Notifications;

// Rule N3: the overdue-job/unpaid-bill sweep (RunOverdueUnpaidAlertSweepCommandHandler), invoked
// directly here the same way OverdueUnpaidAlertHostedService's timer would — no timer needed to
// test the business logic itself.
[Collection("Database")]
public class OverdueUnpaidAlertSweepTests(IntegrationTestFixture fixture)
{
    // A pinned instant so "overdue" / "past threshold" comparisons are exact, not flaky against
    // wall-clock time.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<Guid> CreateSuperAdminAsync(AppDbContext ctx, string email)
    {
        var user = new User
        {
            FullName = "Test Super Admin", Email = email, PasswordHash = "unused",
            Role = Role.SuperAdmin, DepartmentId = null, IsActive = true,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    // Rule N3: a job past its due date (and not yet Delivered) is flagged overdue exactly once —
    // running the sweep again the same day must not re-raise the alert.
    [Fact]
    public async Task Sweep_FlagsOverdueJob_OnceThenGuardsSameDay_N3()
    {
        var day0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var day1 = day0.AddDays(1);

        Guid superAdminId;
        await using (var setup = fixture.CreateContext())
        {
            superAdminId = await CreateSuperAdminAsync(setup, "n3-overdue-super@test.local");
            var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            await new CreateJobCommandHandler(setup, new FixedTimeProvider(day0), TestNotifiers.For(setup), TestDepartmentScope.For(scopeAdmin))
                .Handle(new CreateJobCommand("N3 overdue job", null, "Test bike", null, null,
                    DateOnly.FromDateTime(day0.UtcDateTime), DateOnly.FromDateTime(day0.UtcDateTime),
                    DepartmentConfiguration.MechanicsId), default);
        }

        // First sweep, one day after the due date: the job is overdue.
        await using (var ctx = fixture.CreateContext())
        {
            await new RunOverdueUnpaidAlertSweepCommandHandler(ctx, TestNotifiers.For(ctx), new FixedTimeProvider(day1))
                .Handle(new RunOverdueUnpaidAlertSweepCommand(), default);
        }

        // Second sweep, same day: the guard must skip it — no duplicate alert.
        await using (var ctx = fixture.CreateContext())
        {
            await new RunOverdueUnpaidAlertSweepCommandHandler(ctx, TestNotifiers.For(ctx), new FixedTimeProvider(day1))
                .Handle(new RunOverdueUnpaidAlertSweepCommand(), default);
        }

        await using var verify = fixture.CreateContext();
        var notifications = await new GetNotificationsQueryHandler(verify, TestCurrentUser.SuperAdmin(superAdminId))
            .Handle(new GetNotificationsQuery(), default);
        notifications.Items.Count(n => n.Type == "JobOverdue").Should().Be(1);
    }

    // Rule N3: an unpaid bill only alerts once it has aged past OrgSettings.UnpaidAlertDays
    // (default 7, no OrgSettings row here) — not before.
    [Fact]
    public async Task Sweep_FlagsUnpaidBillPastThreshold_ButNotBeforeIt_N3()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var beforeThreshold = createdAt.AddDays(3);
        var pastThreshold = createdAt.AddDays(8);

        Guid superAdminId;
        await using (var setup = fixture.CreateContext())
        {
            superAdminId = await CreateSuperAdminAsync(setup, "n3-unpaid-super@test.local");
            var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            var jobId = await new CreateJobCommandHandler(setup, new FixedTimeProvider(createdAt), TestNotifiers.For(setup), TestDepartmentScope.For(scopeAdmin))
                .Handle(new CreateJobCommand("N3 unpaid job", null, "Test bike", null, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            await new CreateBillCommandHandler(setup, scopeAdmin, new FixedTimeProvider(createdAt))
                .Handle(new CreateBillCommand(jobId), default);
        }

        // Still under the 7-day default threshold: no alert yet.
        await using (var ctx = fixture.CreateContext())
        {
            await new RunOverdueUnpaidAlertSweepCommandHandler(ctx, TestNotifiers.For(ctx), new FixedTimeProvider(beforeThreshold))
                .Handle(new RunOverdueUnpaidAlertSweepCommand(), default);
        }

        await using (var afterEarlySweep = fixture.CreateContext())
        {
            var early = await new GetNotificationsQueryHandler(afterEarlySweep, TestCurrentUser.SuperAdmin(superAdminId))
                .Handle(new GetNotificationsQuery(), default);
            early.Items.Should().NotContain(n => n.Type == "BillUnpaid");
        }

        // Past the threshold: the alert fires.
        await using (var ctx = fixture.CreateContext())
        {
            await new RunOverdueUnpaidAlertSweepCommandHandler(ctx, TestNotifiers.For(ctx), new FixedTimeProvider(pastThreshold))
                .Handle(new RunOverdueUnpaidAlertSweepCommand(), default);
        }

        await using var verify = fixture.CreateContext();
        var late = await new GetNotificationsQueryHandler(verify, TestCurrentUser.SuperAdmin(superAdminId))
            .Handle(new GetNotificationsQuery(), default);
        late.Items.Should().Contain(n => n.Type == "BillUnpaid");
    }
}
