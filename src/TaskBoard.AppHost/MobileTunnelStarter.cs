using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.DevTunnels;
using Microsoft.Extensions.DependencyInjection;

namespace TaskBoard.AppHost;

// Share tunnel startup between Android and iOS, including subsequent app restarts.
internal sealed class MobileTunnelStarter : IDisposable
{
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public async Task StartAsync(IReadOnlyList<DevTunnelResource> tunnels, IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            var commands = services.GetRequiredService<ResourceCommandService>();
            var notifications = services.GetRequiredService<ResourceNotificationService>();
            foreach (var tunnel in tunnels)
            {
                var current = await notifications.WaitForResourceAsync(tunnel.Name, _ => true, cancellationToken);
                var state = current.Snapshot.State?.Text;
                if (state != KnownResourceStates.Running && state != KnownResourceStates.Starting)
                {
                    var result = await commands.ExecuteCommandAsync(tunnel, KnownResourceCommands.StartCommand,
                        cancellationToken);
                    if (!result.Success)
                    {
                        throw new DistributedApplicationException(
                            $"Could not start mobile tunnel '{tunnel.Name}': {result.Message}");
                    }
                }

                // Fail the app's Start command when a tunnel cannot start, instead of waiting forever.
                await notifications.WaitForResourceHealthyAsync(tunnel.Name,
                    WaitBehavior.StopOnResourceUnavailable, cancellationToken);
            }
        }
        finally
        {
            _startLock.Release();
        }
    }

    public void Dispose() => _startLock.Dispose();
}
