using Microsoft.Extensions.Logging.Abstractions;
using TaskBoard.ClientCore.Resources;
using TaskBoard.ClientCore.Tests.Fakes;
using TaskBoard.ClientCore.ViewModels;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Tests;

public sealed class TaskEditorViewModelTests
{
    private readonly FakeTaskApiClient _api = new();
    private readonly FakeNavigationService _navigation = new();
    private TaskEditorViewModel CreateViewModel() => new(_api, _navigation, NullLogger<TaskEditorViewModel>.Instance);

    [Fact]
    public async Task SaveCreatesTaskAndClosesEditorAsync()
    {
        var viewModel = CreateViewModel();
        viewModel.Title = "  New task  ";
        viewModel.Description = "Details";
        viewModel.IsCompleted = true;
        await viewModel.SaveCommand.ExecuteAsync(null);
        var task = Assert.Single(_api.Tasks);
        Assert.Equal("New task", task.Title);
        Assert.True(task.IsCompleted);
        Assert.Equal(1, _navigation.CloseCount);
    }

    [Fact]
    public async Task SaveUpdatesExistingTaskAsync()
    {
        var task = new TaskDto(Guid.NewGuid(), "Before", null, false, DateTime.UtcNow, DateTime.UtcNow);
        _api.Tasks.Add(task);
        var viewModel = CreateViewModel();
        viewModel.Initialize(task);
        viewModel.Title = "After";
        viewModel.IsCompleted = true;
        await viewModel.SaveCommand.ExecuteAsync(null);
        var updated = Assert.Single(_api.Tasks);
        Assert.Equal(task.Id, updated.Id);
        Assert.Equal("After", updated.Title);
        Assert.True(updated.IsCompleted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidTitleDisablesSaveAndDoesNotCallApiAsync(string title)
    {
        var viewModel = CreateViewModel();
        viewModel.Title = title;
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Empty(_api.Tasks);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public void OversizedInputsDisableSave()
    {
        var viewModel = CreateViewModel();
        viewModel.Title = new string('a', 121);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        viewModel.Title = "Valid";
        viewModel.Description = new string('a', 2001);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        viewModel.Description = "Valid";
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailureKeepsEditorOpenAndPresentsErrorAsync()
    {
        _api.Failure = new HttpRequestException("Save failed");
        var viewModel = CreateViewModel();
        viewModel.Title = "New task";
        await viewModel.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, _navigation.CloseCount);
        Assert.Contains("Save failed", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task CancelStopsPendingSaveAndClosesEditorAsync()
    {
        _api.BeforeRequest = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        var viewModel = CreateViewModel();
        viewModel.Title = "New task";
        var saving = viewModel.SaveCommand.ExecuteAsync(null);
        await viewModel.CancelCommand.ExecuteAsync(null);
        await saving;
        Assert.True(_api.LastToken.IsCancellationRequested);
        Assert.Empty(_api.Tasks);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(1, _navigation.CloseCount);
    }

    [Fact]
    public async Task FailedCloseAfterCreateCanBeRetriedWithoutCreatingADuplicateAsync()
    {
        _navigation.CloseFailure = new InvalidOperationException("Navigation failed");
        var viewModel = CreateViewModel();
        viewModel.Title = "New task";
        await viewModel.SaveCommand.ExecuteAsync(null);
        var created = Assert.Single(_api.Tasks);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);
        Assert.True(viewModel.SaveCommand.CanExecute(null));

        _navigation.CloseFailure = null;
        viewModel.Title = "Updated after failed close";
        await viewModel.SaveCommand.ExecuteAsync(null);
        var updated = Assert.Single(_api.Tasks);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(viewModel.Title, updated.Title);
        Assert.Equal(1, _navigation.CloseCount);
    }

    [Fact]
    public async Task FailedCancelNavigationCanBeRetriedAsync()
    {
        var viewModel = CreateViewModel();
        _navigation.CloseFailure = new InvalidOperationException("Navigation failed");
        await viewModel.CancelCommand.ExecuteAsync(null);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);
        _navigation.CloseFailure = null;
        await viewModel.CancelCommand.ExecuteAsync(null);
        Assert.Equal(1, _navigation.CloseCount);
    }

    [Fact]
    public async Task SaveAndCancelDoNotCloseTwiceWhileNavigationIsPendingAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _navigation.BeforeClose = () => completion.Task;
        var viewModel = CreateViewModel();
        viewModel.Title = "New task";
        var saving = viewModel.SaveCommand.ExecuteAsync(null);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        await viewModel.CancelCommand.ExecuteAsync(null);
        completion.SetResult();
        await saving;
        Assert.Equal(1, _navigation.CloseCount);
        Assert.Single(_api.Tasks);
    }
}
