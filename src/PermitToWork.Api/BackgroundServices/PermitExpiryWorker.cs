using PermitToWork.Application.Permits;

namespace PermitToWork.Api.BackgroundServices;

/// <summary>
/// Runs the expiry sweep on a timer.
/// <para>
/// Deliberately dull. It owns no rules — it decides when to ask, and
/// <see cref="IPermitExpiryService"/> decides what happens, which in turn asks each permit.
/// A background job that knew about permit statuses would be a second place the lifecycle
/// lives.
/// </para>
/// </summary>
public sealed class PermitExpiryWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PermitExpiryWorker> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(
        configuration.GetValue("PermitExpiry:IntervalMinutes", 15));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Permit expiry sweep will run every {Minutes} minutes.", _interval.TotalMinutes);

        // PeriodicTimer rather than Task.Delay in a loop: it does not drift, and it stops
        // cleanly on the cancellation token when the host shuts down.
        using var timer = new PeriodicTimer(_interval);

        // Once at startup, so a machine that has been off over a weekend does not wait a
        // further quarter of an hour before noticing.
        await SweepAsync(stoppingToken);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A new scope per sweep: the DbContext is scoped, and holding one for the life
            // of the process would accumulate every entity it ever tracked.
            await using var scope = scopeFactory.CreateAsyncScope();
            var expiry = scope.ServiceProvider.GetRequiredService<IPermitExpiryService>();

            var expired = await expiry.ExpireElapsedAsync(cancellationToken);

            if (expired > 0)
            {
                logger.LogInformation("Expired {Count} permit(s) whose validity had passed.", expired);
            }
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // A failed sweep must not take the host down with it. The database being
            // briefly unreachable is a reason to try again in fifteen minutes, not a reason
            // to stop the API.
            logger.LogError(failure, "The permit expiry sweep failed. It will run again shortly.");
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
