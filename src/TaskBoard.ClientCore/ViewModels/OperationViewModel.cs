using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using TaskBoard.ClientCore.Resources;

namespace TaskBoard.ClientCore.ViewModels;

/// <summary>Shared loading, cancellation, and recoverable error presentation for UI operations.</summary>
public abstract partial class OperationViewModel(ILogger logger) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    protected async Task<bool> ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            return await RunSafelyAsync(() => operation(cancellationToken), cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Protects awaited UI callbacks, including navigation, without changing an ongoing busy state.</summary>
    public async Task<bool> RunSafelyAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        try
        {
            await operation();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception)
        {
            ReportError(exception);
            return false;
        }
    }

    /// <summary>Protects synchronous native events and property refreshes.</summary>
    public bool RunSafely(Action operation)
    {
        try
        {
            operation();
            return true;
        }
        catch (Exception exception)
        {
            ReportError(exception);
            return false;
        }
    }

    private void ReportError(Exception exception)
    {
        var expected = exception is HttpRequestException or TimeoutRejectedException or OperationCanceledException
            or System.Text.Json.JsonException or IOException or BrokenCircuitException or RateLimiterRejectedException;
        logger.Log(expected ? LogLevel.Warning : LogLevel.Error, exception, "UI operation failed");
        ErrorMessage = exception switch
        {
            TimeoutRejectedException or OperationCanceledException => Text.RequestTimedOut,
            BrokenCircuitException or RateLimiterRejectedException => Text.ServiceTemporarilyUnavailable,
            System.Text.Json.JsonException => Text.InvalidApiResponse,
            HttpRequestException or IOException => $"{Text.RequestFailed} {exception.Message}",
            _ => Text.UnexpectedError
        };
    }
}
