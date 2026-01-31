using System.CommandLine;
using System.Text.Json;
using GraphLedger.Cli.Output;
using GraphLedger.Core.Graph;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models;
using GraphLedger.Core.Models.Utcm;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Cli.Commands;

public static class MonitorCommands
{
    public static Command CreateMonitorCommand()
    {
        var monitorCommand = new Command("monitor", "Manage UTCM drift monitors");

        monitorCommand.AddCommand(CreateCreateCommand());
        monitorCommand.AddCommand(CreateListCommand());
        monitorCommand.AddCommand(CreateShowCommand());
        monitorCommand.AddCommand(CreateDeleteCommand());

        return monitorCommand;
    }

    private static Command CreateCreateCommand()
    {
        var nameArgument = new Argument<string>("name", "Display name for the monitor");

        var workloadsOption = new Option<string[]>(
            aliases: ["--workloads", "-w"],
            description: "Workloads to monitor (e.g., Entra, Exchange, Intune)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var resourceTypesOption = new Option<string[]>(
            aliases: ["--resource-types", "-r"],
            description: "Specific UTCM resource types to monitor (overrides --workloads)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var frequencyOption = new Option<int>(
            aliases: ["--frequency", "-f"],
            getDefaultValue: () => 24,
            description: "How often to check for drift, in hours");

        var descriptionOption = new Option<string?>(
            aliases: ["--description", "-d"],
            description: "Description for the monitor");

        var createCommand = new Command("create", "Create a new UTCM drift monitor")
        {
            nameArgument,
            workloadsOption,
            resourceTypesOption,
            frequencyOption,
            descriptionOption
        };

        createCommand.SetHandler(async (name, workloads, resourceTypes, frequency, description) =>
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

                // Determine resource types to monitor
                IReadOnlyList<string> targetResourceTypes;
                if (resourceTypes.Length > 0)
                {
                    var invalidTypes = resourceTypes.Where(rt => !UtcmResourceTypeRegistry.IsValidResourceType(rt)).ToList();
                    if (invalidTypes.Count > 0)
                    {
                        TableFormatter.WriteError($"Invalid resource types: {string.Join(", ", invalidTypes)}");
                        return;
                    }
                    targetResourceTypes = resourceTypes;
                }
                else if (workloads.Length > 0)
                {
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
                    TableFormatter.WriteError("Please specify --workloads or --resource-types to monitor.");
                    return;
                }

                TableFormatter.WriteInfo($"Creating monitor '{name}' for {targetResourceTypes.Count} resource types...");

                // First, take a snapshot to use as baseline
                TableFormatter.WriteInfo("Taking baseline snapshot...");
                var snapshotJob = await utcmClient.CreateSnapshotAsync($"Baseline for {name}", targetResourceTypes);
                var completedJob = await utcmClient.WaitForSnapshotAsync(snapshotJob.Id);

                if (completedJob.Status == UtcmJobStatus.Failed)
                {
                    TableFormatter.WriteError($"Failed to create baseline snapshot: {completedJob.ErrorMessage}");
                    return;
                }

                // Build baseline from snapshot
                var baselineResources = new List<UtcmBaselineResource>();
                if (completedJob.SnapshotData != null)
                {
                    foreach (var resource in completedJob.SnapshotData)
                    {
                        baselineResources.Add(new UtcmBaselineResource
                        {
                            DisplayName = resource.DisplayName,
                            ResourceType = resource.ResourceType,
                            Properties = resource.Properties
                        });
                    }
                }

                var baseline = new UtcmBaseline
                {
                    DisplayName = $"Baseline for {name}",
                    Description = description,
                    Resources = baselineResources
                };

                // Create the monitor
                var monitor = await utcmClient.CreateMonitorAsync(name, baseline, frequency);

                // Cache monitor locally
                using var context = CreateDbContext(config);
                await context.Database.EnsureCreatedAsync();

                var monitorRecord = new UtcmMonitorRecord
                {
                    UtcmMonitorId = monitor.Id,
                    DisplayName = monitor.DisplayName,
                    ResourceTypesJson = JsonSerializer.Serialize(targetResourceTypes),
                    Status = monitor.Status,
                    FrequencyInHours = frequency,
                    LastSyncedAt = DateTime.UtcNow
                };

                context.UtcmMonitors.Add(monitorRecord);
                await context.SaveChangesAsync();

                TableFormatter.WriteSuccess($"Monitor created: {monitor.Id}");
                TableFormatter.WriteInfo($"Monitoring {baselineResources.Count} resources every {frequency} hours");
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to create monitor: {ex.Message}");
            }
        }, nameArgument, workloadsOption, resourceTypesOption, frequencyOption, descriptionOption);

        return createCommand;
    }

    private static Command CreateListCommand()
    {
        var localOption = new Option<bool>(
            aliases: ["--local", "-l"],
            description: "Only show locally cached monitors (no API call)");

        var listCommand = new Command("list", "List UTCM drift monitors")
        {
            localOption
        };

        listCommand.SetHandler(async (localOnly) =>
        {
            try
            {
                var config = LoadAppConfiguration();

                if (localOnly)
                {
                    // Show locally cached monitors
                    using var context = CreateDbContext(config);
                    if (!File.Exists(config.Storage.DatabasePath))
                    {
                        TableFormatter.WriteWarning("No local monitor cache found.");
                        return;
                    }

                    var localMonitors = await context.UtcmMonitors.ToListAsync();
                    if (localMonitors.Count == 0)
                    {
                        TableFormatter.WriteWarning("No monitors found in local cache.");
                        return;
                    }

                    TableFormatter.WriteTable(localMonitors,
                        ("UTCM ID", m => m.UtcmMonitorId[..Math.Min(8, m.UtcmMonitorId.Length)]),
                        ("Name", m => m.DisplayName),
                        ("Status", m => m.Status),
                        ("Frequency", m => $"{m.FrequencyInHours}h"),
                        ("Last Synced", m => m.LastSyncedAt.ToString("yyyy-MM-dd HH:mm"))
                    );
                    return;
                }

                // Fetch from API
                if (string.IsNullOrEmpty(config.Azure.TenantId) ||
                    string.IsNullOrEmpty(config.Azure.ClientId))
                {
                    TableFormatter.WriteError("Azure credentials not configured. Use --local to view cached monitors.");
                    return;
                }

                var authProvider = new GraphAuthProvider(config.Azure);
                var utcmClient = new UtcmClient(authProvider, config.Azure.TenantId);

                var monitors = await utcmClient.ListMonitorsAsync();

                if (monitors.Count == 0)
                {
                    TableFormatter.WriteWarning("No monitors found.");
                    return;
                }

                TableFormatter.WriteTable(monitors,
                    ("ID", m => m.Id[..Math.Min(8, m.Id.Length)]),
                    ("Name", m => m.DisplayName),
                    ("Status", m => m.Status),
                    ("Frequency", m => $"{m.MonitorRunFrequencyInHours}h"),
                    ("Created", m => m.CreatedDateTime.ToString("yyyy-MM-dd HH:mm"))
                );
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to list monitors: {ex.Message}");
            }
        }, localOption);

        return listCommand;
    }

    private static Command CreateShowCommand()
    {
        var idArgument = new Argument<string>("id", "Monitor ID");

        var showCommand = new Command("show", "Show monitor details")
        {
            idArgument
        };

        showCommand.SetHandler(async (id) =>
        {
            try
            {
                var config = LoadAppConfiguration();

                if (string.IsNullOrEmpty(config.Azure.TenantId) ||
                    string.IsNullOrEmpty(config.Azure.ClientId))
                {
                    TableFormatter.WriteError("Azure credentials not configured.");
                    return;
                }

                var authProvider = new GraphAuthProvider(config.Azure);
                var utcmClient = new UtcmClient(authProvider, config.Azure.TenantId);

                var monitor = await utcmClient.GetMonitorAsync(id);

                if (monitor == null)
                {
                    TableFormatter.WriteError($"Monitor '{id}' not found.");
                    return;
                }

                Console.WriteLine($"Monitor ID:    {monitor.Id}");
                Console.WriteLine($"Name:          {monitor.DisplayName}");
                Console.WriteLine($"Status:        {monitor.Status}");
                Console.WriteLine($"Frequency:     Every {monitor.MonitorRunFrequencyInHours} hours");
                Console.WriteLine($"Created:       {monitor.CreatedDateTime:yyyy-MM-dd HH:mm:ss} UTC");
                if (!string.IsNullOrEmpty(monitor.Description))
                {
                    Console.WriteLine($"Description:   {monitor.Description}");
                }
                Console.WriteLine();
                Console.WriteLine("Baseline:");
                Console.WriteLine($"  Name:        {monitor.Baseline.DisplayName}");
                Console.WriteLine($"  Resources:   {monitor.Baseline.Resources.Count}");

                if (monitor.Baseline.Resources.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("  Monitored Resources:");
                    foreach (var resource in monitor.Baseline.Resources.Take(10))
                    {
                        var friendlyName = UtcmResourceTypeRegistry.GetFriendlyName(resource.ResourceType);
                        Console.WriteLine($"    - {friendlyName}: {resource.DisplayName}");
                    }
                    if (monitor.Baseline.Resources.Count > 10)
                    {
                        Console.WriteLine($"    ... and {monitor.Baseline.Resources.Count - 10} more");
                    }
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to show monitor: {ex.Message}");
            }
        }, idArgument);

        return showCommand;
    }

    private static Command CreateDeleteCommand()
    {
        var idArgument = new Argument<string>("id", "Monitor ID to delete");

        var forceOption = new Option<bool>(
            aliases: ["--force", "-f"],
            description: "Skip confirmation prompt");

        var deleteCommand = new Command("delete", "Delete a UTCM drift monitor")
        {
            idArgument,
            forceOption
        };

        deleteCommand.SetHandler(async (id, force) =>
        {
            try
            {
                var config = LoadAppConfiguration();

                if (string.IsNullOrEmpty(config.Azure.TenantId) ||
                    string.IsNullOrEmpty(config.Azure.ClientId))
                {
                    TableFormatter.WriteError("Azure credentials not configured.");
                    return;
                }

                if (!force)
                {
                    Console.Write($"Delete monitor '{id}'? [y/N] ");
                    var response = Console.ReadLine();
                    if (!string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
                    {
                        TableFormatter.WriteInfo("Cancelled.");
                        return;
                    }
                }

                var authProvider = new GraphAuthProvider(config.Azure);
                var utcmClient = new UtcmClient(authProvider, config.Azure.TenantId);

                await utcmClient.DeleteMonitorAsync(id);

                // Remove from local cache
                using var context = CreateDbContext(config);
                var localMonitor = await context.UtcmMonitors
                    .FirstOrDefaultAsync(m => m.UtcmMonitorId == id);
                if (localMonitor != null)
                {
                    context.UtcmMonitors.Remove(localMonitor);
                    await context.SaveChangesAsync();
                }

                TableFormatter.WriteSuccess($"Monitor '{id}' deleted.");
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to delete monitor: {ex.Message}");
            }
        }, idArgument, forceOption);

        return deleteCommand;
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
