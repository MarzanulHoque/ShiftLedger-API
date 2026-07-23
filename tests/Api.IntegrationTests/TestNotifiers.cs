using Microsoft.Extensions.Logging.Abstractions;
using ShiftLedger.Application.Common.Interfaces;
using ShiftLedger.Application.Notifications;
using ShiftLedger.Infrastructure.Persistence;

namespace ShiftLedger.Api.IntegrationTests;

// The real Notifier (rows persist, so notification behavior is testable) with the SignalR leg
// swapped for a no-op — tests have no hub.
public static class TestNotifiers
{
    public static INotifier For(AppDbContext ctx) =>
        new Notifier(ctx, new NoopPusher(), TimeProvider.System, NullLogger<Notifier>.Instance);

    private sealed class NoopPusher : IRealtimePusher
    {
        public Task PushAsync(Guid recipientId, NotificationDto notification, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task PushToDepartmentAsync(Guid departmentId, NotificationDto notification, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
