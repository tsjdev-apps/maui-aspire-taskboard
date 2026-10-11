using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Interfaces;

public interface ITaskNavigationService
{
    Task ShowEditorAsync(TaskDto? task);
    Task CloseEditorAsync();
    Task<bool> ConfirmDeleteAsync(string title);
}
