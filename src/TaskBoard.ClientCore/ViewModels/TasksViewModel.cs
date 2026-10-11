using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.ViewModels;

public sealed partial class TasksViewModel(ITaskApiClient apiClient, ITaskNavigationService navigation,
    TimeProvider timeProvider, ILogger<TasksViewModel> logger) : OperationViewModel(logger)
{
    public ObservableCollection<TaskDto> Tasks { get; } = [];

    [ObservableProperty]
    private DateTimeOffset? _lastSynchronizedAt;

    [ObservableProperty]
    private bool _isRefreshing;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteAsync(async token =>
            {
                var tasks = await apiClient.GetTasksAsync(token);
                token.ThrowIfCancellationRequested();
                Tasks.Clear();
                foreach (var task in tasks)
                {
                    Tasks.Add(task);
                }

                LastSynchronizedAt = timeProvider.GetUtcNow().ToLocalTime();
            }, cancellationToken);
        }
        finally
        {
            // RefreshView starts its own gesture state. Setting it true during Load would reenter the command.
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task AddAsync() => ExecuteAsync(_ => navigation.ShowEditorAsync(null), CancellationToken.None);

    [RelayCommand]
    private Task EditAsync(TaskDto task) => ExecuteAsync(_ => navigation.ShowEditorAsync(task), CancellationToken.None);

    [RelayCommand]
    private Task ToggleCompleteAsync(TaskDto task, CancellationToken cancellationToken) =>
        ExecuteAsync(async token =>
        {
            var updated = await apiClient.UpdateTaskAsync(task.Id,
                new UpdateTaskRequest(task.Title, task.Description, !task.IsCompleted), token);
            var index = Tasks.IndexOf(task);
            if (index >= 0)
            {
                Tasks[index] = updated;
            }

            LastSynchronizedAt = timeProvider.GetUtcNow().ToLocalTime();
        }, cancellationToken);

    [RelayCommand]
    private Task DeleteAsync(TaskDto task, CancellationToken cancellationToken) => ExecuteAsync(async token =>
    {
        if (!await navigation.ConfirmDeleteAsync(task.Title))
        {
            return;
        }

        token.ThrowIfCancellationRequested();
        await apiClient.DeleteTaskAsync(task.Id, token);
        Tasks.Remove(task);
        LastSynchronizedAt = timeProvider.GetUtcNow().ToLocalTime();
    }, cancellationToken);
}
