using GraphLedger.Core.Models;

namespace GraphLedger.Core.Graph;

public interface IUtcmClient
{
    Task<Snapshot> TakeSnapshotAsync(string workload, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Snapshot>> TakeAllSnapshotsAsync(IEnumerable<string> workloads, CancellationToken cancellationToken = default);

    Task<string> GetRawConfigurationAsync(string workload, CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetSupportedWorkloads();
}
