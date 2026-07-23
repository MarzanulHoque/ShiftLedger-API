using MediatR;
using ShiftLedger.Application.Notifications;

namespace ShiftLedger.Api.BackgroundServices;

// Rule N3: v1 has no scheduler (Hangfire is deferred — see docs/13 §7), so this is a lightweight
// in-process timer. The actual sweep logic lives in RunOverdueUnpaidAlertSweepCommandHandler
// (Application layer, directly unit/integration-testable); this class only owns the timer and a
// DI scope per tick, since IAppDbContext/INotifier are scoped but a BackgroundService is a singleton.
public class OverdueUnpaidAlertHostedService(
    IServiceScopeFactory scopeFactory, ILogger<OverdueUnpaidAlertHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<ISender>();
                await mediator.Send(new RunOverdueUnpaidAlertSweepCommand(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Overdue/unpaid alert sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
