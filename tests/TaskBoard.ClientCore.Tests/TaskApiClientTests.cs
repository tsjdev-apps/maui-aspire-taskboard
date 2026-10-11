using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using TaskBoard.ClientCore.DependencyInjection;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.ClientCore.Services;
using TaskBoard.ClientCore.Tests.Fakes;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Tests;

public sealed class TaskApiClientTests
{
    [Fact]
    public async Task ParsesValidationProblemAsync()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new ApiProblemDto("Invalid input", null, 400,
                new Dictionary<string, string[]> { ["title"] = ["A title is required."] }))
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var exception = await Assert.ThrowsAsync<TaskApiException>(() =>
            new TaskApiClient(http).CreateTaskAsync(new CreateTaskRequest("", null)));
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("title", exception.Errors!.Keys);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task HandlesProxyHtmlFailureAsync()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        { Content = new StringContent("<html>Unavailable</html>") }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var exception = await Assert.ThrowsAsync<TaskApiException>(() => new TaskApiClient(http).GetTasksAsync());
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
    }

    [Theory]
    [InlineData("GET", 3)]
    [InlineData("POST", 1)]
    [InlineData("PUT", 1)]
    [InlineData("DELETE", 1)]
    public async Task ResilienceRetriesReadsButNeverWritesAsync(string method, int attempts)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTaskBoardClient(new Uri("https://example.test/"), options => options.Retry.Delay = TimeSpan.Zero);
        services.AddHttpClient<ITaskApiClient, TaskApiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ITaskApiClient>();
        var id = Guid.NewGuid();
        Func<Task> operation = method switch
        {
            "POST" => () => client.CreateTaskAsync(new CreateTaskRequest("Example", null)),
            "PUT" => () => client.UpdateTaskAsync(id, new UpdateTaskRequest("Example", null, true)),
            "DELETE" => () => client.DeleteTaskAsync(id),
            _ => () => client.GetTasksAsync()
        };
        await Assert.ThrowsAsync<TaskApiException>(operation);
        Assert.Equal(attempts, handler.CallCount);
    }

    [Fact]
    public async Task AttemptTimeoutTerminatesWriteWithoutRetryAsync()
    {
        var handler = new StubHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTaskBoardClient(new Uri("https://example.test/"), options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(2);
        });
        services.AddHttpClient<ITaskApiClient, TaskApiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            provider.GetRequiredService<ITaskApiClient>().CreateTaskAsync(new CreateTaskRequest("Example", null)));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task HttpCancellationIsNotRetriedAsync()
    {
        var handler = new StubHttpMessageHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTaskBoardClient(new Uri("https://example.test/"));
        services.AddHttpClient<ITaskApiClient, TaskApiClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        var loading = provider.GetRequiredService<ITaskApiClient>().GetTasksAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loading);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("{not-json}")]
    public async Task RejectsInvalidSuccessfulTaskListsAsync(string payload)
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        await Assert.ThrowsAsync<JsonException>(() => new TaskApiClient(http).GetTasksAsync());
    }

    [Fact]
    public async Task MalformedValidationEntriesRetainTheHttpStatusAsync()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        { Content = new StringContent("{\"title\":\"Invalid input\",\"errors\":{\"title\":null}}", Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var error = await Assert.ThrowsAsync<TaskApiException>(() => new TaskApiClient(http).GetTasksAsync());
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal("Invalid input", error.Message);
    }

    [Fact]
    public async Task SlowResponseBodyIsBoundedEvenAfterHeadersHaveArrivedAsync()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StalledContent() }));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTaskBoardClient(new Uri("https://example.test/"));
        services.AddHttpClient<ITaskApiClient, TaskApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<ITaskApiClient>().GetTasksAsync(deadline.Token));
        Assert.False(deadline.IsCancellationRequested);
        Assert.Equal(1, handler.CallCount);
    }

    private sealed class StalledContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.Delay(Timeout.InfiniteTimeSpan);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context,
            CancellationToken cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
