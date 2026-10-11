using Microsoft.EntityFrameworkCore;

namespace TaskBoard.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool seedDemoData,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<TaskBoardDbContext>();
        // EnsureCreated inserts model seed data only when it creates the schema.
        // Deleting every task therefore never restores deleted examples on restart.
        var created = await database.Database.EnsureCreatedAsync(cancellationToken);
        if (created && !seedDemoData)
        {
            await database.Tasks.ExecuteDeleteAsync(cancellationToken);
        }
    }
}
