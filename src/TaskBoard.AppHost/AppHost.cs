using Aspire.Hosting.DevTunnels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TaskBoard.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
var api = builder.AddProject<Projects.TaskBoard_Api>("taskapi").WithHttpHealthCheck("/health");
var maui = builder.AddMauiProject("taskboard", "../TaskBoard.Maui/TaskBoard.Maui.csproj");

if (OperatingSystem.IsWindows())
{
    maui.AddWindowsDevice().WithReference(api);
}
if (OperatingSystem.IsMacOS())
{
    maui.AddMacCatalystDevice().WithReference(api);
}

var prepareMobileTunnels = builder.Configuration.GetValue<bool>("TaskBoard:EnableMobileTargets");
if (prepareMobileTunnels && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Mobile Dev Tunnels are only supported in Development.");
}

// Mobile targets are always visible in Development. Public tunnels start on demand.
using var tunnelStarter = new MobileTunnelStarter();
if (builder.Environment.IsDevelopment())
{

    var tunnel = builder.AddDevTunnel("taskapi-tunnel")
        .WithAnonymousAccess()
        .WithReference(api.GetEndpoint("https"));

    // The hosting integration passes this to adb -s: use a running emulator's serial.
    var emulatorId = builder.Configuration["TaskBoard:AndroidEmulatorId"];
    var android = maui.AddAndroidEmulator("taskboard-android", string.IsNullOrWhiteSpace(emulatorId) ? null : emulatorId)
        .WithOtlpDevTunnel()
        .WithReference(api, tunnel);

    // WithOtlpDevTunnel creates the shared telemetry tunnel in the application model.
    var mobileTunnels = builder.Resources.OfType<DevTunnelResource>().ToArray();
    if (!prepareMobileTunnels)
    {
        foreach (var mobileTunnel in mobileTunnels)
        {
            builder.CreateResourceBuilder(mobileTunnel).WithExplicitStart();
        }
    }

    android.OnBeforeResourceStarted((_, e, ct) => tunnelStarter.StartAsync(mobileTunnels, e.Services, ct));
    if (OperatingSystem.IsMacOS())
    {
        maui.AddiOSSimulator().WithOtlpDevTunnel().WithReference(api, tunnel)
            .OnBeforeResourceStarted((_, e, ct) => tunnelStarter.StartAsync(mobileTunnels, e.Services, ct));
    }
}

builder.Build().Run();
