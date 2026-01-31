using System.CommandLine;
using System.Text.Json;
using GraphLedger.Cli.Output;
using GraphLedger.Core.Diff;
using GraphLedger.Core.Graph;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models;
using GraphLedger.Core.Models.Utcm;
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
            aliases: ["--workloads", "-w"],
            description: "UTCM workloads to snapshot (e.g., Entra, Exchange, Intune, Teams, Security)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var resourceTypesOption = new Option<string[]>(
            aliases: ["--resource-types", "-r"],
            description: "Specific UTCM resource types to snapshot (overrides --workloads)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var descriptionOption = new Option<string?>(
            aliases: ["--description", "-d"],
            description: "Description for this snapshot");

        var takeCommand = new Command("take", "Take a new configuration snapshot using UTCM API")
        {
            workloadsOption,
            resourceTypesOption,
            descriptionOption
        };

        takeCommand.SetHandler(async (workloads, resourceTypes, description) =>
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

                // Determine resource types to snapshot
                IReadOnlyList<string> targetResourceTypes;
                if (resourceTypes.Length > 0)
                {
                    // Validate resource types
                    var invalidTypes = resourceTypes.Where(rt => !UtcmResourceTypeRegistry.IsValidResourceType(rt)).ToList();
                    if (invalidTypes.Count > 0)
                    {
                        TableFormatter.WriteError($"Invalid resource types: {string.Join(", ", invalidTypes)}");
                        TableFormatter.WriteInfo("Use 'graphledger workload resources <workload>' to see valid resource types.");
                        return;
                    }
                    targetResourceTypes = resourceTypes;
                }
                else if (workloads.Length > 0)
                {
                    // Validate workloads
                    var invalidWorkloads = workloads.Where(w => !UtcmResourceTypeRegistry.IsValidWorkload(w)).ToList();
                    if (invalidWorkloads.Count > 0)
                    {
                        TableFormatter.WriteError($"Invalid workloads: {string.Join(", ", invalidWorkloads)}");
                        TableFormatter.WriteInfo($"Valid workloads: {string.Join(", ", UtcmResourceTypeRegistry.GetWorkloads())}");
                        return;
                    }
                    targetResourceTypes = UtcmResourceTypeRegistry.GetResourceTypesForWorkloads(workloads);
                }
                else
                {
                    // Default: use Entra workload
                    targetResourceTypes = UtcmResourceTypeRegistry.GetResourceTypes("Entra");
                }

                var displayName = description ?? $"Snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

                TableFormatter.WriteInfo($"Creating UTCM snapshot with {targetResourceTypes.Count} resource types...");

                // Create snapshot job
                var job = await utcmClient.CreateSnapshotAsync(displayName, targetResourceTypes);
                TableFormatter.WriteInfo($"Snapshot job created: {job.Id}");
                TableFormatter.WriteInfo("Waiting for snapshot to complete...");

                // Wait for completion
                var completedJob = await utcmClient.WaitForSnapshotAsync(job.Id);

                if (completedJob.Status == UtcmJobStatus.Failed)
                {
                    TableFormatter.WriteError($"Snapshot failed: {completedJob.ErrorMessage}");
                    return;
                }

                using var context = CreateDbContext(config);
                await context.Database.EnsureCreatedAsync();
                var repository = new SnapshotRepository(context);

                // Store each resource as a separate snapshot
                if (completedJob.SnapshotData != null && completedJob.SnapshotData.Count > 0)
                {
                    foreach (var resource in completedJob.SnapshotData)
                    {
                        var workload = UtcmResourceTypeRegistry.GetWorkloadForResourceType(resource.ResourceType) ?? "Unknown";
                        var snapshot = new Snapshot
                        {
                            TenantId = config.Azure.TenantId,
                            Workload = workload,
                            ConfigurationJson = JsonSerializer.Serialize(resource.Properties, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            }),
                            Source = SnapshotSource.Manual,
                            Description = description,
                            UtcmJobId = completedJob.Id,
                            UtcmResourceType = resource.ResourceType,
                            ResourceDisplayName = resource.DisplayName
                        };

                        await repository.CreateAsync(snapshot);
                        TableFormatter.WriteSuccess($"Saved {UtcmResourceTypeRegistry.GetFriendlyName(resource.ResourceType)}: {resource.DisplayName}");
                    }

                    TableFormatter.WriteSuccess($"Snapshot complete. Saved {completedJob.SnapshotData.Count} resources.");
                }
                else
                {
                    TableFormatter.WriteWarning("Snapshot completed but no resources were returned.");
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to take snapshot: {ex.Message}");
            }
        }, workloadsOption, resourceTypesOption, descriptionOption);

        return takeCommand;
    }

    private static Command CreateListCommand()
    {
        var limitOption = new Option<int>(
            aliases: ["--limit", "-n"],
            getDefaultValue: () => 20,
            description: "Maximum number of snapshots to display");

        var workloadOption = new Option<string?>(
            aliases: ["--workload", "-w"],
            description: "Filter by workload");

        var resourceTypeOption = new Option<string?>(
            aliases: ["--resource-type", "-r"],
            description: "Filter by UTCM resource type");

        var listCommand = new Command("list", "List configuration snapshots")
        {
            limitOption,
            workloadOption,
            resourceTypeOption
        };

        listCommand.SetHandler(async (limit, workload, resourceType) =>
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

                IReadOnlyList<Snapshot> snapshots;
                if (!string.IsNullOrEmpty(resourceType))
                {
                    // Filter by resource type
                    var allSnapshots = await repository.GetAllAsync(limit * 10);
                    snapshots = allSnapshots
                        .Where(s => string.Equals(s.UtcmResourceType, resourceType, StringComparison.OrdinalIgnoreCase))
                        .Take(limit)
                        .ToList();
                }
                else if (!string.IsNullOrEmpty(workload))
                {
                    snapshots = await repository.GetByWorkloadAsync(workload, limit);
                }
                else
                {
                    snapshots = await repository.GetAllAsync(limit);
                }

                if (snapshots.Count == 0)
                {
                    TableFormatter.WriteWarning("No snapshots found.");
                    return;
                }

                TableFormatter.WriteTable(snapshots,
                    ("ID", s => s.Id.ToString()[..8]),
                    ("Workload", s => s.Workload),
                    ("Resource Type", s => s.UtcmResourceType != null
                        ? UtcmResourceTypeRegistry.GetFriendlyName(s.UtcmResourceType)
                        : "-"),
                    ("Display Name", s => TruncateString(s.ResourceDisplayName ?? "-", 25)),
                    ("Created At", s => s.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
                    ("Size", s => $"{s.ConfigurationJson.Length:N0}b")
                );
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to list snapshots: {ex.Message}");
            }
        }, limitOption, workloadOption, resourceTypeOption);

        return listCommand;
    }

    private static Command CreateDiffCommand()
    {
        var id1Argument = new Argument<string>("id1", "First snapshot ID");
        var id2Argument = new Argument<string>("id2", "Second snapshot ID");

        var jsonOption = new Option<bool>(
            aliases: ["--json", "-j"],
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
            aliases: ["--output", "-o"],
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
                    snapshot.UtcmResourceType,
                    snapshot.ResourceDisplayName,
                    snapshot.CreatedAt,
                    snapshot.Source,
                    snapshot.Description,
                    snapshot.UtcmJobId,
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

                Console.WriteLine($"Snapshot:      {snapshot.Id}");
                Console.WriteLine($"Tenant:        {snapshot.TenantId}");
                Console.WriteLine($"Workload:      {snapshot.Workload}");
                if (!string.IsNullOrEmpty(snapshot.UtcmResourceType))
                {
                    Console.WriteLine($"Resource Type: {UtcmResourceTypeRegistry.GetFriendlyName(snapshot.UtcmResourceType)}");
                    Console.WriteLine($"               ({snapshot.UtcmResourceType})");
                }
                if (!string.IsNullOrEmpty(snapshot.ResourceDisplayName))
                {
                    Console.WriteLine($"Display Name:  {snapshot.ResourceDisplayName}");
                }
                Console.WriteLine($"Created:       {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"Source:        {snapshot.Source}");
                if (!string.IsNullOrEmpty(snapshot.UtcmJobId))
                {
                    Console.WriteLine($"UTCM Job ID:   {snapshot.UtcmJobId}");
                }
                if (!string.IsNullOrEmpty(snapshot.Description))
                {
                    Console.WriteLine($"Description:   {snapshot.Description}");
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

    private static string TruncateString(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        return value[..(maxLength - 3)] + "...";
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
