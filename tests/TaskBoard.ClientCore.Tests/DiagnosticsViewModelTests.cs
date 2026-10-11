using Microsoft.Extensions.Logging.Abstractions;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.ClientCore.Resources;
using TaskBoard.ClientCore.Tests.Fakes;
using TaskBoard.ClientCore.ViewModels;

namespace TaskBoard.ClientCore.Tests;

public sealed class DiagnosticsViewModelTests
{
    [Fact]
    public async Task DiagnosticsPresentSuccessAndFailureAsync()
    {
        var api = new FakeTaskApiClient();
        var viewModel = new DiagnosticsViewModel(api, new ConnectedStatus(), NullLogger<DiagnosticsViewModel>.Instance);
        await viewModel.NormalCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        Assert.NotNull(viewModel.ResultMessage);
        Assert.Null(viewModel.ErrorMessage);
        Assert.True(viewModel.DurationMilliseconds >= 0);
        api.Failure = new HttpRequestException("HTTP 503");
        await viewModel.FailureCommand.ExecuteAsync(null);
        Assert.Contains("HTTP 503", viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task DiagnosticsCanCancelPendingRequestAsync()
    {
        var api = new FakeTaskApiClient { BeforeRequest = token => Task.Delay(Timeout.InfiniteTimeSpan, token) };
        var viewModel = new DiagnosticsViewModel(api, new ConnectedStatus(), NullLogger<DiagnosticsViewModel>.Instance);
        var request = viewModel.SlowCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsBusy);
        viewModel.SlowCommand.Cancel();
        await request;
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.ErrorMessage);
    }

    private sealed class ConnectedStatus : IConnectionStatus
    {
        public bool IsConnected => true;
    }

    [Fact]
    public async Task NativeConnectivityFailureDoesNotEscapeEventsOrCommandsAsync()
    {
        var status = new BrokenStatus();
        var viewModel = new DiagnosticsViewModel(new FakeTaskApiClient(), status,
            NullLogger<DiagnosticsViewModel>.Instance);
        viewModel.RefreshConnection();
        Assert.True(viewModel.IsConnected);
        status.Fail = true;
        viewModel.RefreshConnection();
        Assert.False(viewModel.IsConnected);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);
        await viewModel.NormalCommand.ExecuteAsync(null);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);
        Assert.Equal(Text.RequestFailed, viewModel.ResultMessage);
    }

    private sealed class BrokenStatus : IConnectionStatus
    {
        public bool Fail { get; set; }
        public bool IsConnected => Fail ? throw new NotSupportedException("Native connectivity is unavailable") : true;
    }
}
