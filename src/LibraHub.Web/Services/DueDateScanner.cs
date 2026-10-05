namespace LibraHub.Services;

/// <summary>
/// Background job (F3 + F5): every 15 minutes expire stale holds and run the due-soon/overdue scanner.
/// Staff can also trigger it from the notification dashboard ("Run scan now").
/// </summary>
public class DueDateScanner(IServiceScopeFactory scopes, ILogger<DueDateScanner> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var holds = scope.ServiceProvider.GetRequiredService<ItemLifecycleService>();
                var notify = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var expired = await holds.ExpireHoldsAsync();
                var r = await notify.RunScanAsync();
                log.LogInformation("Scanner: {Expired} hold(s) expired, {Due} due-soon, {Over} overdue notices.", expired, r.DueSoon, r.Overdue);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Scanner run failed");
            }
            await Task.Delay(TimeSpan.FromMinutes(15), ct);
        }
    }
}
