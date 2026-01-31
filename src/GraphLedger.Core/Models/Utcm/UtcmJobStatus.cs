namespace GraphLedger.Core.Models.Utcm;

/// <summary>
/// Status of a UTCM snapshot job.
/// </summary>
public enum UtcmJobStatus
{
    NotStarted,
    Running,
    Succeeded,
    Failed
}
