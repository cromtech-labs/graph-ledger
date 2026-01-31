namespace GraphLedger.Core.Models.Utcm;

/// <summary>
/// Represents a UTCM snapshot job that captures tenant configuration.
/// </summary>
public class UtcmSnapshotJob
{
    /// <summary>
    /// Unique identifier for the snapshot job.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the snapshot job.
    /// </summary>
    public UtcmJobStatus Status { get; set; }

    /// <summary>
    /// When the snapshot job was created.
    /// </summary>
    public DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// When the snapshot job completed (if finished).
    /// </summary>
    public DateTime? CompletedDateTime { get; set; }

    /// <summary>
    /// Display name for the snapshot job.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// List of UTCM resource types included in this snapshot.
    /// </summary>
    public IReadOnlyList<string> Resources { get; set; } = [];

    /// <summary>
    /// URL to fetch the snapshot results when completed.
    /// </summary>
    public string? ResourceLocation { get; set; }

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The snapshot data retrieved from the job (populated after fetching results).
    /// </summary>
    public IReadOnlyList<UtcmSnapshotResource>? SnapshotData { get; set; }
}

/// <summary>
/// Represents a single resource captured in a UTCM snapshot.
/// </summary>
public class UtcmSnapshotResource
{
    /// <summary>
    /// Display name of the resource.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// UTCM resource type (e.g., "microsoft.entra.conditionalAccessPolicy").
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// The resource properties as a dictionary.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();
}
