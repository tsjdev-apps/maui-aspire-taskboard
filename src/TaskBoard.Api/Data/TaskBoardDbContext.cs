using Microsoft.EntityFrameworkCore;
using TaskBoard.Contracts;

namespace TaskBoard.Api.Data;

public sealed class TaskBoardDbContext(DbContextOptions<TaskBoardDbContext> options) : DbContext(options)
{
    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskEntity>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).IsRequired().HasMaxLength(TaskValidation.MaxTitleLength);
            entity.Property(task => task.Description).HasMaxLength(TaskValidation.MaxDescriptionLength);
            entity.Property(task => task.CreatedAtUtc).HasConversion(
                value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
            entity.Property(task => task.UpdatedAtUtc).HasConversion(
                value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
            entity.HasIndex(task => task.CreatedAtUtc);
            entity.HasData(
                new TaskEntity
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Title = "Explore the Aspire dashboard",
                    Description = "Load this board and find the matching client and API spans.",
                    CreatedAtUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
                },
                new TaskEntity
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Title = "Try the diagnostics screen",
                    Description = "Compare a normal request, a slow request, and a simulated failure.",
                    CreatedAtUtc = new DateTime(2026, 1, 1, 12, 1, 0, DateTimeKind.Utc),
                    UpdatedAtUtc = new DateTime(2026, 1, 1, 12, 1, 0, DateTimeKind.Utc)
                });
        });
    }
}
