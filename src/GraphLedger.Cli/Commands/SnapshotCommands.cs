using System.CommandLine;
using System.Text.Json;
using GraphLedger.Cli.Output;
using GraphLedger.Core.Diff;
using GraphLedger.Core.Graph;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Cli.Commands;

public static class SnapshotCommands
{
    public static Command CreateSnapshotCommand()
    {
        var snapshotCommand = new Command("snapshot", "Manage configuration snapshots");

        snapshotCommand.AddCommand(CreateTakeCommand());
        snapshotCommand.AddCommand(CreateListCommand());
        snapshotCommand.AddCommand(CreateDiffCommand());
        snapshotCommand.AddCommand(CreateExportCommand());
        snapshotCommand.AddCommand(CreateShowCommand());

        return snapshotCommand;
    }

    private static Command CreateTakeCommand()
    {
        var workloadsOption = new Option<string[]>(
            aliases: new[] { "--workloads", "-w" },
            description: "Workloads to snapshot (comma-separated)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var takeCommand = new Command("take", "Take a new configuration snapshot")
        {
            workloadsOption
        };

        takeCommand.SetHandler(async (workloads) =>
        {
            try
            {
                var config = LoadAppConfiguration();

                if (string.IsNullOrEmpty(config.Azure.TenantId) ||
                    string.IsNullOrEmpty(config.Azure.ClientId))
                {
                    TableFormatter.WriteError("Azure credentials not configured. Use 'graphledger config set' to configure.");
                    return;
                }

                var authProvider = new GraphAuthProvider(config.Azure);
                var utcmClient = new UtcmClient(authProvider, config.Azure.TenantId);

                var targetWorkloads = workloads.Length > 0
                    ? workloads
                    : config.EnabledWorkloads.ToArray();

                TableFormatter.WriteInfo($"Taking snapshots for workloads: {string.Join(", ", targetWorkloads)}");

                using var context = CreateDbContext(config);
                await context.Database.EnsureCreatedAsync();
                var repository = new SnapshotRepository(context);

                var snapshots = await utcmClient.TakeAllSnapshotsAsync(targetWorkloads);

                foreach (var snapshot in snapshots)
                {
                    await repository.CreateAsync(snapshot);
                    TableFormatter.WriteSuccess($"Snapshot taken for {snapshot.Workload}: {snapshot.Id}");
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to take snapshot: {ex.Message}");
            }
        }, workloadsOption);

        return takeCommand;
    }

    private static Command CreateListCommand()
    {
        var limitOption = new Option<int>(
            aliases: new[] { "--limit", "-n" },
            getDefaultValue: () => 20,
            description: "Maximum number of snapshots to display");

        var workloadOption = new Option<string?>(
            aliases: new[] { "--workload", "-w" },
            description: "Filter by workload");

        var listCommand = new Command("list", "List configuration snapshots")
        {
            limitOption,
            workloadOption
        };

        listCommand.SetHandler(async (limit, workload) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);

                if (!File.Exists(config.Storage.DatabasePath))
                {
                    TableFormatter.WriteWarning("No snapshots found. Database does not exist.");
                    return;
                }

                var repository = new SnapshotRepository(context);

                var snapshots = string.IsNullOrEmpty(workload)
                    ? await repository.GetAllAsync(limit)
                    : await repository.GetByWorkloadAsync(workload, limit);

                if (snapshots.Count == 0)
                {
                    TableFormatter.WriteWarning("No snapshots found.");
                    return;
                }

                TableFormatter.WriteTable(snapshots,
                    ("ID", s => s.Id.ToString()[..8]),
                    ("Workload", s => s.Workload),
                    ("Created At", s => s.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                    ("Source", s => s.Source.ToString()),
                    ("Size", s => $"{s.ConfigurationJson.Length:N0} bytes")
                );
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to list snapshots: {ex.Message}");
            }
        }, limitOption, workloadOption);

        return listCommand;
    }

    private static Command CreateDiffCommand()
    {
        var id1Argument = new Argument<string>("id1", "First snapshot ID");
        var id2Argument = new Argument<string>("id2", "Second snapshot ID");

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json", "-j" },
            description: "Output raw JSON diff");

        var diffCommand = new Command("diff", "Compare two snapshots")
        {
            id1Argument,
            id2Argument,
            jsonOption
        };

        diffCommand.SetHandler(async (id1, id2, jsonOutput) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);
                var repository = new SnapshotRepository(context);

                var snapshot1 = await FindSnapshotByPartialId(repository, id1);
                var snapshot2 = await FindSnapshotByPartialId(repository, id2);

                if (snapshot1 == null)
                {
                    TableFormatter.WriteError($"Snapshot '{id1}' not found.");
                    return;
                }

                if (snapshot2 == null)
                {
                    TableFormatter.WriteError($"Snapshot '{id2}' not found.");
                    return;
                }

                var diffEngine = new JsonDiffEngine();
                var result = diffEngine.Compare(snapshot1, snapshot2);

                if (jsonOutput)
                {
                    Console.WriteLine(result.RawDiffJson ?? "{}");
                    return;
                }

                Console.WriteLine($"Comparing snapshots:");
                Console.WriteLine($"  Left:  {snapshot1.Id} ({snapshot1.CreatedAt:yyyy-MM-dd HH:mm:ss})");
                Console.WriteLine($"  Right: {snapshot2.Id} ({snapshot2.CreatedAt:yyyy-MM-dd HH:mm:ss})");
                Console.WriteLine();

                if (!result.HasChanges)
                {
                    TableFormatter.WriteSuccess("No differences found.");
                    return;
                }

                TableFormatter.WriteWarning($"Found {result.ChangeCount} change(s):");
                Console.WriteLine();

                foreach (var change in result.Changes)
                {
                    var color = change.Operation switch
                    {
                        DiffOperation.Add => ConsoleColor.Green,
                        DiffOperation.Remove => ConsoleColor.Red,
                        DiffOperation.Replace => ConsoleColor.Yellow,
                        _ => ConsoleColor.White
                    };

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = color;
                    Console.WriteLine($"  {change}");
                    Console.ForegroundColor = originalColor;
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to diff snapshots: {ex.Message}");
            }
        }, id1Argument, id2Argument, jsonOption);

        return diffCommand;
    }

    private static Command CreateExportCommand()
    {
        var idArgument = new Argument<string>("id", "Snapshot ID");
        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path")
        { IsRequired = true };

        var exportCommand = new Command("export", "Export a snapshot to file")
        {
            idArgument,
            outputOption
        };

        exportCommand.SetHandler(async (id, output) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);
                var repository = new SnapshotRepository(context);

                var snapshot = await FindSnapshotByPartialId(repository, id);

                if (snapshot == null)
                {
                    TableFormatter.WriteError($"Snapshot '{id}' not found.");
                    return;
                }

                var exportData = new
                {
                    snapshot.Id,
                    snapshot.TenantId,
                    snapshot.Workload,
                    snapshot.CreatedAt,
                    snapshot.Source,
                    snapshot.Description,
                    Configuration = JsonSerializer.Deserialize<JsonElement>(snapshot.ConfigurationJson)
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(output, json);
                TableFormatter.WriteSuccess($"Snapshot exported to: {output}");
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to export snapshot: {ex.Message}");
            }
        }, idArgument, outputOption);

        return exportCommand;
    }

    private static Command CreateShowCommand()
    {
        var idArgument = new Argument<string>("id", "Snapshot ID");

        var showCommand = new Command("show", "Show snapshot details")
        {
            idArgument
        };

        showCommand.SetHandler(async (id) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);
                var repository = new SnapshotRepository(context);

                var snapshot = await FindSnapshotByPartialId(repository, id);

                if (snapshot == null)
                {
                    TableFormatter.WriteError($"Snapshot '{id}' not found.");
                    return;
                }

                Console.WriteLine($"Snapshot: {snapshot.Id}");
                Console.WriteLine($"Tenant:   {snapshot.TenantId}");
                Console.WriteLine($"Workload: {snapshot.Workload}");
                Console.WriteLine($"Created:  {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"Source:   {snapshot.Source}");
                if (!string.IsNullOrEmpty(snapshot.Description))
                {
                    Console.WriteLine($"Description: {snapshot.Description}");
                }
                Console.WriteLine();
                Console.WriteLine("Configuration:");
                Console.WriteLine(JsonFormatter.FormatPretty(snapshot.ConfigurationJson));
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to show snapshot: {ex.Message}");
            }
        }, idArgument);

        return showCommand;
    }

    private static async Task<Snapshot?> FindSnapshotByPartialId(ISnapshotRepository repository, string partialId)
    {
        if (Guid.TryParse(partialId, out var fullId))
        {
            return await repository.GetByIdAsync(fullId);
        }

        var allSnapshots = await repository.GetAllAsync(1000);
        return allSnapshots.FirstOrDefault(s =>
            s.Id.ToString().StartsWith(partialId, StringComparison.OrdinalIgnoreCase));
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
}
