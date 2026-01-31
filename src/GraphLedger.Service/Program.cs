using GraphLedger.Core.Models;
using GraphLedger.Core.Services;
using GraphLedger.Core.Storage;
using GraphLedger.Service.Jobs;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Configure app settings
builder.Services.Configure<AppConfiguration>(
    builder.Configuration.GetSection("GraphLedger"));

// Configure Entity Framework with SQLite
var dbPath = builder.Configuration.GetValue<string>("GraphLedger:Storage:DatabasePath")
    ?? "data/graphledger.db";

var dbDirectory = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
{
    Directory.CreateDirectory(dbDirectory);
}

builder.Services.AddDbContextFactory<GraphLedgerDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Register services
builder.Services.AddSingleton<IConfigurationHashService, ConfigurationHashService>();

// Configure Quartz
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("SnapshotPollingJob");
    q.AddJob<SnapshotPollingJob>(opts => opts.WithIdentity(jobKey));

    var cronExpression = builder.Configuration.GetValue<string>("GraphLedger:Scheduling:CronExpression")
        ?? "0 0 * * * ?"; // Default: every hour

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("SnapshotPollingJob-trigger")
        .WithCronSchedule(cronExpression));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var host = builder.Build();

// Ensure database is created
using (var scope = host.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GraphLedgerDbContext>>();
    using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.EnsureCreatedAsync();
}

host.Run();
