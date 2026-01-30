using System.Text.Json;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models;
using Microsoft.Graph.Beta;

namespace GraphLedger.Core.Graph;

public class UtcmClient : IUtcmClient
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _tenantId;

    private static readonly Dictionary<string, Func<GraphServiceClient, CancellationToken, Task<object?>>> WorkloadFetchers = new()
    {
        ["deviceManagement"] = async (client, ct) => await client.DeviceManagement.GetAsync(cancellationToken: ct),
        ["conditionalAccess"] = async (client, ct) => await client.Identity.ConditionalAccess.Policies.GetAsync(cancellationToken: ct),
        ["identityGovernance"] = async (client, ct) => await client.IdentityGovernance.GetAsync(cancellationToken: ct),
        ["deviceConfigurations"] = async (client, ct) => await client.DeviceManagement.DeviceConfigurations.GetAsync(cancellationToken: ct),
        ["compliancePolicies"] = async (client, ct) => await client.DeviceManagement.DeviceCompliancePolicies.GetAsync(cancellationToken: ct),
        ["appProtectionPolicies"] = async (client, ct) => await client.DeviceAppManagement.ManagedAppPolicies.GetAsync(cancellationToken: ct),
    };

    public UtcmClient(GraphAuthProvider authProvider, string tenantId)
    {
        _graphClient = authProvider.CreateClient();
        _tenantId = tenantId;
    }

    public UtcmClient(GraphServiceClient graphClient, string tenantId)
    {
        _graphClient = graphClient;
        _tenantId = tenantId;
    }

    public async Task<Snapshot> TakeSnapshotAsync(string workload, CancellationToken cancellationToken = default)
    {
        var configJson = await GetRawConfigurationAsync(workload, cancellationToken);

        return new Snapshot
        {
            TenantId = _tenantId,
            Workload = workload,
            ConfigurationJson = configJson,
            Source = SnapshotSource.Manual,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<IReadOnlyList<Snapshot>> TakeAllSnapshotsAsync(IEnumerable<string> workloads, CancellationToken cancellationToken = default)
    {
        var snapshots = new List<Snapshot>();

        foreach (var workload in workloads)
        {
            try
            {
                var snapshot = await TakeSnapshotAsync(workload, cancellationToken);
                snapshots.Add(snapshot);
            }
            catch (Exception ex)
            {
                snapshots.Add(new Snapshot
                {
                    TenantId = _tenantId,
                    Workload = workload,
                    ConfigurationJson = JsonSerializer.Serialize(new { error = ex.Message }),
                    Source = SnapshotSource.Manual,
                    Description = $"Error: {ex.Message}"
                });
            }
        }

        return snapshots;
    }

    public async Task<string> GetRawConfigurationAsync(string workload, CancellationToken cancellationToken = default)
    {
        if (!WorkloadFetchers.TryGetValue(workload, out var fetcher))
        {
            throw new ArgumentException($"Unsupported workload: {workload}. Supported workloads: {string.Join(", ", WorkloadFetchers.Keys)}");
        }

        var result = await fetcher(_graphClient, cancellationToken);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(result, options);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var org = await _graphClient.Organization.GetAsync(cancellationToken: cancellationToken);
            return org?.Value?.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetSupportedWorkloads()
    {
        return WorkloadFetchers.Keys.ToList();
    }
}
