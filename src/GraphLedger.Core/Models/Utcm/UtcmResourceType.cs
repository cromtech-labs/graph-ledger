namespace GraphLedger.Core.Models.Utcm;

/// <summary>
/// Represents a UTCM resource type with metadata.
/// </summary>
public class UtcmResourceType
{
    /// <summary>
    /// The full UTCM type name (e.g., "microsoft.entra.conditionalAccessPolicy").
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// The workload this resource type belongs to (e.g., "Entra").
    /// </summary>
    public string Workload { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for the resource type (e.g., "Conditional Access Policy").
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;
}
