namespace VHSmart_Api.Shared.Infrastructure.Subscriptions;

// Daily runner for the D-11 7-day warning (a background service, never a GET). It runs once
// at startup so a restart cannot silently skip a day, then every 24 hours; a failing run is
// logged and the loop keeps going (an exception escaping ExecuteAsync would stop the host).
public class SubscriptionExpiryWarningService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionExpiryWarningService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            // One scope per run: the processor is scoped and needs its own VHSmartDbContext.
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<SubscriptionExpiryWarningProcessor>();

            var warned = await processor.ProcessAsync(DateTime.UtcNow.Date, stoppingToken);
            if (warned > 0)
                logger.LogInformation(
                    "Subscription expiry warning run warned about {Count} subscription period(s)",
                    warned);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down; nothing to log.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Subscription expiry warning run failed");
        }
    }
}
