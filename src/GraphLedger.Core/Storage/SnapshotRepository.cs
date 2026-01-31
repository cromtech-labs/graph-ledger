using GraphLedger.Core.Models;
using GraphLedger.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Core.Storage;

public class SnapshotRepository : ISnapshotRepository
{
    private readonly GraphLedgerDbContext _context;
    private readonly IConfigurationHashService _hashService;

    public SnapshotRepository(GraphLedgerDbContext context, IConfigurationHashService hashService)
    {
        _context = context;
        _hashService = hashService;
    }

    public async Task<Snapshot> CreateAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        // Process resource tracking before saving
        await ProcessResourceTrackingAsync(snapshot, cancellationToken);

        _context.Snapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        // Update LatestSnapshotId on resource after snapshot is saved (avoids circular dependency)
        if (snapshot.ResourceId.HasValue)
        {
            var resource = await _context.Resources.FindAsync(new object[] { snapshot.ResourceId.Value }, cancellationToken);
            if (resource != null)
            {
                resource.LatestSnapshotId = snapshot.Id;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        return snapshot;
    }

    public async Task<Snapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> GetAllAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> GetByWorkloadAsync(string workload, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.Workload == workload)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> GetByTenantAsync(string tenantId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Snapshot?> GetLatestByWorkloadAsync(string workload, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.Workload == workload)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.Snapshots.FindAsync(new object[] { id }, cancellationToken);
        if (snapshot == null)
        {
            return false;
        }

        _context.Snapshots.Remove(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        var oldSnapshots = await _context.Snapshots
            .Where(s => s.CreatedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        _context.Snapshots.RemoveRange(oldSnapshots);
        await _context.SaveChangesAsync(cancellationToken);
        return oldSnapshots.Count;
    }

    /// <summary>
    /// Processes resource tracking for a snapshot before saving.
    /// Links to existing resource or creates new one, detects changes.
    /// </summary>
    private async Task ProcessResourceTrackingAsync(Snapshot snapshot, CancellationToken ct)
    {
        // 1. Extract external ID from configuration JSON
        var externalId = _hashService.ExtractExternalId(snapshot.ConfigurationJson);
        snapshot.ExternalId = externalId;

        // 2. Compute configuration hash
        snapshot.ConfigurationHash = _hashService.ComputeHash(snapshot.ConfigurationJson);

        // If no external ID or resource type, we can't track this resource
        if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(snapshot.UtcmResourceType))
        {
            snapshot.HasChangesFromPrevious = null;
            return;
        }

        // 3. Find or create Resource record
        var (resource, isNew) = await FindOrCreateResourceAsync(
            externalId,
            snapshot.UtcmResourceType,
            snapshot.ResourceDisplayName ?? "",
            snapshot.Workload,
            snapshot.TenantId,
            ct);

        snapshot.ResourceId = resource.Id;

        // 4. Find previous snapshot of this resource
        var previousSnapshot = await _context.Snapshots
            .Where(s => s.ResourceId == resource.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (previousSnapshot != null)
        {
            snapshot.PreviousSnapshotId = previousSnapshot.Id;

            // 5. Compare hashes to detect changes
            snapshot.HasChangesFromPrevious =
                snapshot.ConfigurationHash != previousSnapshot.ConfigurationHash;

            // 6. Update resource metadata
            if (snapshot.HasChangesFromPrevious == true)
            {
                resource.LastChangedAt = snapshot.CreatedAt;
                resource.ChangeCount++;
            }
        }
        else
        {
            // First snapshot for this resource - initial discovery, not a change
            snapshot.HasChangesFromPrevious = null; // No previous to compare
            resource.FirstSeenAt = snapshot.CreatedAt;
            resource.LastChangedAt = null; // Never changed yet (just discovered)
            resource.ChangeCount = 0; // No changes yet
        }

        // 7. Update resource counters
        resource.SnapshotCount++;
        resource.DisplayName = snapshot.ResourceDisplayName ?? resource.DisplayName;

        // Note: LatestSnapshotId will be updated after snapshot is saved to avoid circular dependency
    }

    /// <summary>
    /// Finds an existing resource or creates a new one.
    /// Returns the resource and whether it was newly created.
    /// </summary>
    private async Task<(Resource resource, bool isNew)> FindOrCreateResourceAsync(
        string externalId,
        string resourceType,
        string displayName,
        string workload,
        string tenantId,
        CancellationToken ct)
    {
        var resource = await _context.Resources
            .FirstOrDefaultAsync(r => r.ExternalId == externalId && r.ResourceType == resourceType, ct);

        if (resource == null)
        {
            resource = new Resource
            {
                ExternalId = externalId,
                ResourceType = resourceType,
                DisplayName = displayName,
                Workload = workload,
                TenantId = tenantId,
                FirstSeenAt = DateTime.UtcNow,
                SnapshotCount = 0,
                ChangeCount = 0
            };
            _context.Resources.Add(resource);
            // Save resource first to avoid circular dependency
            await _context.SaveChangesAsync(ct);
            return (resource, true);
        }

        return (resource, false);
    }
}
