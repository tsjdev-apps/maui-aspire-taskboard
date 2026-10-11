namespace TaskBoard.Contracts;

public sealed record UpdateTaskRequest(string Title, string? Description, bool IsCompleted);
