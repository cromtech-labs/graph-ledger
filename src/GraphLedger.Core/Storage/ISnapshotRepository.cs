using GraphLedger.Core.Models;

namespace GraphLedger.Core.Storage;

public interface ISnapshotRepository
{
    Task<Snapshot> CreateAsync(Snapshot snapshot, CancellationToken cancellationToken = default);

    Task<Snapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Snapshot>> GetAllAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Snapshot>> GetByWorkloadAsync(string workload, int limit = 100, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Snapshot>> GetByTenantAsync(string tenantId, int limit = 100, CancellationToken cancellationToken = default);

    Task<Snapshot?> GetLatestByWorkloadAsync(string workload, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
}
