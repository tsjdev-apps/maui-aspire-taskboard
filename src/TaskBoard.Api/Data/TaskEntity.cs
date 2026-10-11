using TaskBoard.Contracts;

namespace TaskBoard.Api.Data;

public sealed class TaskEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public TaskDto ToDto() => new(Id, Title, Description, IsCompleted, CreatedAtUtc, UpdatedAtUtc);
}
