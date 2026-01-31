namespace GraphLedger.Core.Models;

/// <summary>
/// Local cache of UTCM monitor information for tracking and reference.
/// </summary>
public class UtcmMonitorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The UTCM monitor ID from the Graph API.
    /// </summary>
    public string UtcmMonitorId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the monitor.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized list of resource types being monitored.
    /// </summary>
    public string ResourceTypesJson { get; set; } = "[]";

    /// <summary>
    /// When this record was last synced with UTCM.
    /// </summary>
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the monitor was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current status of the monitor (active, inactive).
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// How often the monitor checks for drift, in hours.
    /// </summary>
    public int FrequencyInHours { get; set; } = 24;
}
