using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.ClientCore.Services;
using TaskBoard.ClientCore.ViewModels;

namespace TaskBoard.ClientCore.DependencyInjection;

public static class ClientServiceCollectionExtensions
{
    /// <summary>GET requests retry twice; write operations never retry automatically.</summary>
    public static IServiceCollection AddTaskBoardClient(this IServiceCollection services, Uri baseAddress,
        Action<HttpStandardResilienceOptions>? configureResilience = null)
    {
        var http = services.AddHttpClient<ITaskApiClient, TaskApiClient>(client =>
        {
            client.BaseAddress = baseAddress;
        });
        http.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.BackoffType = DelayBackoffType.Constant;
            options.Retry.UseJitter = false;
            options.Retry.DisableFor(HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch);
            configureResilience?.Invoke(options);
        });
        // The standard handler disables HttpClient.Timeout. Set this afterward to also bound
        // response-body buffering, which takes place outside the handler's timeout scope.
        http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(12));

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<TasksViewModel>();
        services.AddTransient<TaskEditorViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        return services;
    }
}
