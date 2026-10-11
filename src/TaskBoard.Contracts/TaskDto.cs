namespace TaskBoard.Contracts;

/// <summary>A snapshot of a task. All timestamps are UTC.</summary>
public sealed record TaskDto(Guid Id, string Title, string? Description, bool IsCompleted,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
