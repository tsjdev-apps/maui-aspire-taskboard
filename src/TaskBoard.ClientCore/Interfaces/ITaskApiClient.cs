using TaskBoard.Contracts;

namespace TaskBoard.ClientCore.Interfaces;

public interface ITaskApiClient
{
    Task<IReadOnlyList<TaskDto>> GetTasksAsync(CancellationToken cancellationToken = default);
    Task<TaskDto> GetTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskDto> CreateTaskAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskDto> UpdateTaskAsync(Guid id, UpdateTaskRequest request, CancellationToken cancellationToken = default);
    Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task RunSlowRequestAsync(int durationMs, CancellationToken cancellationToken = default);
    Task RunFailureRequestAsync(CancellationToken cancellationToken = default);
}
