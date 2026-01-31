using System.CommandLine;
using GraphLedger.Cli.Output;
using GraphLedger.Core.Graph;

namespace GraphLedger.Cli.Commands;

public static class WorkloadCommands
{
    public static Command CreateWorkloadCommand()
    {
        var workloadCommand = new Command("workload", "Explore UTCM workloads and resource types");

        workloadCommand.AddCommand(CreateListCommand());
        workloadCommand.AddCommand(CreateResourcesCommand());

        return workloadCommand;
    }

    private static Command CreateListCommand()
    {
        var listCommand = new Command("list", "List all supported UTCM workloads");

        listCommand.SetHandler(() =>
        {
            var workloads = UtcmResourceTypeRegistry.GetWorkloads();

            Console.WriteLine("Supported UTCM Workloads:");
            Console.WriteLine();

            foreach (var workload in workloads)
            {
                var resourceTypes = UtcmResourceTypeRegistry.GetResourceTypes(workload);
                Console.WriteLine($"  {workload}");
                Console.WriteLine($"    Resource types: {resourceTypes.Count}");
            }

            Console.WriteLine();
            Console.WriteLine("Use 'graphledger workload resources <name>' to see resource types for a workload.");
        });

        return listCommand;
    }

    private static Command CreateResourcesCommand()
    {
        var nameArgument = new Argument<string>("name", "Workload name");

        var resourcesCommand = new Command("resources", "List resource types for a workload")
        {
            nameArgument
        };

        resourcesCommand.SetHandler((name) =>
        {
            if (!UtcmResourceTypeRegistry.IsValidWorkload(name))
            {
                TableFormatter.WriteError($"Unknown workload: {name}");
                TableFormatter.WriteInfo($"Valid workloads: {string.Join(", ", UtcmResourceTypeRegistry.GetWorkloads())}");
                return;
            }

            var resourceTypes = UtcmResourceTypeRegistry.GetResourceTypeInfo(name);

            Console.WriteLine($"Resource types for {name}:");
            Console.WriteLine();

            TableFormatter.WriteTable(resourceTypes,
                ("Type Name", r => r.TypeName),
                ("Friendly Name", r => r.FriendlyName)
            );

            Console.WriteLine();
            Console.WriteLine("Use these type names with:");
            Console.WriteLine("  graphledger snapshot take --resource-types <type1> <type2>");
            Console.WriteLine("  graphledger monitor create <name> --resource-types <type1> <type2>");
        }, nameArgument);

        return resourcesCommand;
    }
}
