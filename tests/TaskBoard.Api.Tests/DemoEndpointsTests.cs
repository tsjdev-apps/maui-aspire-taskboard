using System.Net;
using System.Net.Http.Json;
using TaskBoard.Contracts;
using Microsoft.Extensions.DependencyInjection;
using TaskBoard.Api.Data;

namespace TaskBoard.Api.Tests;

public sealed class DemoEndpointsTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Testing")]
    public async Task DiagnosticsAreAbsentOutsideDevelopmentAsync(string environment)
    {
        using var factory = new TaskBoardApiFactory(environment);
        using var client = factory.CreateClient();
        using var slow = await client.GetAsync("/api/demo/slow?durationMs=0");
        using var failure = await client.GetAsync("/api/demo/fail");
        Assert.Equal(HttpStatusCode.NotFound, slow.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task DevelopmentOffersSeedDataHealthAndDiagnosticsAsync()
    {
        using var factory = new TaskBoardApiFactory("Development");
        using var client = factory.CreateClient();
        Assert.Equal(2, (await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!.Length);
        using var health = await client.GetAsync("/health");
        using var slow = await client.GetAsync("/api/demo/slow?durationMs=0");
        using var failure = await client.GetAsync("/api/demo/fail");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, slow.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitializerNeverReseedsAnExistingEmptyDatabaseAsync(bool seedDemoData)
    {
        using var factory = new TaskBoardApiFactory("Development");
        using var client = factory.CreateClient();
        var tasks = (await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!;
        foreach (var task in tasks)
        {
            using var response = await client.DeleteAsync($"/api/tasks/{task.Id}");
            response.EnsureSuccessStatusCode();
        }

        await DatabaseInitializer.InitializeAsync(factory.Services, seedDemoData, CancellationToken.None);
        Assert.Empty((await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public async Task DelayIsBoundedAsync(int duration)
    {
        using var factory = new TaskBoardApiFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/demo/slow?durationMs={duration}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
