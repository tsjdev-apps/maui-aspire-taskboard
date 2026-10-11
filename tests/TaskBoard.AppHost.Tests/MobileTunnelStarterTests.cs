using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.DevTunnels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaskBoard.AppHost;

namespace TaskBoard.AppHost.Tests;

public sealed class MobileTunnelStarterTests
{
    [Fact]
    public async Task StartsBothTunnelsOnDemandAndReusesThemForConcurrentLaunches()
    {
        await using var fixture = new TunnelFixture();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await fixture.InitializeAsync();
        Assert.Empty(fixture.Starts);

        await Task.WhenAll(fixture.StartAsync(timeout.Token), fixture.StartAsync(timeout.Token));

        Assert.Equal(["api-tunnel", "otlp-tunnel"], fixture.Starts);
    }

    [Fact]
    public async Task RestartsAStoppedTunnelWithoutRestartingTheOtherTunnel()
    {
        await using var fixture = new TunnelFixture();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await fixture.InitializeAsync();
        await fixture.StartAsync(timeout.Token);
        await fixture.SetStateAsync(fixture.Tunnels[0], KnownResourceStates.NotStarted);

        await fixture.StartAsync(timeout.Token);

        Assert.Equal(["api-tunnel", "otlp-tunnel", "api-tunnel"], fixture.Starts);
    }

    [Fact]
    public async Task ReportsStartFailureAndAllowsRetryAfterFixingTheDependency()
    {
        await using var fixture = new TunnelFixture { FailStart = true };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await fixture.InitializeAsync();

        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() => fixture.StartAsync(timeout.Token));
        Assert.Contains("api-tunnel", error.Message);
        Assert.Contains("devtunnel unavailable", error.Message);

        fixture.FailStart = false;
        await fixture.StartAsync(timeout.Token);
        Assert.Equal(["api-tunnel", "api-tunnel", "otlp-tunnel"], fixture.Starts);
    }

    [Fact]
    public async Task FailedTunnelReadinessDoesNotHangAndAllowsRetryAsync()
    {
        await using var fixture = new TunnelFixture { FailReadiness = true };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await fixture.InitializeAsync();

        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() => fixture.StartAsync(timeout.Token));
        Assert.Contains("api-tunnel", error.Message);
        Assert.False(timeout.IsCancellationRequested);

        fixture.FailReadiness = false;
        await fixture.SetStateAsync(fixture.Tunnels[0], KnownResourceStates.NotStarted);
        await fixture.StartAsync(timeout.Token);
        Assert.Equal(["api-tunnel", "api-tunnel", "otlp-tunnel"], fixture.Starts);
    }

    private sealed class TunnelFixture : IAsyncDisposable
    {
        private readonly DistributedApplication _app;
        private readonly ResourceNotificationService _notifications;
        private readonly IHostedService _healthWatcher;
        private readonly MobileTunnelStarter _starter = new();
        public DevTunnelResource[] Tunnels { get; }
        public List<string> Starts { get; } = [];
        public bool FailStart { get; set; }
        public bool FailReadiness { get; set; }

        public TunnelFixture()
        {
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                DisableDashboard = true,
                AllowUnsecuredTransport = true,
                ProjectDirectory = Directory.GetCurrentDirectory()
            });
            Tunnels = [CreateTunnel("api-tunnel"), CreateTunnel("otlp-tunnel")];
            foreach (var tunnel in Tunnels)
            {
                builder.AddResource(tunnel).WithExplicitStart()
                    .WithCommand(KnownResourceCommands.StartCommand, "Start", async _ =>
                    {
                        Starts.Add(tunnel.Name);
                        if (FailStart)
                        {
                            return CommandResults.Failure("devtunnel unavailable");
                        }
                        await SetStateAsync(tunnel, FailReadiness
                            ? KnownResourceStates.FailedToStart : KnownResourceStates.Running);
                        return CommandResults.Success();
                    });
            }
            var healthWatcherType = builder.Services.Single(descriptor =>
                descriptor.ServiceType.Name == "ResourceHealthCheckService").ServiceType;
            _app = builder.Build();
            _notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
            // Run Aspire's real readiness watcher; keep DCP, the dashboard and external processes stopped.
            _healthWatcher = (IHostedService)_app.Services.GetRequiredService(healthWatcherType);
        }

        // Fake command callbacks exercise orchestration without installing a CLI or opening a tunnel.
        private static DevTunnelResource CreateTunnel(string name) =>
            new(name, name, "unused", Directory.GetCurrentDirectory(), new DevTunnelOptions());

        public async Task InitializeAsync()
        {
            await _healthWatcher.StartAsync(CancellationToken.None);
            foreach (var tunnel in Tunnels)
            {
                await SetStateAsync(tunnel, KnownResourceStates.NotStarted);
            }
        }

        public Task SetStateAsync(DevTunnelResource tunnel, string state) =>
            _notifications.PublishUpdateAsync(tunnel, snapshot => snapshot with { State = new(state, null) });

        public Task StartAsync(CancellationToken cancellationToken) =>
            _starter.StartAsync(Tunnels, _app.Services, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await _healthWatcher.StopAsync(CancellationToken.None);
            _app.Dispose();
            _starter.Dispose();
        }
    }
}
