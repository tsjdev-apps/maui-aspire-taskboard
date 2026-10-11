namespace TaskBoard.Contracts;

/// <summary>RFC Problem Details fields consumed by the client.</summary>
public sealed record ApiProblemDto(string? Title, string? Detail, int? Status,
    Dictionary<string, string[]>? Errors);
