using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using TaskBoard.ClientCore.Resources;
using TaskBoard.ClientCore.Tests.Fakes;
using TaskBoard.ClientCore.ViewModels;

namespace TaskBoard.ClientCore.Tests;

public sealed class OperationErrorTests
{
    public static TheoryData<Exception, string> Failures => new()
    {
        { new BrokenCircuitException(), nameof(Text.ServiceTemporarilyUnavailable) },
        { new RateLimiterRejectedException(), nameof(Text.ServiceTemporarilyUnavailable) },
        { new TimeoutRejectedException(), nameof(Text.RequestTimedOut) },
        { new System.Text.Json.JsonException("Invalid payload"), nameof(Text.InvalidApiResponse) },
        { new InvalidOperationException("Internal implementation details"), nameof(Text.UnexpectedError) }
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task FailureIsPresentedAndTheCommandCanRecoverAsync(Exception failure, string resourceKey)
    {
        var api = new FakeTaskApiClient { Failure = failure };
        var logger = new RecordingLogger();
        var viewModel = new TasksViewModel(api, new FakeNavigationService(), TimeProvider.System,
            logger);
        // Use the actual ICommand entry point used by MAUI buttons, which normally rethrows faults.
        viewModel.LoadCommand.Execute(null);
        await viewModel.LoadCommand.ExecutionTask!;
        Assert.Equal(Text.Get(resourceKey), viewModel.ErrorMessage);
        Assert.Same(failure, logger.Exception);
        Assert.Equal(failure is InvalidOperationException ? LogLevel.Error : LogLevel.Warning, logger.Level);
        Assert.False(viewModel.IsBusy);
        api.Failure = null;
        await viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Null(viewModel.ErrorMessage);
        Assert.NotNull(viewModel.LastSynchronizedAt);
    }

    [Fact]
    public async Task LifecycleCallbackFaultIsObservedAndReportedAsync()
    {
        var viewModel = new TasksViewModel(new FakeTaskApiClient(), new FakeNavigationService(), TimeProvider.System,
            NullLogger<TasksViewModel>.Instance);
        Assert.False(await viewModel.RunSafelyAsync(() => Task.FromException(new ObjectDisposedException("Page"))));
        Assert.Equal(Text.UnexpectedError, viewModel.ErrorMessage);
    }

    private sealed class RecordingLogger : ILogger<TasksViewModel>
    {
        public LogLevel Level { get; private set; }
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Level = logLevel;
            Exception = exception;
        }
    }
}
