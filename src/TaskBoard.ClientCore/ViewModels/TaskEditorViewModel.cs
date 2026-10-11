using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.ClientCore.Resources;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.ViewModels;

public sealed partial class TaskEditorViewModel(ITaskApiClient apiClient, ITaskNavigationService navigation,
    ILogger<TaskEditorViewModel> logger) : OperationViewModel(logger)
{
    private Guid? _taskId;
    private bool _isClosed;
    private bool _isClosing;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? _description;

    [ObservableProperty]
    private bool _isCompleted;

    public void Initialize(TaskDto? task)
    {
        _taskId = task?.Id;
        _isClosed = false;
        _isClosing = false;
        Title = task?.Title ?? string.Empty;
        Description = task?.Description;
        IsCompleted = task?.IsCompleted ?? false;
        ErrorMessage = null;
    }

    private bool CanSave() => TaskValidation.Validate(Title, Description).Count == 0 && !IsBusy && !_isClosed && !_isClosing;

    [RelayCommand(CanExecute = nameof(CanSave), IncludeCancelCommand = true)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (TaskValidation.Validate(Title, Description).Count > 0)
        {
            ErrorMessage = Text.ValidationError;
            return;
        }

        var saved = await ExecuteAsync(async token =>
        {
            if (_taskId is { } id)
            {
                await apiClient.UpdateTaskAsync(id, new UpdateTaskRequest(Title.Trim(), Description, IsCompleted), token);
            }
            else
            {
                var created = await apiClient.CreateTaskAsync(new CreateTaskRequest(Title.Trim(), Description, IsCompleted), token);
                // A failed close must not turn the next Save into a duplicate POST.
                _taskId = created.Id;
            }
        }, cancellationToken);
        SaveCommand.NotifyCanExecuteChanged();
        if (saved)
        {
            await CloseEditorOnceAsync();
        }
    }

    [RelayCommand]
    private Task CancelAsync() => RunSafelyAsync(async () =>
    {
        if (IsBusy)
        {
            SaveCommand.Cancel();
            if (SaveCommand.ExecutionTask is { } operation)
            {
                await operation;
            }
        }

        await CloseEditorOnceAsync();
    });

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task CloseEditorOnceAsync()
    {
        if (_isClosed || _isClosing)
        {
            return;
        }

        _isClosing = true;
        SaveCommand.NotifyCanExecuteChanged();
        try
        {
            _isClosed = await RunSafelyAsync(navigation.CloseEditorAsync);
        }
        finally
        {
            _isClosing = false;
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
