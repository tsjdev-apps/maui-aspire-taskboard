using Microsoft.Extensions.Logging.Abstractions;
using TaskBoard.ClientCore.Resources;
using TaskBoard.ClientCore.Tests.Fakes;
using TaskBoard.ClientCore.ViewModels;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Tests;

public sealed class TasksViewModelTests
{
    private readonly FakeTaskApiClient _api = new();
    private readonly FakeNavigationService _navigation = new();

    private TasksViewModel CreateViewModel() =>
        new(_api, _navigation, TimeProvider.System, NullLogger<TasksViewModel>.Instance);

    private static TaskDto ExampleTask() => new(Guid.NewGuid(), "Read the guide", "Try it", false,
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task LoadReplacesTasksAndRecordsSynchronizationAsync()
    {
        _api.Tasks.Add(ExampleTask());
        var viewModel = CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Tasks);
        Assert.NotNull(viewModel.LastSynchronizedAt);
        Assert.False(viewModel.IsBusy);
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Tasks);
    }

    [Fact]
    public async Task LoadShowsBusyWhileRequestIsPendingAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _api.BeforeRequest = token => completion.Task.WaitAsync(token);
        var viewModel = CreateViewModel();
        var loading = viewModel.LoadCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.IsRefreshing);
        Assert.False(viewModel.LoadCommand.CanExecute(null));
        completion.SetResult();
        await loading;
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.LoadCommand.CanExecute(null));
    }

    [Fact]
    public async Task PullToRefreshStateResetsAfterFailureAsync()
    {
        _api.Failure = new HttpRequestException("Refresh failed");
        var viewModel = CreateViewModel();
        viewModel.IsRefreshing = true;
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.False(viewModel.IsRefreshing);
        Assert.False(viewModel.IsBusy);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task FailedRefreshPreservesLastSuccessfulStateAsync()
    {
        _api.Tasks.Add(ExampleTask());
        var viewModel = CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        var lastSync = viewModel.LastSynchronizedAt;
        _api.Failure = new HttpRequestException("API unavailable");
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Tasks);
        Assert.Equal(lastSync, viewModel.LastSynchronizedAt);
        Assert.Contains("API unavailable", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task CommandCancellationReachesApiAndRestoresLoadingStateAsync()
    {
        _api.BeforeRequest = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        var viewModel = CreateViewModel();
        var loading = viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.LoadCommand.Cancel();
        await loading;
        Assert.True(_api.LastToken.IsCancellationRequested);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Null(viewModel.LastSynchronizedAt);
    }

    [Fact]
    public async Task CompleteReplacesTaskWithServerResultAsync()
    {
        var task = ExampleTask();
        _api.Tasks.Add(task);
        var viewModel = CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.ToggleCompleteCommand.ExecuteAsync(task);
        Assert.True(Assert.Single(viewModel.Tasks).IsCompleted);
        Assert.True(_api.Tasks[0].IsCompleted);
    }

    [Fact]
    public async Task DeleteRemovesTaskOnlyAfterSuccessAsync()
    {
        var task = ExampleTask();
        _api.Tasks.Add(task);
        var viewModel = CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        _api.Failure = new HttpRequestException("Delete failed");
        await viewModel.DeleteCommand.ExecuteAsync(task);
        Assert.Single(viewModel.Tasks);
        _api.Failure = null;
        await viewModel.DeleteCommand.ExecuteAsync(task);
        Assert.Empty(viewModel.Tasks);
        Assert.Empty(_api.Tasks);
    }

    [Fact]
    public async Task DeclinedDeletionKeepsTaskAsync()
    {
        var task = ExampleTask();
        _api.Tasks.Add(task);
        _navigation.ConfirmDeletion = false;
        var viewModel = CreateViewModel();
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.DeleteCommand.ExecuteAsync(task);
        Assert.Single(viewModel.Tasks);
    }

    [Fact]
    public async Task AddAndEditCommandsNavigateAsync()
    {
        var viewModel = CreateViewModel();
        await viewModel.AddCommand.ExecuteAsync(null);
        Assert.True(_navigation.EditorOpened);
        Assert.Null(_navigation.EditedTask);
        var task = ExampleTask();
        await viewModel.EditCommand.ExecuteAsync(task);
        Assert.Equal(task, _navigation.EditedTask);
    }

    [Fact]
    public async Task NavigationAndConfirmationFailuresStayInTheViewModelAndCanBeRetriedAsync()
    {
        var viewModel = CreateViewModel();
        var task = ExampleTask();
        _api.Tasks.Add(task);
        await viewModel.LoadCommand.ExecuteAsync(null);
        _navigation.OpenFailure = new InvalidOperationException("Sensitive internal route details");
        await viewModel.AddCommand.ExecuteAsync(null);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
        await viewModel.EditCommand.ExecuteAsync(task);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);

        _navigation.ConfirmationFailure = new InvalidOperationException("Dialog unavailable");
        await viewModel.DeleteCommand.ExecuteAsync(task);
        Assert.Single(viewModel.Tasks);
        Assert.Single(_api.Tasks);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);

        _navigation.OpenFailure = null;
        await viewModel.AddCommand.ExecuteAsync(null);
        Assert.True(_navigation.EditorOpened);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task UnexpectedLoadFailureKeepsDataAndRestoresRefreshStateAsync()
    {
        var viewModel = CreateViewModel();
        _api.Tasks.Add(ExampleTask());
        await viewModel.LoadCommand.ExecuteAsync(null);
        var synchronizedAt = viewModel.LastSynchronizedAt;
        _api.Failure = new InvalidOperationException("Service discovery configuration failed");
        viewModel.IsRefreshing = true;
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Tasks);
        Assert.Equal(synchronizedAt, viewModel.LastSynchronizedAt);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);
        Assert.False(viewModel.IsRefreshing);
        Assert.False(viewModel.IsBusy);
    }
}
