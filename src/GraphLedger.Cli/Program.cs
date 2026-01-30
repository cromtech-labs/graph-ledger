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
            SnapshotCommands.CreateSnapshotCommand()
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

  2. Take a configuration snapshot:
     graphledger snapshot take

  3. List snapshots:
     graphledger snapshot list

  4. Compare snapshots:
     graphledger snapshot diff <id1> <id2>
";

        return await rootCommand.InvokeAsync(args);
    }
}
