using System.CommandLine;
using System.Text.Json;
using GraphLedger.Cli.Output;
using GraphLedger.Core.Graph;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models;

namespace GraphLedger.Cli.Commands;

public static class DriftCommands
{
    public static Command CreateDriftCommand()
    {
        var driftCommand = new Command("drift", "View UTCM drift detection results");

        driftCommand.AddCommand(CreateListCommand());
        driftCommand.AddCommand(CreateShowCommand());

        return driftCommand;
    }

    private static Command CreateListCommand()
    {
        var monitorOption = new Option<string?>(
            aliases: ["--monitor", "-m"],
            description: "Filter by monitor ID");

        var statusOption = new Option<string?>(
            aliases: ["--status", "-s"],
            description: "Filter by status (active, fixed)");

        var listCommand = new Command("list", "List detected drifts")
        {
            monitorOption,
            statusOption
        };

        listCommand.SetHandler(async (monitorId, status) =>
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

                var drifts = await utcmClient.ListDriftsAsync(monitorId);

                // Filter by status if specified
                if (!string.IsNullOrEmpty(status))
                {
                    drifts = drifts
                        .Where(d => string.Equals(d.Status, status, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (drifts.Count == 0)
                {
                    TableFormatter.WriteSuccess("No drifts detected.");
                    return;
                }

                TableFormatter.WriteWarning($"Found {drifts.Count} drift(s):");
                Console.WriteLine();

                TableFormatter.WriteTable(drifts,
                    ("ID", d => d.Id[..Math.Min(8, d.Id.Length)]),
                    ("Resource", d => TruncateString(d.BaselineResourceDisplayName, 25)),
                    ("Type", d => UtcmResourceTypeRegistry.GetFriendlyName(d.ResourceType)),
                    ("Status", d => d.Status),
                    ("Properties", d => d.DriftedProperties.Count.ToString()),
                    ("First Reported", d => d.FirstReportedDateTime.ToString("yyyy-MM-dd HH:mm"))
                );
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to list drifts: {ex.Message}");
            }
        }, monitorOption, statusOption);

        return listCommand;
    }

    private static Command CreateShowCommand()
    {
        var idArgument = new Argument<string>("id", "Drift ID");

        var jsonOption = new Option<bool>(
            aliases: ["--json", "-j"],
            description: "Output as JSON");

        var showCommand = new Command("show", "Show drift details")
        {
            idArgument,
            jsonOption
        };

        showCommand.SetHandler(async (id, jsonOutput) =>
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

                var drift = await utcmClient.GetDriftAsync(id);

                if (drift == null)
                {
                    TableFormatter.WriteError($"Drift '{id}' not found.");
                    return;
                }

                if (jsonOutput)
                {
                    var json = JsonSerializer.Serialize(drift, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    Console.WriteLine(json);
                    return;
                }

                Console.WriteLine($"Drift ID:      {drift.Id}");
                Console.WriteLine($"Monitor ID:    {drift.MonitorId}");
                Console.WriteLine($"Resource:      {drift.BaselineResourceDisplayName}");
                Console.WriteLine($"Resource Type: {UtcmResourceTypeRegistry.GetFriendlyName(drift.ResourceType)}");
                Console.WriteLine($"               ({drift.ResourceType})");
                Console.WriteLine($"Status:        {drift.Status}");
                Console.WriteLine($"First Report:  {drift.FirstReportedDateTime:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();

                if (drift.DriftedProperties.Count > 0)
                {
                    Console.WriteLine($"Drifted Properties ({drift.DriftedProperties.Count}):");
                    Console.WriteLine();

                    foreach (var prop in drift.DriftedProperties)
                    {
                        Console.WriteLine($"  Property: {prop.PropertyName}");

                        var originalColor = Console.ForegroundColor;

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"    Expected: {prop.DesiredValue ?? "(null)"}");

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"    Actual:   {prop.CurrentValue ?? "(null)"}");

                        Console.ForegroundColor = originalColor;
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine("No detailed property drift information available.");
                }
            }
            catch (Exception ex)
            {
                TableFormatter.WriteError($"Failed to show drift: {ex.Message}");
            }
        }, idArgument, jsonOption);

        return showCommand;
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
}
