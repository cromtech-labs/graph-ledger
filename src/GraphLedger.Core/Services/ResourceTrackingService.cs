using GraphLedger.Core.Models;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Core.Services;

/// <summary>
/// Manages resource lifecycle and change detection across snapshots.
/// </summary>
public class ResourceTrackingService : IResourceTrackingService
{
    private readonly IDbContextFactory<GraphLedgerDbContext> _contextFactory;
    private readonly IConfigurationHashService _hashService;

    public ResourceTrackingService(
        IDbContextFactory<GraphLedgerDbContext> contextFactory,
        IConfigurationHashService hashService)
    {
        _contextFactory = contextFactory;
        _hashService = hashService;
    }

    /// <inheritdoc />
    public async Task<Snapshot> ProcessSnapshotAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // 1. Extract external ID from configuration JSON
        var externalId = _hashService.ExtractExternalId(snapshot.ConfigurationJson);
        snapshot.ExternalId = externalId;

        // 2. Compute configuration hash
        snapshot.ConfigurationHash = _hashService.ComputeHash(snapshot.ConfigurationJson);

        // If no external ID or resource type, we can't track this resource
        if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(snapshot.UtcmResourceType))
        {
            snapshot.HasChangesFromPrevious = null;
            return snapshot;
        }

        // 3. Find or create Resource record
        var resource = await FindOrCreateResourceAsync(
            context,
            externalId,
            snapshot.UtcmResourceType,
            snapshot.ResourceDisplayName ?? "",
            snapshot.Workload,
            snapshot.TenantId,
            ct);

        snapshot.ResourceId = resource.Id;

        // 4. Find previous snapshot of this resource
        var previousSnapshot = await context.Snapshots
            .Where(s => s.ResourceId == resource.Id && s.Id != snapshot.Id)
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

        // 7. Update resource counters and latest reference
        resource.SnapshotCount++;
        resource.LatestSnapshotId = snapshot.Id;
        resource.DisplayName = snapshot.ResourceDisplayName ?? resource.DisplayName;

        context.Snapshots.Update(snapshot);
        await context.SaveChangesAsync(ct);

        return snapshot;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Resource>> GetResourcesAsync(
        string? workload = null,
        string? resourceType = null,
        bool? hasRecentChanges = null,
        int? changedWithinDays = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Resources.AsQueryable();

        if (!string.IsNullOrEmpty(workload))
            query = query.Where(r => r.Workload == workload);

        if (!string.IsNullOrEmpty(resourceType))
            query = query.Where(r => r.ResourceType == resourceType);

        if (changedWithinDays.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-changedWithinDays.Value);
            query = query.Where(r => r.LastChangedAt >= cutoff);
        }

        if (hasRecentChanges == true)
            query = query.Where(r => r.ChangeCount > 1);

        return await query
            .OrderByDescending(r => r.LastChangedAt ?? r.FirstSeenAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Snapshot>> GetResourceHistoryAsync(
        Guid resourceId,
        bool includeUnchanged = true,
        int? limit = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.Snapshots
            .Where(s => s.ResourceId == resourceId);

        if (!includeUnchanged)
            query = query.Where(s => s.HasChangesFromPrevious != false);

        query = query.OrderByDescending(s => s.CreatedAt);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Resource>> GetRecentlyChangedResourcesAsync(
        TimeSpan period,
        int? limit = null,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var cutoff = DateTime.UtcNow - period;

        var query = context.Resources
            .Where(r => r.LastChangedAt >= cutoff)
            .OrderByDescending(r => r.LastChangedAt);

        if (limit.HasValue)
            return await query.Take(limit.Value).ToListAsync(ct);

        return await query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Resource?> GetResourceAsync(Guid resourceId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Resources
            .Include(r => r.LatestSnapshot)
            .FirstOrDefaultAsync(r => r.Id == resourceId, ct);
    }

    /// <inheritdoc />
    public async Task<int> BackfillResourceTrackingAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Get all snapshots without resource tracking, ordered by creation date
        var snapshots = await context.Snapshots
            .Where(s => s.ResourceId == null)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        var processed = 0;

        foreach (var snapshot in snapshots)
        {
            ct.ThrowIfCancellationRequested();

            // Extract external ID
            var externalId = _hashService.ExtractExternalId(snapshot.ConfigurationJson);
            snapshot.ExternalId = externalId;

            // Compute hash
            snapshot.ConfigurationHash = _hashService.ComputeHash(snapshot.ConfigurationJson);

            if (!string.IsNullOrEmpty(externalId) && !string.IsNullOrEmpty(snapshot.UtcmResourceType))
            {
                // Find or create resource
                var resource = await FindOrCreateResourceAsync(
                    context,
                    externalId,
                    snapshot.UtcmResourceType,
                    snapshot.ResourceDisplayName ?? "",
                    snapshot.Workload,
                    snapshot.TenantId,
                    ct);

                snapshot.ResourceId = resource.Id;

                // Find previous snapshot of this resource (by date)
                var previousSnapshot = await context.Snapshots
                    .Where(s => s.ResourceId == resource.Id && s.CreatedAt < snapshot.CreatedAt)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (previousSnapshot != null)
                {
                    snapshot.PreviousSnapshotId = previousSnapshot.Id;
                    snapshot.HasChangesFromPrevious =
                        snapshot.ConfigurationHash != previousSnapshot.ConfigurationHash;

                    if (snapshot.HasChangesFromPrevious == true)
                    {
                        resource.ChangeCount++;
                        if (resource.LastChangedAt == null || snapshot.CreatedAt > resource.LastChangedAt)
                            resource.LastChangedAt = snapshot.CreatedAt;
                    }
                }
                else
                {
                    // First snapshot for this resource - initial discovery, not a change
                    snapshot.HasChangesFromPrevious = null;
                    resource.FirstSeenAt = snapshot.CreatedAt;
                    resource.LastChangedAt = null; // Never changed yet
                    resource.ChangeCount = 0; // No changes yet
                }

                resource.SnapshotCount++;
                if (resource.LatestSnapshotId == null || snapshot.CreatedAt > (resource.LatestSnapshot?.CreatedAt ?? DateTime.MinValue))
                {
                    resource.LatestSnapshotId = snapshot.Id;
                }
                resource.DisplayName = snapshot.ResourceDisplayName ?? resource.DisplayName;
            }

            processed++;
            progress?.Report(processed);

            // Save in batches of 100
            if (processed % 100 == 0)
            {
                await context.SaveChangesAsync(ct);
            }
        }

        // Save any remaining changes
        await context.SaveChangesAsync(ct);

        return processed;
    }

    /// <inheritdoc />
    public async Task<ResourceStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var weekAgo = DateTime.UtcNow.AddDays(-7);

        return new ResourceStatistics
        {
            TotalResources = await context.Resources.CountAsync(ct),
            TotalSnapshots = await context.Snapshots.CountAsync(ct),
            ResourcesChangedThisWeek = await context.Resources
                .CountAsync(r => r.LastChangedAt >= weekAgo, ct),
            TotalChanges = await context.Snapshots
                .CountAsync(s => s.HasChangesFromPrevious == true, ct)
        };
    }

    /// <summary>
    /// Finds an existing resource or creates a new one.
    /// </summary>
    private async Task<Resource> FindOrCreateResourceAsync(
        GraphLedgerDbContext context,
        string externalId,
        string resourceType,
        string displayName,
        string workload,
        string tenantId,
        CancellationToken ct)
    {
        var resource = await context.Resources
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
            context.Resources.Add(resource);
            await context.SaveChangesAsync(ct);
        }

        return resource;
    }
}
