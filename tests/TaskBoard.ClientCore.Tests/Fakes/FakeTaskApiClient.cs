using TaskBoard.ClientCore.Interfaces;
using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Tests.Fakes;

internal sealed class FakeTaskApiClient : ITaskApiClient
{
    public List<TaskDto> Tasks { get; } = [];
    public Exception? Failure { get; set; }
    public Func<CancellationToken, Task>? BeforeRequest { get; set; }
    public CancellationToken LastToken { get; private set; }

    private async Task BeforeAsync(CancellationToken cancellationToken)
    {
        LastToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        if (BeforeRequest is not null)
        {
            await BeforeRequest(cancellationToken);
        }
        if (Failure is not null)
        {
            throw Failure;
        }
    }

    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        await BeforeAsync(cancellationToken);
        return Tasks.ToArray();
    }

    public async Task<TaskDto> GetTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await BeforeAsync(cancellationToken);
        return Tasks.Single(task => task.Id == id);
    }

    public async Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        await BeforeAsync(cancellationToken);
        var task = new TaskDto(Guid.NewGuid(), request.Title, request.Description, request.IsCompleted,
            DateTime.UtcNow, DateTime.UtcNow);
        Tasks.Add(task);
        return task;
    }

    public async Task<TaskDto> UpdateTaskAsync(Guid id, UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        await BeforeAsync(cancellationToken);
        var index = Tasks.FindIndex(task => task.Id == id);
        var updated = Tasks[index] with
        {
            Title = request.Title,
            Description = request.Description,
            IsCompleted = request.IsCompleted,
            UpdatedAtUtc = DateTime.UtcNow
        };
        Tasks[index] = updated;
        return updated;
    }

    public async Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await BeforeAsync(cancellationToken);
        Tasks.RemoveAll(task => task.Id == id);
    }

    public Task RunSlowRequestAsync(int durationMs, CancellationToken cancellationToken = default) =>
        BeforeAsync(cancellationToken);

    public Task RunFailureRequestAsync(CancellationToken cancellationToken = default) =>
        BeforeAsync(cancellationToken);
}
