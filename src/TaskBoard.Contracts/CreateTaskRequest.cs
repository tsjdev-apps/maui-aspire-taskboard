namespace TaskBoard.Contracts;

public sealed record CreateTaskRequest(string Title, string? Description, bool IsCompleted = false);
