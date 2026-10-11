using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskBoard.ClientCore.DependencyInjection;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.Maui.Services;
using TaskBoard.Maui.Views;

namespace TaskBoard.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // MAUI 10.0.110 normalizes matching ASPNETCORE_/DOTNET_ keys and rejects duplicates.
        // Prefer generic host settings while building, then restore the process environment.
        var environment = Environment.GetEnvironmentVariables();
        var names = environment.Keys.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicates = environment.Cast<System.Collections.DictionaryEntry>()
            .Where(entry => entry.Key is string key && key.StartsWith("ASPNETCORE_", StringComparison.OrdinalIgnoreCase)
                && names.Contains("DOTNET_" + key["ASPNETCORE_".Length..]))
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
        foreach (var key in duplicates.Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
        try
        {
            return BuildApplication();
        }
        catch (Exception exception)
        {
            // A configuration/DI/telemetry initialization error must still produce a useful window.
            var fallback = CreateNativeBuilder();
            fallback.Services.AddSingleton(new AppStartupState { Error = exception });
#if DEBUG
            fallback.Logging.AddDebug();
#endif
            return fallback.Build();
        }
        finally
        {
            foreach (var (key, value) in duplicates)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static MauiApp BuildApplication()
    {
        var builder = CreateNativeBuilder();
        builder.Services.AddSingleton<AppStartupState>();

        using var settings = typeof(MauiProgram).Assembly.GetManifestResourceStream("TaskBoard.Maui.clientsettings.json")
            ?? throw new InvalidOperationException("Client settings are missing.");
        builder.Configuration.AddJsonStream(settings);
        builder.Configuration.AddEnvironmentVariables();
        builder.AddServiceDefaults();

        // Aspire supplies service endpoints to the MAUI launch. Standalone launches use a conventional URL.
        var hasDiscoveredApi = builder.Configuration.GetSection("services:taskapi").GetChildren().Any();
        var baseAddress = hasDiscoveredApi
            ? new Uri("https+http://taskapi")
            : GetStandaloneAddress(builder.Configuration["TaskBoard:ApiBaseUrl"]);
        builder.Services.AddTaskBoardClient(baseAddress);
        builder.Services.AddSingleton<ITaskNavigationService, MauiTaskNavigationService>();
        builder.Services.AddSingleton<IConnectionStatus, MauiConnectionStatus>();
        builder.Services.AddSingleton<TasksPage>();
        builder.Services.AddSingleton<DiagnosticsPage>();
        builder.Services.AddTransient<TaskEditorPage>();
        builder.Services.AddSingleton<AppShell>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }

    private static MauiAppBuilder CreateNativeBuilder()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });
        builder.Services.AddLogging();
        builder.Logging.AddConsole();
        return builder;
    }

    private static Uri GetStandaloneAddress(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) || address.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("TaskBoard:ApiBaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        return new Uri(address.AbsoluteUri.TrimEnd('/') + "/");
    }
}
