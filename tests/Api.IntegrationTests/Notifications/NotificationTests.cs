using FluentAssertions;
using ShiftLedger.Application.Common.Exceptions;
using ShiftLedger.Application.Jobs;
using ShiftLedger.Application.Notifications;
using ShiftLedger.Application.Users;
using ShiftLedger.Domain.Enums;
using ShiftLedger.Infrastructure.Persistence;
using ShiftLedger.Infrastructure.Security;
using Xunit;

namespace ShiftLedger.Api.IntegrationTests.Notifications;

[Collection("Database")]
public class NotificationTests(IntegrationTestFixture fixture)
{
    private static async Task<Guid> CreateUserAsync(AppDbContext ctx, string email, Role role) =>
        await new CreateUserCommandHandler(ctx, new PasswordHasher())
            .Handle(new CreateUserCommand("Test User", email, "Secret#123", role, null), default);

    // Assigning a job persists a JobAssigned notification for the mechanic — and only for them.
    [Fact]
    public async Task AssignJob_PersistsNotificationForMechanicOnly()
    {
        Guid mechanicId, otherId, jobId;
        await using (var setup = fixture.CreateContext())
        {
            mechanicId = await CreateUserAsync(setup, "p6-mech@test.local", Role.Employee);
            otherId = await CreateUserAsync(setup, "p6-other@test.local", Role.Employee);
            jobId = await new CreateJobCommandHandler(setup, TimeProvider.System, TestNotifiers.For(setup))
                .Handle(new CreateJobCommand("Fork service", null, "Specialized Rockhopper", null, null, null, null), default);
        }

        await using (var ctx = fixture.CreateContext())
        {
            await new AssignMechanicCommandHandler(ctx, TestNotifiers.For(ctx))
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
}
