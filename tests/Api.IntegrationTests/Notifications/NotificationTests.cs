using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShiftLedger.Application.Bills;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Notifications;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Entities;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Persistence.Configurations;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Notifications;

[Collection("Database")]
public class NotificationTests(IntegrationTestFixture fixture)
{
    private static async Task<Guid> CreateUserAsync(AppDbContext ctx, string email, Role role)
    {
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        return await new CreateUserCommandHandler(ctx, new PasswordHasher(), admin, TestDepartmentScope.For(admin))
            .Handle(new CreateUserCommand("Test User", email, "Secret#123", role, DepartmentConfiguration.MechanicsId), default);
    }

    // CreateUserCommandHandler forbids provisioning a SuperAdmin (Rule RB1 — it's seeded once at
    // startup), so NotifyDepartmentAsync's fan-out target is inserted directly here, same as
    // DbSeeder does for the real bootstrap account.
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

    private static async Task<Guid> CreateDepartmentAdminAsync(AppDbContext ctx, string email, Guid departmentId)
    {
        var admin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        return await new CreateUserCommandHandler(ctx, new PasswordHasher(), admin, TestDepartmentScope.For(admin))
            .Handle(new CreateUserCommand("Test Dept Admin", email, "Secret#123", Role.DepartmentAdmin, departmentId), default);
    }

    // Assigning a job persists a JobAssigned notification for the mechanic — and only for them.
    [Fact]
    public async Task AssignJob_PersistsNotificationForMechanicOnly()
    {
        Guid mechanicId, otherId, jobId;
        await using (var setup = fixture.CreateContext())
        {
            mechanicId = await CreateUserAsync(setup, "p6-mech@test.local", Role.Employee);
            otherId = await CreateUserAsync(setup, "p6-other@test.local", Role.Employee);
            var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(scopeAdmin))
                .Handle(new CreateJobCommand("Fork service", null, "Specialized Rockhopper", null, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
        }

        await using (var ctx = fixture.CreateContext())
        {
            await new AssignMechanicCommandHandler(ctx, TestNotifiers.For(ctx), TestCurrentUser.SuperAdmin(Guid.NewGuid()))
                .Handle(new AssignMechanicCommand(jobId, mechanicId), default);
        }

        await using var verify = fixture.CreateContext();

        var mine = await new GetNotificationsQueryHandler(verify, TestCurrentUser.Employee(mechanicId))
            .Handle(new GetNotificationsQuery(), default);
        mine.Items.Should().Contain(n => n.Type == "JobAssigned" && !n.IsRead);

        var others = await new GetNotificationsQueryHandler(verify, TestCurrentUser.Employee(otherId))
            .Handle(new GetNotificationsQuery(), default);
        others.Items.Should().BeEmpty();
    }

    // Mark-read is owner-scoped: another user gets a 404, never access to the row.
    [Fact]
    public async Task MarkRead_OwnNotificationOnly()
    {
        Guid mechanicId, otherId;
        Guid notificationId;
        await using (var setup = fixture.CreateContext())
        {
            mechanicId = await CreateUserAsync(setup, "p6-read-owner@test.local", Role.Employee);
            otherId = await CreateUserAsync(setup, "p6-read-other@test.local", Role.Employee);
            await TestNotifiers.For(setup).NotifyAsync(mechanicId, "JobAssigned", "Test message", default);
            notificationId = setup.Notifications.Single(n => n.RecipientId == mechanicId).Id;
        }

        await using (var ctx = fixture.CreateContext())
        {
            var foreign = () => new MarkNotificationReadCommandHandler(ctx, TestCurrentUser.Employee(otherId))
                .Handle(new MarkNotificationReadCommand(notificationId), default);
            await foreign.Should().ThrowAsync<NotFoundException>();
        }

        await using (var ctx = fixture.CreateContext())
        {
            await new MarkNotificationReadCommandHandler(ctx, TestCurrentUser.Employee(mechanicId))
                .Handle(new MarkNotificationReadCommand(notificationId), default);
        }

        await using var verify = fixture.CreateContext();
        var unread = await new GetNotificationsQueryHandler(verify, TestCurrentUser.Employee(mechanicId))
            .Handle(new GetNotificationsQuery(UnreadOnly: true), default);
        unread.Items.Should().NotContain(n => n.Id == notificationId);
    }

    // Rule N2: a job created in one department notifies the SuperAdmin and that department's own
    // admin — never the other department's admin (dept isolation holds for the cockpit feed too).
    [Fact]
    public async Task CreateJob_NotifiesSuperAdminAndOwnDepartmentAdmin_NotOtherDepartment_N2()
    {
        Guid superAdminId, mechanicsAdminId, washAdminId;
        await using (var setup = fixture.CreateContext())
        {
            superAdminId = await CreateSuperAdminAsync(setup, "n2-super@test.local");
            mechanicsAdminId = await CreateDepartmentAdminAsync(setup, "n2-mech-admin@test.local", DepartmentConfiguration.MechanicsId);
            washAdminId = await CreateDepartmentAdminAsync(setup, "n2-wash-admin@test.local", DepartmentConfiguration.BikeWashId);
        }

        var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        await using (var ctx = fixture.CreateContext(scopeAdmin))
        {
            await new CreateJobCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestDepartmentScope.For(scopeAdmin))
                .Handle(new CreateJobCommand("N2 test job", null, "Test bike", null, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
        }

        await using var verify = fixture.CreateContext();
        (await new GetNotificationsQueryHandler(verify, TestCurrentUser.SuperAdmin(superAdminId))
            .Handle(new GetNotificationsQuery(), default)).Items.Should().Contain(n => n.Type == "JobCreated");
        (await new GetNotificationsQueryHandler(verify, TestCurrentUser.DepartmentAdmin(mechanicsAdminId, DepartmentConfiguration.MechanicsId))
            .Handle(new GetNotificationsQuery(), default)).Items.Should().Contain(n => n.Type == "JobCreated");
        (await new GetNotificationsQueryHandler(verify, TestCurrentUser.DepartmentAdmin(washAdminId, DepartmentConfiguration.BikeWashId))
            .Handle(new GetNotificationsQuery(), default)).Items.Should().NotContain(n => n.Type == "JobCreated");
    }

    // Rule N1: a bill marked Paid in one department notifies the SuperAdmin and that department's
    // own admin only.
    [Fact]
    public async Task SetBillPaid_NotifiesSuperAdminAndOwnDepartmentAdmin_NotOtherDepartment_N1()
    {
        Guid superAdminId, mechanicsAdminId, washAdminId, billId;
        await using (var setup = fixture.CreateContext())
        {
            superAdminId = await CreateSuperAdminAsync(setup, "n1-super@test.local");
            mechanicsAdminId = await CreateDepartmentAdminAsync(setup, "n1-mech-admin@test.local", DepartmentConfiguration.MechanicsId);
            washAdminId = await CreateDepartmentAdminAsync(setup, "n1-wash-admin@test.local", DepartmentConfiguration.BikeWashId);

            var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            var jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(scopeAdmin))
                .Handle(new CreateJobCommand("N1 test job", null, "Test bike", null, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
            billId = await new CreateBillCommandHandler(setup, scopeAdmin, TimeProvider.System).Handle(new CreateBillCommand(jobId), default);
            await new AddLineItemCommandHandler(setup, scopeAdmin)
                .Handle(new AddLineItemCommand(billId, LineItemType.Labor, "Service", 1m, 100m), default);
        }

        await using (var ctx = fixture.CreateContext())
        {
            await new SetBillPaidCommandHandler(ctx, TimeProvider.System, TestNotifiers.For(ctx), TestCurrentUser.SuperAdmin(Guid.NewGuid()))
                .Handle(new SetBillPaidCommand(billId, true), default);
        }

        await using var verify = fixture.CreateContext();
        (await new GetNotificationsQueryHandler(verify, TestCurrentUser.SuperAdmin(superAdminId))
            .Handle(new GetNotificationsQuery(), default)).Items.Should().Contain(n => n.Type == "BillPaid");
        (await new GetNotificationsQueryHandler(verify, TestCurrentUser.DepartmentAdmin(mechanicsAdminId, DepartmentConfiguration.MechanicsId))
            .Handle(new GetNotificationsQuery(), default)).Items.Should().Contain(n => n.Type == "BillPaid");
        (await new GetNotificationsQueryHandler(verify, TestCurrentUser.DepartmentAdmin(washAdminId, DepartmentConfiguration.BikeWashId))
            .Handle(new GetNotificationsQuery(), default)).Items.Should().NotContain(n => n.Type == "BillPaid");
    }

    // Rule N2: only the Completed/Delivered milestones raise a department notification —
    // Received→InProgress is routine board activity, not worth a cross-department alert.
    [Fact]
    public async Task ChangeJobStatus_OnlyNotifiesDepartmentOnMilestoneStatuses_N2()
    {
        Guid superAdminId, jobId;
        await using (var setup = fixture.CreateContext())
        {
            superAdminId = await CreateSuperAdminAsync(setup, "n2-milestone-super@test.local");
            var scopeAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
            jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup), TestDepartmentScope.For(scopeAdmin))
                .Handle(new CreateJobCommand("N2 milestone job", null, "Test bike", null, null, null, null,
                    DepartmentConfiguration.MechanicsId), default);
        }

        var superAdmin = TestCurrentUser.SuperAdmin(Guid.NewGuid());
        await using (var ctx = fixture.CreateContext(superAdmin))
        {
            await new ChangeJobStatusCommandHandler(ctx, superAdmin, TestNotifiers.For(ctx))
                .Handle(new ChangeJobStatusCommand(jobId, JobStatus.InProgress), default);
        }

        await using (var afterInProgress = fixture.CreateContext())
        {
            var mine = await new GetNotificationsQueryHandler(afterInProgress, TestCurrentUser.SuperAdmin(superAdminId))
                .Handle(new GetNotificationsQuery(), default);
            mine.Items.Should().NotContain(n => n.Type == "JobStatusChanged");
        }

        await using (var ctx = fixture.CreateContext(superAdmin))
        {
            await new ChangeJobStatusCommandHandler(ctx, superAdmin, TestNotifiers.For(ctx))
                .Handle(new ChangeJobStatusCommand(jobId, JobStatus.Completed), default);
        }

        await using var verify = fixture.CreateContext();
        (await new GetNotificationsQueryHandler(verify, TestCurrentUser.SuperAdmin(superAdminId))
            .Handle(new GetNotificationsQuery(), default)).Items.Should().Contain(n => n.Type == "JobStatusChanged");
    }
}
