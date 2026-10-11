using Microsoft.EntityFrameworkCore;
using TaskBoard.Api.Data;
using TaskBoard.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<TaskBoardDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("taskboard");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        var directory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        connectionString = $"Data Source={Path.Combine(directory, "taskboard.db")}";
    }

    options.UseSqlite(connectionString);
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
await DatabaseInitializer.InitializeAsync(app.Services, app.Environment.IsDevelopment(),
    app.Lifetime.ApplicationStopping);
app.MapTaskEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapDemoEndpoints();
}
app.MapDefaultEndpoints();
app.Run();
