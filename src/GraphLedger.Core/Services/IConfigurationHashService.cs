namespace GraphLedger.Core.Services;

/// <summary>
/// Service for computing configuration hashes and extracting resource identifiers.
/// </summary>
public interface IConfigurationHashService
{
    /// <summary>
    /// Computes SHA256 hash of normalized JSON configuration.
    /// Normalization: sorted keys, no whitespace, consistent formatting.
    /// </summary>
    string ComputeHash(string configurationJson);

    /// <summary>
    /// Extracts the external ID from configuration JSON.
    /// Looks for "id" field at root level.
    /// </summary>
    string? ExtractExternalId(string configurationJson);
}
