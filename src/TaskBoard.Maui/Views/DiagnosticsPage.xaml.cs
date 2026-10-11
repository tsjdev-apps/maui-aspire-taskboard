using TaskBoard.ClientCore.ViewModels;

namespace TaskBoard.Maui.Views;

public partial class DiagnosticsPage : ContentPage
{
    private readonly DiagnosticsViewModel _viewModel;

    public DiagnosticsPage(DiagnosticsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
#if DEBUG
        SimulationControls.IsVisible = true;
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshConnection();
        _viewModel.RunSafely(() => Connectivity.Current.ConnectivityChanged += OnConnectivityChanged);
    }

    protected override void OnDisappearing()
    {
        _viewModel.RunSafely(() => Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged);
        _viewModel.NormalCommand.Cancel();
        _viewModel.SlowCommand.Cancel();
        _viewModel.TimeoutCommand.Cancel();
        _viewModel.FailureCommand.Cancel();
        base.OnDisappearing();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args) =>
        _viewModel.RunSafely(() => MainThread.BeginInvokeOnMainThread(_viewModel.RefreshConnection));
}
