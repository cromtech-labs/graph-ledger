using System.CommandLine;
using System.Text.Json;
using GraphLedger.Cli.Output;
using GraphLedger.Core.Models;
using GraphLedger.Core.Services;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Cli.Commands;

public static class AdminCommands
{
    public static Command CreateAdminCommand()
    {
        var adminCommand = new Command("admin", "Administrative commands");

        adminCommand.AddCommand(CreateBackfillResourcesCommand());
        adminCommand.AddCommand(CreateMigrateCommand());

        return adminCommand;
    }

    private static Command CreateBackfillResourcesCommand()
    {
        var dryRunOption = new Option<bool>(
            aliases: ["--dry-run"],
            description: "Show what would be processed without making changes");

        var backfillCommand = new Command("backfill-resources", "Backfill resource tracking for existing snapshots")
        {
            dryRunOption
        };

        backfillCommand.SetHandler(async (dryRun) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);

                if (!File.Exists(config.Storage.DatabasePath))
                {
                    TableFormatter.WriteWarning("No database found. Nothing to backfill.");
                    return;
                }

                // Count snapshots without resource tracking
                var unprocessedCount = await context.Snapshots
                    .CountAsync(s => s.ResourceId == null);

                if (unprocessedCount == 0)
                {
                    TableFormatter.WriteSuccess("All snapshots already have resource tracking.");
                    return;
                }

                TableFormatter.WriteInfo($"Found {unprocessedCount} snapshots without resource tracking.");

                if (dryRun)
                {
                    TableFormatter.WriteInfo("Dry run mode - no changes will be made.");

                    // Show preview of what would be processed
                    var sample = await context.Snapshots
                        .Where(s => s.ResourceId == null)
                        .OrderBy(s => s.CreatedAt)
                        .Take(10)
                        .ToListAsync();

                    TableFormatter.WriteTable(sample,
                        ("ID", s => s.Id.ToString()[..8]),
                        ("Resource Type", s => s.UtcmResourceType ?? "-"),
                        ("Display Name", s => TruncateString(s.ResourceDisplayName ?? "-", 30)),
                        ("Created At", s => s.CreatedAt.ToString("yyyy-MM-dd HH:mm")));

                    if (unprocessedCount > 10)
                    {
                        TableFormatter.WriteInfo($"... and {unprocessedCount - 10} more");
                    }
                    return;
                }

                // Perform backfill
                var hashService = new ConfigurationHashService();
                var contextFactory = new SingleContextFactory(context);
                var trackingService = new ResourceTrackingService(contextFactory, hashService);

                TableFormatter.WriteInfo("Processing snapshots...");

                var progress = new Progress<int>(processed =>
                {
                    if (processed % 50 == 0 || processed == unprocessedCount)
                    {
                        Console.Write($"\rProcessed {processed}/{unprocessedCount} snapshots...");
                    }
                });

                var processedCount = await trackingService.BackfillResourceTrackingAsync(progress);

                Console.WriteLine();
                TableFormatter.WriteSuccess($"Backfill complete. Processed {processedCount} snapshots.");

                // Show summary
                var resourceCount = await context.Resources.CountAsync();
                var changeCount = await context.Snapshots.CountAsync(s => s.HasChangesFromPrevious == true);

                TableFormatter.WriteInfo($"Total resources tracked: {resourceCount}");
                TableFormatter.WriteInfo($"Snapshots with changes: {changeCount}");
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to backfill resources: {ex.Message}");
            }
        }, dryRunOption);

        return backfillCommand;
    }

    private static Command CreateMigrateCommand()
    {
        var migrateCommand = new Command("migrate", "Apply database migrations");

        migrateCommand.SetHandler(async () =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);

                TableFormatter.WriteInfo("Applying database migrations...");

                // EnsureCreated will create the database and tables if they don't exist
                // For a dev project without formal migrations, this is sufficient
                await context.Database.EnsureCreatedAsync();

                TableFormatter.WriteSuccess("Database migrations applied.");
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to apply migrations: {ex.Message}");
            }
        });

        return migrateCommand;
    }

    private static string TruncateString(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        return value[..(maxLength - 3)] + "...";
    }

    private static AppConfiguration LoadAppConfiguration()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".graphledger",
            "config.json"
        );

        if (!File.Exists(configPath))
        {
            return new AppConfiguration();
        }

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<AppConfiguration>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new AppConfiguration();
    }

    private static GraphLedgerDbContext CreateDbContext(AppConfiguration config)
    {
        var directory = Path.GetDirectoryName(config.Storage.DatabasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var optionsBuilder = new DbContextOptionsBuilder<GraphLedgerDbContext>();
        optionsBuilder.UseSqlite($"Data Source={config.Storage.DatabasePath}");

        return new GraphLedgerDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Helper class to wrap a single context as a factory for the tracking service.
    /// </summary>
    private class SingleContextFactory : IDbContextFactory<GraphLedgerDbContext>
    {
        private readonly GraphLedgerDbContext _context;

        public SingleContextFactory(GraphLedgerDbContext context)
        {
            _context = context;
        }

        public GraphLedgerDbContext CreateDbContext() => _context;
    }
}
