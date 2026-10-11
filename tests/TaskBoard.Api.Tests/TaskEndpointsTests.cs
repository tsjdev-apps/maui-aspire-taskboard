using System.Net;
using System.Net.Http.Json;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TaskBoard.Contracts;

namespace TaskBoard.Api.Tests;

public sealed class TaskEndpointsTests
{
    [Fact]
    public async Task UnexpectedDatabaseFailureReturnsProblemDetailsAndAllowsRecoveryAsync()
    {
        var interceptor = new FailingQueryInterceptor();
        using var factory = new TaskBoardApiFactory("Production", interceptor);
        using var client = factory.CreateClient();
        interceptor.Fail = true;
        using var response = await client.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test-database-query-failure", body);
        Assert.Equal(500, (await response.Content.ReadFromJsonAsync<ApiProblemDto>())!.Status);

        interceptor.Fail = false;
        Assert.Empty((await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!);
    }

    private sealed class FailingQueryInterceptor : DbCommandInterceptor
    {
        public bool Fail { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) =>
            Fail ? throw new IOException("test-database-query-failure") : ValueTask.FromResult(result);
    }

    [Fact]
    public async Task NewDatabaseIsEmptyOutsideDevelopmentAsync()
    {
        using var factory = new TaskBoardApiFactory();
        using var client = factory.CreateClient();
        var tasks = await client.GetFromJsonAsync<TaskDto[]>("/api/tasks");
        Assert.Empty(tasks!);
    }

    [Fact]
    public async Task CreateReadUpdateCompleteAndDeleteTaskAsync()
    {
        using var factory = new TaskBoardApiFactory();
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest("  Read docs  ", "Details"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = (await created.Content.ReadFromJsonAsync<TaskDto>())!;
        Assert.Equal("Read docs", task.Title);
        Assert.False(task.IsCompleted);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(DateTimeKind.Utc, task.CreatedAtUtc.Kind);
        Assert.Equal(task.CreatedAtUtc, task.UpdatedAtUtc);
        Assert.Equal($"/api/tasks/{task.Id}", created.Headers.Location!.OriginalString);
        Assert.Equal(task, await client.GetFromJsonAsync<TaskDto>($"/api/tasks/{task.Id}"));
        Assert.Equal(DateTimeKind.Utc, (await client.GetFromJsonAsync<TaskDto>($"/api/tasks/{task.Id}"))!.CreatedAtUtc.Kind);
        Assert.Single((await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!);

        using var updated = await client.PutAsJsonAsync($"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Updated", null, true));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var completed = (await updated.Content.ReadFromJsonAsync<TaskDto>())!;
        Assert.True(completed.IsCompleted);
        Assert.Equal("Updated", completed.Title);
        Assert.Null(completed.Description);
        Assert.Equal(task.CreatedAtUtc, completed.CreatedAtUtc);
        Assert.True(completed.UpdatedAtUtc >= task.UpdatedAtUtc);

        using var deleted = await client.DeleteAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var missing = await client.GetAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidTitleReturnsValidationProblemAsync(string title)
    {
        using var factory = new TaskBoardApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(title, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblemDto>();
        Assert.Contains("title", problem!.Errors!.Keys);
    }

    [Fact]
    public async Task OversizedFieldsAreRejectedOnCreateAndUpdateAsync()
    {
        using var factory = new TaskBoardApiFactory();
        using var client = factory.CreateClient();
        using var title = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(new string('a', 121), null));
        using var description = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest("Title", new string('a', 2001)));
        using var update = await client.PutAsJsonAsync($"/api/tasks/{Guid.NewGuid()}",
            new UpdateTaskRequest("", null, false));
        Assert.Equal(HttpStatusCode.BadRequest, title.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, description.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task MissingIdsReturnProblemDetailsForAllOperationsAsync()
    {
        using var factory = new TaskBoardApiFactory();
        using var client = factory.CreateClient();
        var path = $"/api/tasks/{Guid.NewGuid()}";
        using var get = await client.GetAsync(path);
        using var put = await client.PutAsJsonAsync(path, new UpdateTaskRequest("Missing", null, false));
        using var delete = await client.DeleteAsync(path);
        foreach (var response in new[] { get, put, delete })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDto>();
            Assert.Equal(404, problem!.Status);
            Assert.Equal("Task not found", problem.Title);
        }
    }

    [Fact]
    public async Task NullTitleAndMalformedJsonReturnBadRequestAsync()
    {
        using var factory = new TaskBoardApiFactory();
        using var client = factory.CreateClient();
        using var nullTitle = await client.PostAsJsonAsync("/api/tasks", new { title = (string?)null });
        using var malformed = await client.PostAsync("/api/tasks", new StringContent("{", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, nullTitle.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task SeparateFactoriesHaveIsolatedStateAsync()
    {
        using var first = new TaskBoardApiFactory();
        using var second = new TaskBoardApiFactory();
        using var firstClient = first.CreateClient();
        using var secondClient = second.CreateClient();
        using var response = await firstClient.PostAsJsonAsync("/api/tasks", new CreateTaskRequest("First only", null));
        response.EnsureSuccessStatusCode();
        Assert.Empty((await secondClient.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!);
        Assert.Single((await firstClient.GetFromJsonAsync<TaskDto[]>("/api/tasks"))!);
    }
}
