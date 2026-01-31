namespace GraphLedger.Core.Models;

public class Snapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string TenantId { get; set; } = string.Empty;

    public string Workload { get; set; } = string.Empty;

    public string ConfigurationJson { get; set; } = string.Empty;

    public string? Description { get; set; }

    public SnapshotSource Source { get; set; } = SnapshotSource.Manual;

    public string? TriggeredBy { get; set; }

    /// <summary>
    /// UTCM snapshot job ID that created this snapshot.
    /// </summary>
    public string? UtcmJobId { get; set; }

    /// <summary>
    /// UTCM resource type (e.g., "microsoft.entra.conditionalAccessPolicy").
    /// </summary>
    public string? UtcmResourceType { get; set; }

    /// <summary>
    /// Friendly display name from UTCM for the resource.
    /// </summary>
    public string? ResourceDisplayName { get; set; }

    // ========== Resource Tracking Fields ==========

    /// <summary>
    /// Link to the tracked resource.
    /// </summary>
    public Guid? ResourceId { get; set; }
    public Resource? Resource { get; set; }

    /// <summary>
    /// The Graph API ID extracted from the configuration JSON.
    /// Used to match snapshots to resources.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// SHA256 hash of normalized ConfigurationJson for quick equality checks.
    /// Two snapshots with the same hash are identical.
    /// </summary>
    public string? ConfigurationHash { get; set; }

    /// <summary>
    /// True if this snapshot's configuration differs from the previous snapshot
    /// of the same resource. Null for first snapshot or unknown.
    /// </summary>
    public bool? HasChangesFromPrevious { get; set; }

    /// <summary>
    /// Reference to the previous snapshot of the same resource (if any)
    /// </summary>
    public Guid? PreviousSnapshotId { get; set; }
    public Snapshot? PreviousSnapshot { get; set; }
}

public enum SnapshotSource
{
    Manual,
    Scheduled,
    Webhook
}
