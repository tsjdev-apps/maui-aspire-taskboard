using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TaskBoard.Api.Data;

namespace TaskBoard.Api.Tests;

/// <summary>Each factory owns a separate SQLite database, including real relational behavior.</summary>
internal sealed class TaskBoardApiFactory(string environment = "Testing", DbCommandInterceptor? interceptor = null)
    : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"taskboard-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:taskboard"] = $"Data Source={_databasePath};Pooling=False"
            }));
        if (interceptor is not null)
        {
            builder.ConfigureServices(services => services.AddDbContext<TaskBoardDbContext>(options =>
                options.AddInterceptors(interceptor)));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            File.Delete(_databasePath);
            File.Delete(_databasePath + "-wal");
            File.Delete(_databasePath + "-shm");
        }
    }
}
