using TaskBoard.ClientCore.Interfaces;
using TaskBoard.ClientCore.Resources;
using TaskBoard.Contracts;

namespace TaskBoard.Maui.Services;

public sealed class MauiTaskNavigationService : ITaskNavigationService
{
    public Task ShowEditorAsync(TaskDto? task)
    {
        var parameters = new ShellNavigationQueryParameters();
        if (task is not null)
        {
            parameters["task"] = task;
        }

        return Shell.Current.GoToAsync("editor", parameters);
    }

    public Task CloseEditorAsync() => Shell.Current.GoToAsync("..");

    public Task<bool> ConfirmDeleteAsync(string title) => Shell.Current.DisplayAlertAsync(
        Text.DeleteTitle, string.Format(Text.DeleteQuestion, title), Text.Delete, Text.Cancel);
}
