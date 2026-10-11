using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.ClientCore.Resources;

namespace TaskBoard.ClientCore.ViewModels;

public sealed partial class DiagnosticsViewModel(ITaskApiClient apiClient, IConnectionStatus connection,
    ILogger<DiagnosticsViewModel> logger) : OperationViewModel(logger)
{
    [ObservableProperty]
    private double _durationMilliseconds;

    [ObservableProperty]
    private string? _resultMessage;

    [ObservableProperty]
    private bool _isConnected;

    public void RefreshConnection() => RunSafely(UpdateConnection);

    private void UpdateConnection()
    {
        IsConnected = false;
        IsConnected = connection.IsConnected;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task NormalAsync(CancellationToken cancellationToken) =>
        MeasureAsync(async token => { await apiClient.GetTasksAsync(token); }, cancellationToken);

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SlowAsync(CancellationToken cancellationToken) =>
        MeasureAsync(token => apiClient.RunSlowRequestAsync(1500, token), cancellationToken);

    [RelayCommand(IncludeCancelCommand = true)]
    private Task TimeoutAsync(CancellationToken cancellationToken) =>
        MeasureAsync(token => apiClient.RunSlowRequestAsync(5000, token), cancellationToken);

    [RelayCommand(IncludeCancelCommand = true)]
    private Task FailureAsync(CancellationToken cancellationToken) =>
        MeasureAsync(apiClient.RunFailureRequestAsync, cancellationToken);

    private async Task MeasureAsync(Func<CancellationToken, Task> request, CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        ResultMessage = null;
        var timer = Stopwatch.StartNew();
        var success = await ExecuteAsync(async token =>
        {
            UpdateConnection();
            await request(token);
        }, cancellationToken);
        DurationMilliseconds = timer.Elapsed.TotalMilliseconds;
        ResultMessage = success ? Text.RequestSucceeded
            : cancellationToken.IsCancellationRequested ? Text.RequestCanceled : Text.RequestFailed;
    }
}
