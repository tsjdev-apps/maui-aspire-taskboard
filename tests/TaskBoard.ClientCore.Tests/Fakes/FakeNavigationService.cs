using TaskBoard.ClientCore.Interfaces;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Tests.Fakes;

internal sealed class FakeNavigationService : ITaskNavigationService
{
    public bool ConfirmDeletion { get; set; } = true;
    public bool EditorOpened { get; private set; }
    public TaskDto? EditedTask { get; private set; }
    public int CloseCount { get; private set; }
    public Exception? OpenFailure { get; set; }
    public Exception? CloseFailure { get; set; }
    public Exception? ConfirmationFailure { get; set; }
    public Func<Task>? BeforeClose { get; set; }
    public Task ShowEditorAsync(TaskDto? task)
    {
        if (OpenFailure is not null) { throw OpenFailure; }
        EditorOpened = true;
        EditedTask = task;
        return Task.CompletedTask;
    }

    public async Task CloseEditorAsync()
    {
        if (CloseFailure is not null) { throw CloseFailure; }
        if (BeforeClose is not null) { await BeforeClose(); }
        CloseCount++;
    }

    public Task<bool> ConfirmDeleteAsync(string title) => ConfirmationFailure is null
        ? Task.FromResult(ConfirmDeletion) : Task.FromException<bool>(ConfirmationFailure);
}
