using System.Net;

namespace TaskBoard.ClientCore.Services;

public sealed class TaskApiException(HttpStatusCode statusCode, string message,
    IReadOnlyDictionary<string, string[]>? errors = null) : HttpRequestException(message, null, statusCode)
{
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}
