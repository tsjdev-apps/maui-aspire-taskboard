using TaskBoard.ClientCore.ViewModels;
using TaskBoard.Contracts;

namespace TaskBoard.Maui.Views;

public partial class TaskEditorPage : ContentPage, IQueryAttributable
{
    private readonly TaskEditorViewModel _viewModel;

    public TaskEditorPage(TaskEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _viewModel.RunSafely(() => _viewModel.Initialize(query.TryGetValue("task", out var task) ? task as TaskDto : null));

    protected override void OnDisappearing()
    {
        _viewModel.SaveCommand.Cancel();
        base.OnDisappearing();
    }
}
