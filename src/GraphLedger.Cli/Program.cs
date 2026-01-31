using System.CommandLine;
using GraphLedger.Cli.Commands;

namespace GraphLedger.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("GraphLedger - M365 Configuration Management Tool")
        {
            ConfigCommands.CreateConfigCommand(),
            SnapshotCommands.CreateSnapshotCommand(),
            MonitorCommands.CreateMonitorCommand(),
            DriftCommands.CreateDriftCommand(),
            WorkloadCommands.CreateWorkloadCommand()
        };

        rootCommand.Description = @"
GraphLedger is a configuration management tool for Microsoft 365.

It uses the Unified Tenant Configuration Management (UTCM) APIs to capture,
compare, and track configuration changes across your M365 tenant.

Getting Started:
  1. Configure Azure credentials:
     graphledger config set Azure:TenantId <your-tenant-id>
     graphledger config set Azure:ClientId <your-client-id>
     graphledger config set Azure:ClientSecret <your-client-secret>

  2. View available workloads:
     graphledger workload list

  3. Take a configuration snapshot:
     graphledger snapshot take --workloads Entra

  4. Create a drift monitor:
     graphledger monitor create ""My Monitor"" --workloads Entra

  5. View detected drifts:
     graphledger drift list

Commands:
  config    - Manage configuration settings
  snapshot  - Take and manage configuration snapshots
  monitor   - Create and manage UTCM drift monitors
  drift     - View drift detection results
  workload  - Explore available UTCM workloads and resource types
";

        return await rootCommand.InvokeAsync(args);
    }
}
