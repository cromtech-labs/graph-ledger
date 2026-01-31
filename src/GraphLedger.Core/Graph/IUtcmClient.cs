using GraphLedger.Core.Models.Utcm;

namespace GraphLedger.Core.Graph;

/// <summary>
/// Client interface for interacting with the Microsoft Graph UTCM
/// (Unified Tenant Configuration Management) APIs.
/// </summary>
public interface IUtcmClient
{
    #region Snapshot Operations

    /// <summary>
    /// Creates a new UTCM snapshot job for the specified resource types.
    /// </summary>
    /// <param name="displayName">Display name for the snapshot.</param>
    /// <param name="resourceTypes">UTCM resource types to include in the snapshot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created snapshot job (in NotStarted or Running state).</returns>
    Task<UtcmSnapshotJob> CreateSnapshotAsync(string displayName, IEnumerable<string> resourceTypes, CancellationToken ct = default);

    /// <summary>
    /// Gets the status of a snapshot job.
    /// </summary>
    /// <param name="jobId">The snapshot job ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The snapshot job with current status.</returns>
    Task<UtcmSnapshotJob> GetSnapshotJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Waits for a snapshot job to complete, polling until success or failure.
    /// </summary>
    /// <param name="jobId">The snapshot job ID.</param>
    /// <param name="timeout">Maximum time to wait (default 10 minutes).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The completed snapshot job with results.</returns>
    Task<UtcmSnapshotJob> WaitForSnapshotAsync(string jobId, TimeSpan? timeout = null, CancellationToken ct = default);

    /// <summary>
    /// Fetches the snapshot data from a completed snapshot job.
    /// </summary>
    /// <param name="job">The completed snapshot job.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The snapshot job with populated SnapshotData.</returns>
    Task<UtcmSnapshotJob> FetchSnapshotDataAsync(UtcmSnapshotJob job, CancellationToken ct = default);

    #endregion

    #region Monitor Operations

    /// <summary>
    /// Creates a new UTCM monitor for drift detection.
    /// </summary>
    /// <param name="displayName">Display name for the monitor.</param>
    /// <param name="baseline">The baseline configuration to monitor against.</param>
    /// <param name="frequencyInHours">How often to check for drift (default 24 hours).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created monitor.</returns>
    Task<UtcmMonitor> CreateMonitorAsync(string displayName, UtcmBaseline baseline, int frequencyInHours = 24, CancellationToken ct = default);

    /// <summary>
    /// Lists all UTCM monitors for the tenant.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of monitors.</returns>
    Task<IReadOnlyList<UtcmMonitor>> ListMonitorsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a specific monitor by ID.
    /// </summary>
    /// <param name="monitorId">The monitor ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The monitor, or null if not found.</returns>
    Task<UtcmMonitor?> GetMonitorAsync(string monitorId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a monitor.
    /// </summary>
    /// <param name="monitorId">The monitor ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteMonitorAsync(string monitorId, CancellationToken ct = default);

    #endregion

    #region Drift Operations

    /// <summary>
    /// Lists drifts detected by UTCM monitors.
    /// </summary>
    /// <param name="monitorId">Optional monitor ID to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected drifts.</returns>
    Task<IReadOnlyList<UtcmDrift>> ListDriftsAsync(string? monitorId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a specific drift record by ID.
    /// </summary>
    /// <param name="driftId">The drift ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The drift record, or null if not found.</returns>
    Task<UtcmDrift?> GetDriftAsync(string driftId, CancellationToken ct = default);

    #endregion

    #region Utility

    /// <summary>
    /// Tests the connection to the UTCM API.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if connection successful, false otherwise.</returns>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the list of supported workload names.
    /// </summary>
    IReadOnlyList<string> GetSupportedWorkloads();

    /// <summary>
    /// Gets the resource types available for a specific workload.
    /// </summary>
    /// <param name="workload">The workload name.</param>
    /// <returns>List of UTCM resource type names.</returns>
    IReadOnlyList<string> GetResourceTypesForWorkload(string workload);

    #endregion
}
