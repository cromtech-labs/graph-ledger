using System.CommandLine;
using System.Text.Json;
using GraphLedger.Cli.Output;
using GraphLedger.Core.Diff;
using GraphLedger.Core.Graph;
using GraphLedger.Core.Models;
using GraphLedger.Core.Services;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Cli.Commands;

public static class ResourceCommands
{
    public static Command CreateResourceCommand()
    {
        var resourceCommand = new Command("resource", "Browse and manage tracked resources");

        resourceCommand.AddCommand(CreateListCommand());
        resourceCommand.AddCommand(CreateShowCommand());
        resourceCommand.AddCommand(CreateHistoryCommand());
        resourceCommand.AddCommand(CreateDiffCommand());

        return resourceCommand;
    }

    private static Command CreateListCommand()
    {
        var workloadOption = new Option<string?>(
            aliases: ["--workload", "-w"],
            description: "Filter by workload");

        var resourceTypeOption = new Option<string?>(
            aliases: ["--type", "-t"],
            description: "Filter by resource type");

        var changedSinceOption = new Option<int?>(
            aliases: ["--changed-since", "-c"],
            description: "Show only resources changed in the last N days");

        var limitOption = new Option<int>(
            aliases: ["--limit", "-n"],
            getDefaultValue: () => 50,
            description: "Maximum number of resources to display");

        var listCommand = new Command("list", "List tracked resources")
        {
            workloadOption,
            resourceTypeOption,
            changedSinceOption,
            limitOption
        };

        listCommand.SetHandler(async (workload, resourceType, changedSince, limit) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);

                if (!File.Exists(config.Storage.DatabasePath))
                {
                    TableFormatter.WriteWarning("No database found. Run some snapshots first.");
                    return;
                }

                var query = context.Resources.AsQueryable();

                if (!string.IsNullOrEmpty(workload))
                    query = query.Where(r => r.Workload == workload);

                if (!string.IsNullOrEmpty(resourceType))
                    query = query.Where(r => r.ResourceType.Contains(resourceType));

                if (changedSince.HasValue)
                {
                    var cutoff = DateTime.UtcNow.AddDays(-changedSince.Value);
                    query = query.Where(r => r.LastChangedAt >= cutoff);
                }

                var resources = await query
                    .OrderByDescending(r => r.LastChangedAt ?? r.FirstSeenAt)
                    .Take(limit)
                    .ToListAsync();

                if (resources.Count == 0)
                {
                    TableFormatter.WriteWarning("No resources found matching the criteria.");
                    TableFormatter.WriteInfo("Run 'graphledger admin backfill-resources' if you have existing snapshots.");
                    return;
                }

                TableFormatter.WriteTable(resources,
                    ("ID", r => r.Id.ToString()[..8]),
                    ("Display Name", r => TruncateString(r.DisplayName, 35)),
                    ("Type", r => GetShortTypeName(r.ResourceType)),
                    ("Workload", r => r.Workload),
                    ("Snapshots", r => r.SnapshotCount.ToString()),
                    ("Changes", r => r.ChangeCount.ToString()),
                    ("Last Changed", r => FormatTimeAgo(r.LastChangedAt)));

                TableFormatter.WriteInfo($"Showing {resources.Count} resources. Use --limit to see more.");
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to list resources: {ex.Message}");
            }
        }, workloadOption, resourceTypeOption, changedSinceOption, limitOption);

        return listCommand;
    }

    private static Command CreateShowCommand()
    {
        var idArgument = new Argument<string>("id", "Resource ID (can be partial)");

        var showCommand = new Command("show", "Show resource details")
        {
            idArgument
        };

        showCommand.SetHandler(async (id) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);

                var resource = await FindResourceByPartialId(context, id);

                if (resource == null)
                {
                    TableFormatter.WriteError($"Resource '{id}' not found.");
                    return;
                }

                Console.WriteLine($"Resource:     {resource.Id}");
                Console.WriteLine($"Display Name: {resource.DisplayName}");
                Console.WriteLine($"Type:         {resource.ResourceType}");
                Console.WriteLine($"             ({GetShortTypeName(resource.ResourceType)})");
                Console.WriteLine($"Workload:     {resource.Workload}");
                Console.WriteLine($"External ID:  {resource.ExternalId}");
                Console.WriteLine($"Tenant:       {resource.TenantId}");
                Console.WriteLine();
                Console.WriteLine($"First Seen:   {resource.FirstSeenAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"Last Changed: {(resource.LastChangedAt.HasValue ? resource.LastChangedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" : "Never")}");
                Console.WriteLine($"Snapshots:    {resource.SnapshotCount}");
                Console.WriteLine($"Changes:      {resource.ChangeCount}");

                if (resource.LatestSnapshotId.HasValue)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Latest Snapshot: {resource.LatestSnapshotId.Value.ToString()[..8]}");
                    Console.WriteLine();
                    Console.WriteLine("Run 'graphledger resource history {0}' to see full history.", resource.Id.ToString()[..8]);
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to show resource: {ex.Message}");
            }
        }, idArgument);

        return showCommand;
    }

    private static Command CreateHistoryCommand()
    {
        var idArgument = new Argument<string>("id", "Resource ID (can be partial)");

        var showUnchangedOption = new Option<bool>(
            aliases: ["--show-unchanged", "-u"],
            description: "Include snapshots with no changes");

        var limitOption = new Option<int>(
            aliases: ["--limit", "-n"],
            getDefaultValue: () => 20,
            description: "Maximum number of snapshots to display");

        var historyCommand = new Command("history", "Show snapshot history for a resource")
        {
            idArgument,
            showUnchangedOption,
            limitOption
        };

        historyCommand.SetHandler(async (id, showUnchanged, limit) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);

                var resource = await FindResourceByPartialId(context, id);

                if (resource == null)
                {
                    TableFormatter.WriteError($"Resource '{id}' not found.");
                    return;
                }

                Console.WriteLine($"History for: {resource.DisplayName}");
                Console.WriteLine($"Type: {GetShortTypeName(resource.ResourceType)}");
                Console.WriteLine();

                var query = context.Snapshots
                    .Where(s => s.ResourceId == resource.Id);

                if (!showUnchanged)
                    query = query.Where(s => s.HasChangesFromPrevious != false);

                var snapshots = await query
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(limit)
                    .ToListAsync();

                if (snapshots.Count == 0)
                {
                    TableFormatter.WriteWarning("No snapshots found for this resource.");
                    return;
                }

                foreach (var snapshot in snapshots)
                {
                    var changeStatus = snapshot.HasChangesFromPrevious switch
                    {
                        true => "CHANGED",
                        false => "no changes",
                        null => "initial"
                    };

                    var statusColor = snapshot.HasChangesFromPrevious switch
                    {
                        true => ConsoleColor.Yellow,
                        false => ConsoleColor.DarkGray,
                        null => ConsoleColor.Cyan
                    };

                    Console.Write($"  {snapshot.CreatedAt:yyyy-MM-dd HH:mm}  ");
                    Console.Write($"{snapshot.Id.ToString()[..8]}  ");

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = statusColor;
                    Console.Write($"[{changeStatus}]");
                    Console.ForegroundColor = originalColor;

                    Console.WriteLine();
                }

                var totalCount = await context.Snapshots.CountAsync(s => s.ResourceId == resource.Id);
                if (totalCount > snapshots.Count)
                {
                    Console.WriteLine();
                    TableFormatter.WriteInfo($"Showing {snapshots.Count} of {totalCount} snapshots. Use --limit to see more.");
                }

                if (!showUnchanged)
                {
                    var unchangedCount = await context.Snapshots
                        .CountAsync(s => s.ResourceId == resource.Id && s.HasChangesFromPrevious == false);
                    if (unchangedCount > 0)
                    {
                        TableFormatter.WriteInfo($"Hiding {unchangedCount} snapshots with no changes. Use --show-unchanged to include them.");
                    }
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to show history: {ex.Message}");
            }
        }, idArgument, showUnchangedOption, limitOption);

        return historyCommand;
    }

    private static Command CreateDiffCommand()
    {
        var idArgument = new Argument<string>("id", "Resource ID (can be partial)");

        var previousOption = new Option<bool>(
            aliases: ["--previous", "-p"],
            description: "Compare latest snapshot to the previous one");

        var fromOption = new Option<string?>(
            aliases: ["--from", "-f"],
            description: "Snapshot ID to compare from (older)");

        var toOption = new Option<string?>(
            aliases: ["--to", "-t"],
            description: "Snapshot ID to compare to (newer, defaults to latest)");

        var jsonOption = new Option<bool>(
            aliases: ["--json", "-j"],
            description: "Output raw JSON diff");

        var diffCommand = new Command("diff", "Compare snapshots of a resource")
        {
            idArgument,
            previousOption,
            fromOption,
            toOption,
            jsonOption
        };

        diffCommand.SetHandler(async (id, previous, from, to, jsonOutput) =>
        {
            try
            {
                var config = LoadAppConfiguration();
                using var context = CreateDbContext(config);

                var resource = await FindResourceByPartialId(context, id);

                if (resource == null)
                {
                    TableFormatter.WriteError($"Resource '{id}' not found.");
                    return;
                }

                Snapshot? snapshot1, snapshot2;

                if (previous || (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to)))
                {
                    // Compare latest to previous
                    var latestSnapshots = await context.Snapshots
                        .Where(s => s.ResourceId == resource.Id)
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(2)
                        .ToListAsync();

                    if (latestSnapshots.Count < 2)
                    {
                        TableFormatter.WriteError("Need at least 2 snapshots to compare.");
                        return;
                    }

                    snapshot2 = latestSnapshots[0];
                    snapshot1 = latestSnapshots[1];
                }
                else
                {
                    // Use specified snapshots
                    if (string.IsNullOrEmpty(from))
                    {
                        TableFormatter.WriteError("Specify --from snapshot or use --previous.");
                        return;
                    }

                    snapshot1 = await FindSnapshotByPartialId(context, from);
                    if (snapshot1 == null)
                    {
                        TableFormatter.WriteError($"Snapshot '{from}' not found.");
                        return;
                    }

                    if (!string.IsNullOrEmpty(to))
                    {
                        snapshot2 = await FindSnapshotByPartialId(context, to);
                        if (snapshot2 == null)
                        {
                            TableFormatter.WriteError($"Snapshot '{to}' not found.");
                            return;
                        }
                    }
                    else
                    {
                        // Default to latest
                        snapshot2 = await context.Snapshots
                            .Where(s => s.ResourceId == resource.Id)
                            .OrderByDescending(s => s.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (snapshot2 == null)
                        {
                            TableFormatter.WriteError("No snapshots found for this resource.");
                            return;
                        }
                    }
                }

                var diffEngine = new JsonDiffEngine();
                var result = diffEngine.Compare(snapshot1, snapshot2);

                if (jsonOutput)
                {
                    Console.WriteLine(result.RawDiffJson ?? "{}");
                    return;
                }

                Console.WriteLine($"Comparing snapshots for: {resource.DisplayName}");
                Console.WriteLine($"  From: {snapshot1.Id.ToString()[..8]} ({snapshot1.CreatedAt:yyyy-MM-dd HH:mm})");
                Console.WriteLine($"  To:   {snapshot2.Id.ToString()[..8]} ({snapshot2.CreatedAt:yyyy-MM-dd HH:mm})");
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
        }, idArgument, previousOption, fromOption, toOption, jsonOption);

        return diffCommand;
    }

    private static async Task<Resource?> FindResourceByPartialId(GraphLedgerDbContext context, string partialId)
    {
        if (Guid.TryParse(partialId, out var fullId))
        {
            return await context.Resources
                .Include(r => r.LatestSnapshot)
                .FirstOrDefaultAsync(r => r.Id == fullId);
        }

        // Search by partial ID
        var resources = await context.Resources
            .Include(r => r.LatestSnapshot)
            .ToListAsync();

        return resources.FirstOrDefault(r =>
            r.Id.ToString().StartsWith(partialId, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Snapshot?> FindSnapshotByPartialId(GraphLedgerDbContext context, string partialId)
    {
        if (Guid.TryParse(partialId, out var fullId))
        {
            return await context.Snapshots.FirstOrDefaultAsync(s => s.Id == fullId);
        }

        var snapshots = await context.Snapshots.ToListAsync();
        return snapshots.FirstOrDefault(s =>
            s.Id.ToString().StartsWith(partialId, StringComparison.OrdinalIgnoreCase));
    }

    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "-";
        if (value.Length <= maxLength) return value;
        return value[..(maxLength - 3)] + "...";
    }

    private static string GetShortTypeName(string resourceType)
    {
        // Extract the last part of resource type (e.g., "conditionalAccessPolicy" from "microsoft.entra.conditionalAccessPolicy")
        var parts = resourceType.Split('.');
        return parts.Length > 0 ? parts[^1] : resourceType;
    }

    private static string FormatTimeAgo(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return "Never";

        var elapsed = DateTime.UtcNow - dateTime.Value;

        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7)
            return $"{(int)elapsed.TotalDays}d ago";
        if (elapsed.TotalDays < 30)
            return $"{(int)(elapsed.TotalDays / 7)}w ago";

        return dateTime.Value.ToString("yyyy-MM-dd");
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
