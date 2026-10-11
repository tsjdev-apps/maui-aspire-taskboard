using System.Net.Http.Json;
using System.Text.Json;
using TaskBoard.ClientCore.Interfaces;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Services;

/// <summary>Uses the same API contract with either service discovery or a conventional base URL.</summary>
public sealed class TaskApiClient(HttpClient httpClient) : ITaskApiClient
{
    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/tasks", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var tasks = await response.Content.ReadFromJsonAsync<TaskDto?[]>(cancellationToken)
            ?? throw new JsonException("The API returned a null task list.");
        return tasks.Select(ValidateTask).ToArray();
    }

    public async Task<TaskDto> GetTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/tasks/{id}", cancellationToken);
        return await ReadTaskAsync(response, cancellationToken);
    }

    public async Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/tasks", request, cancellationToken);
        return await ReadTaskAsync(response, cancellationToken);
    }

    public async Task<TaskDto> UpdateTaskAsync(Guid id, UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/tasks/{id}", request, cancellationToken);
        return await ReadTaskAsync(response, cancellationToken);
    }

    public async Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/tasks/{id}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RunSlowRequestAsync(int durationMs, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/demo/slow?durationMs={durationMs}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task RunFailureRequestAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/demo/fail", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task<TaskDto> ReadTaskAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return ValidateTask(await response.Content.ReadFromJsonAsync<TaskDto>(cancellationToken));
    }

    private static TaskDto ValidateTask(TaskDto? task)
    {
        if (task is null || task.Id == Guid.Empty || TaskValidation.Validate(task.Title, task.Description).Count > 0)
        {
            throw new JsonException("The API returned an invalid task.");
        }
        return task;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiProblemDto? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblemDto>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException
            or ArgumentException)
        {
            // A non-JSON response must still produce a meaningful HTTP failure.
        }

        var message = problem?.Detail ?? problem?.Title ?? $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
        if (problem?.Errors is { Count: > 0 } errors)
        {
            var details = string.Join(" ", errors.Values.SelectMany(value => value ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(details))
            {
                message = details;
            }
        }

        throw new TaskApiException(response.StatusCode, message, problem?.Errors);
    }
}
