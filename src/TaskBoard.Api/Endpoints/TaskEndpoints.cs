using Microsoft.EntityFrameworkCore;
using TaskBoard.Api.Data;
using TaskBoard.Contracts;

namespace TaskBoard.Api.Endpoints;

public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var tasks = app.MapGroup("/api/tasks");
        tasks.MapGet("/", async (TaskBoardDbContext database, CancellationToken cancellationToken) =>
        {
            var entities = await database.Tasks.AsNoTracking()
                .OrderByDescending(task => task.CreatedAtUtc).ThenBy(task => task.Id)
                .ToListAsync(cancellationToken);
            return Results.Ok(entities.Select(task => task.ToDto()));
        });

        tasks.MapGet("/{id:guid}", async (Guid id, TaskBoardDbContext database, CancellationToken cancellationToken) =>
        {
            var task = await database.Tasks.AsNoTracking().FirstOrDefaultAsync(task => task.Id == id, cancellationToken);
            return task is null ? NotFound() : Results.Ok(task.ToDto());
        });

        tasks.MapPost("/", async (CreateTaskRequest request, TaskBoardDbContext database,
            ILogger<TaskBoardDbContext> logger, CancellationToken cancellationToken) =>
        {
            var errors = TaskValidation.Validate(request.Title, request.Description);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var now = DateTime.UtcNow;
            var task = new TaskEntity
            {
                Id = Guid.NewGuid(),
                Title = request.Title.Trim(),
                Description = NormalizeDescription(request.Description),
                IsCompleted = request.IsCompleted,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            database.Tasks.Add(task);
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Created task {TaskId}", task.Id);
            return Results.Created($"/api/tasks/{task.Id}", task.ToDto());
        });

        tasks.MapPut("/{id:guid}", async (Guid id, UpdateTaskRequest request, TaskBoardDbContext database,
            ILogger<TaskBoardDbContext> logger, CancellationToken cancellationToken) =>
        {
            var errors = TaskValidation.Validate(request.Title, request.Description);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var task = await database.Tasks.FindAsync([id], cancellationToken);
            if (task is null)
            {
                return NotFound();
            }

            task.Title = request.Title.Trim();
            task.Description = NormalizeDescription(request.Description);
            task.IsCompleted = request.IsCompleted;
            task.UpdatedAtUtc = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Updated task {TaskId}; completed: {IsCompleted}", id, task.IsCompleted);
            return Results.Ok(task.ToDto());
        });

        tasks.MapDelete("/{id:guid}", async (Guid id, TaskBoardDbContext database,
            ILogger<TaskBoardDbContext> logger, CancellationToken cancellationToken) =>
        {
            var affected = await database.Tasks.Where(task => task.Id == id).ExecuteDeleteAsync(cancellationToken);
            if (affected == 0)
            {
                return NotFound();
            }

            logger.LogInformation("Deleted task {TaskId}", id);
            return Results.NoContent();
        });
    }

    private static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound,
        title: "Task not found", detail: "No task exists with the specified ID.");

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}
