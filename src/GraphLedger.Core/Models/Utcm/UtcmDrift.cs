namespace GraphLedger.Core.Models.Utcm;

/// <summary>
/// Represents a drift detected by UTCM between current configuration and baseline.
/// </summary>
public class UtcmDrift
{
    /// <summary>
    /// Unique identifier for the drift record.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the monitor that detected this drift.
    /// </summary>
    public string MonitorId { get; set; } = string.Empty;

    /// <summary>
    /// UTCM resource type that drifted.
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the baseline resource that drifted.
    /// </summary>
    public string BaselineResourceDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When the drift was first detected.
    /// </summary>
    public DateTime FirstReportedDateTime { get; set; }

    /// <summary>
    /// Current status of the drift (active, fixed).
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// The properties that have drifted from the baseline.
    /// </summary>
    public IReadOnlyList<UtcmDriftedProperty> DriftedProperties { get; set; } = [];
}

/// <summary>
/// Represents a single property that has drifted from the baseline value.
/// </summary>
public class UtcmDriftedProperty
{
    /// <summary>
    /// Name of the property that drifted.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Current value of the property.
    /// </summary>
    public string? CurrentValue { get; set; }

    /// <summary>
    /// Expected value from the baseline.
    /// </summary>
    public string? DesiredValue { get; set; }
}
