using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace TaskBoard.MauiServiceDefaults;

internal sealed class OpenTelemetryInitializer : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        _ = services.GetService<MeterProvider>();
        _ = services.GetService<TracerProvider>();
        _ = services.GetService<LoggerProvider>();
    }
}
