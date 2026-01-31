namespace GraphLedger.Core.Models;

public class DriftRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    public Guid SnapshotId { get; set; }

    public Snapshot? Snapshot { get; set; }

    public Guid? BaselineSnapshotId { get; set; }

    public Snapshot? BaselineSnapshot { get; set; }

    public string Workload { get; set; } = string.Empty;

    public string DiffJson { get; set; } = string.Empty;

    public int ChangeCount { get; set; }

    public DriftSeverity Severity { get; set; } = DriftSeverity.Info;

    public bool IsAcknowledged { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// UTCM drift ID if this was detected by UTCM server-side drift detection.
    /// </summary>
    public string? UtcmDriftId { get; set; }

    /// <summary>
    /// UTCM monitor ID that detected this drift.
    /// </summary>
    public string? UtcmMonitorId { get; set; }

    /// <summary>
    /// UTCM resource type (e.g., "microsoft.entra.conditionalAccessPolicy").
    /// </summary>
    public string? ResourceType { get; set; }
}

public enum DriftSeverity
{
    Info,
    Warning,
    Critical
}
