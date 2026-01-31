using GraphLedger.Core.Models;

namespace GraphLedger.Core.Services;

/// <summary>
/// Manages resource lifecycle and change detection across snapshots.
/// </summary>
public interface IResourceTrackingService
{
    /// <summary>
    /// Processes a new snapshot: links to resource, computes hash, detects changes.
    /// Creates new Resource record if this is the first snapshot for this resource.
    /// </summary>
    Task<Snapshot> ProcessSnapshotAsync(Snapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Gets all resources, optionally filtered.
    /// </summary>
    Task<IReadOnlyList<Resource>> GetResourcesAsync(
        string? workload = null,
        string? resourceType = null,
        bool? hasRecentChanges = null,
        int? changedWithinDays = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the snapshot history for a specific resource.
    /// </summary>
    Task<IReadOnlyList<Snapshot>> GetResourceHistoryAsync(
        Guid resourceId,
        bool includeUnchanged = true,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets resources that changed within a time period.
    /// </summary>
    Task<IReadOnlyList<Resource>> GetRecentlyChangedResourcesAsync(
        TimeSpan period,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a single resource by ID with its latest snapshot.
    /// </summary>
    Task<Resource?> GetResourceAsync(Guid resourceId, CancellationToken ct = default);

    /// <summary>
    /// Backfills resource tracking for existing snapshots (migration).
    /// </summary>
    Task<int> BackfillResourceTrackingAsync(IProgress<int>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Gets summary statistics for dashboard display.
    /// </summary>
    Task<ResourceStatistics> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>
/// Summary statistics about tracked resources.
/// </summary>
public class ResourceStatistics
{
    public int TotalResources { get; set; }
    public int TotalSnapshots { get; set; }
    public int ResourcesChangedThisWeek { get; set; }
    public int TotalChanges { get; set; }
}
