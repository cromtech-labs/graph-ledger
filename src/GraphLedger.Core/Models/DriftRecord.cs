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
}

public enum DriftSeverity
{
    Info,
    Warning,
    Critical
}
