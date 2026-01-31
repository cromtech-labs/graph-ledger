namespace GraphLedger.Core.Models;

/// <summary>
/// Tracks a unique resource across all snapshots.
/// A resource is identified by its ExternalId + ResourceType combination.
/// </summary>
public class Resource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The stable identifier from Graph API (e.g., Conditional Access Policy ID).
    /// Combined with ResourceType, uniquely identifies a resource.
    /// </summary>
    public string ExternalId { get; set; } = "";

    /// <summary>
    /// UTCM resource type (e.g., "microsoft.entra.conditionalAccessPolicy")
    /// </summary>
    public string ResourceType { get; set; } = "";

    /// <summary>
    /// Human-readable name (may change over time, updated on each snapshot)
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Workload category (Entra, Intune, Exchange, etc.)
    /// </summary>
    public string Workload { get; set; } = "";

    /// <summary>
    /// Tenant ID this resource belongs to
    /// </summary>
    public string TenantId { get; set; } = "";

    /// <summary>
    /// When this resource was first seen
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// When the resource configuration last changed (not just last snapshot)
    /// </summary>
    public DateTime? LastChangedAt { get; set; }

    /// <summary>
    /// Total number of snapshots for this resource
    /// </summary>
    public int SnapshotCount { get; set; }

    /// <summary>
    /// Number of snapshots where configuration changed from previous
    /// </summary>
    public int ChangeCount { get; set; }

    /// <summary>
    /// Reference to the most recent snapshot
    /// </summary>
    public Guid? LatestSnapshotId { get; set; }
    public Snapshot? LatestSnapshot { get; set; }

    /// <summary>
    /// All snapshots for this resource
    /// </summary>
    public ICollection<Snapshot> Snapshots { get; set; } = new List<Snapshot>();
}
