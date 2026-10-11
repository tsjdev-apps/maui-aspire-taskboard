namespace TaskBoard.Api.Endpoints;

public static class DemoEndpoints
{
    public static void MapDemoEndpoints(this WebApplication app)
    {
        var demo = app.MapGroup("/api/demo");
        demo.MapGet("/slow", async (int? durationMs, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            var duration = durationMs ?? 1500;
            if (duration is < 0 or > 10000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["durationMs"] = ["Use a delay between 0 and 10000 milliseconds."]
                });
            }

            loggerFactory.CreateLogger("TaskBoard.Diagnostics")
                .LogInformation("Simulating a {DurationMs} ms delay", duration);
            await Task.Delay(duration, cancellationToken);
            return Results.Ok(new { durationMs = duration });
        });

        demo.MapGet("/fail", (ILoggerFactory loggerFactory) =>
        {
            loggerFactory.CreateLogger("TaskBoard.Diagnostics").LogWarning("Simulating an HTTP 503 failure");
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Simulated failure", detail: "This development endpoint deliberately returns HTTP 503.");
        });
    }
}
