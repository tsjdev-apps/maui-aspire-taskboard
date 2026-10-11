using TaskBoard.ClientCore.ViewModels;

namespace TaskBoard.Maui.Views;

public partial class TasksPage : ContentPage
{
    private readonly TasksViewModel _viewModel;
    private int _appearanceVersion;

    public TasksPage(TasksViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var version = ++_appearanceVersion;
        // Defer until the native UI dispatcher is ready, and let a canceled load finish before reloading.
        _viewModel.RunSafely(() => Dispatcher.Dispatch(async () => await _viewModel.RunSafelyAsync(async () =>
        {
            if (_viewModel.LoadCommand.ExecutionTask is { IsCompleted: false } pending)
            {
                await pending;
            }

            if (version == _appearanceVersion)
            {
                await _viewModel.LoadCommand.ExecuteAsync(null);
            }
        })));
    }

    protected override void OnDisappearing()
    {
        _appearanceVersion++;
        _viewModel.LoadCommand.Cancel();
        _viewModel.ToggleCompleteCommand.Cancel();
        _viewModel.DeleteCommand.Cancel();
        base.OnDisappearing();
    }
}
