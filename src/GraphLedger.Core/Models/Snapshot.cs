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
}

public enum SnapshotSource
{
    Manual,
    Scheduled,
    Webhook
}
