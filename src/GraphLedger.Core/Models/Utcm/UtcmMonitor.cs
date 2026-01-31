namespace GraphLedger.Core.Models.Utcm;

/// <summary>
/// Represents a UTCM configuration monitor that tracks drift from a baseline.
/// </summary>
public class UtcmMonitor
{
    /// <summary>
    /// Unique identifier for the monitor.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the monitor.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the monitor's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current status of the monitor (active, inactive).
    /// </summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// How often the monitor runs drift detection, in hours.
    /// </summary>
    public int MonitorRunFrequencyInHours { get; set; } = 24;

    /// <summary>
    /// When the monitor was created.
    /// </summary>
    public DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// The baseline configuration that drift is measured against.
    /// </summary>
    public UtcmBaseline Baseline { get; set; } = new();
}

/// <summary>
/// Represents a baseline configuration for drift detection.
/// </summary>
public class UtcmBaseline
{
    /// <summary>
    /// Display name for the baseline.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the baseline.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The resources included in this baseline.
    /// </summary>
    public IReadOnlyList<UtcmBaselineResource> Resources { get; set; } = [];
}

/// <summary>
/// Represents a single resource in a baseline configuration.
/// </summary>
public class UtcmBaselineResource
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
    /// The baseline property values for this resource.
    /// </summary>
    public Dictionary<string, object?> Properties { get; set; } = new();
}
