using System.Text.Json;
using GraphLedger.Core.Diff;
using GraphLedger.Core.Graph;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models;
using GraphLedger.Core.Models.Utcm;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace GraphLedger.Service.Jobs;

[DisallowConcurrentExecution]
public class SnapshotPollingJob : IJob
{
    private readonly ILogger<SnapshotPollingJob> _logger;
    private readonly IOptions<AppConfiguration> _config;
    private readonly IDbContextFactory<GraphLedgerDbContext> _contextFactory;

    public SnapshotPollingJob(
        ILogger<SnapshotPollingJob> logger,
        IOptions<AppConfiguration> config,
        IDbContextFactory<GraphLedgerDbContext> contextFactory)
    {
        _logger = logger;
        _config = config;
        _contextFactory = contextFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting scheduled snapshot polling job");

        var config = _config.Value;

        if (string.IsNullOrEmpty(config.Azure.TenantId) ||
            string.IsNullOrEmpty(config.Azure.ClientId))
        {
            _logger.LogWarning("Azure credentials not configured. Skipping snapshot.");
            return;
        }

        try
        {
            var authProvider = new GraphAuthProvider(config.Azure);
            var utcmClient = new UtcmClient(authProvider, config.Azure.TenantId);
            var diffEngine = new JsonDiffEngine();

            await using var dbContext = await _contextFactory.CreateDbContextAsync(context.CancellationToken);
            var snapshotRepository = new SnapshotRepository(dbContext);

            // Filter to valid UTCM workloads
            var utcmWorkloads = config.EnabledWorkloads
                .Where(UtcmResourceTypeRegistry.IsValidWorkload)
                .DefaultIfEmpty("Entra")
                .ToList();

            foreach (var workload in utcmWorkloads)
            {
                try
                {
                    await ProcessWorkloadAsync(
                        utcmClient,
                        snapshotRepository,
                        diffEngine,
                        dbContext,
                        workload,
                        config.Azure.TenantId,
                        context.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process workload {Workload}", workload);
                }
            }

            // Cleanup old snapshots
            if (config.Scheduling.RetentionDays > 0)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-config.Scheduling.RetentionDays);
                var deletedCount = await snapshotRepository.DeleteOlderThanAsync(cutoffDate, context.CancellationToken);
                if (deletedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old snapshots", deletedCount);
                }
            }

            _logger.LogInformation("Snapshot polling job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snapshot polling job failed");
            throw new JobExecutionException(ex);
        }
    }

    private async Task ProcessWorkloadAsync(
        IUtcmClient utcmClient,
        ISnapshotRepository snapshotRepository,
        IDiffEngine diffEngine,
        GraphLedgerDbContext dbContext,
        string workload,
        string tenantId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing workload: {Workload}", workload);

        var resourceTypes = UtcmResourceTypeRegistry.GetResourceTypes(workload);
        if (resourceTypes.Count == 0)
        {
            _logger.LogWarning("No resource types found for workload {Workload}", workload);
            return;
        }

        var displayName = $"Scheduled_{workload}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

        // Create and wait for UTCM snapshot job
        _logger.LogDebug("Creating UTCM snapshot job for {Workload}", workload);
        var job = await utcmClient.CreateSnapshotAsync(displayName, resourceTypes, cancellationToken);

        _logger.LogDebug("Waiting for snapshot job {JobId}", job.Id);
        var completedJob = await utcmClient.WaitForSnapshotAsync(job.Id, TimeSpan.FromMinutes(5), cancellationToken);

        if (completedJob.Status == UtcmJobStatus.Failed)
        {
            _logger.LogError("Snapshot job failed for {Workload}: {Error}", workload, completedJob.ErrorMessage);
            return;
        }

        if (completedJob.SnapshotData == null || completedJob.SnapshotData.Count == 0)
        {
            _logger.LogWarning("No resources returned for workload {Workload}", workload);
            return;
        }

        // Process each resource in the snapshot
        foreach (var resource in completedJob.SnapshotData)
        {
            var newSnapshot = new Snapshot
            {
                TenantId = tenantId,
                Workload = workload,
                ConfigurationJson = JsonSerializer.Serialize(resource.Properties, new JsonSerializerOptions
                {
                    WriteIndented = true
                }),
                Source = SnapshotSource.Scheduled,
                UtcmJobId = completedJob.Id,
                UtcmResourceType = resource.ResourceType,
                ResourceDisplayName = resource.DisplayName
            };

            // Find previous snapshot for this specific resource type
            var allPreviousSnapshots = await snapshotRepository.GetByWorkloadAsync(workload, 100, cancellationToken);
            var previousSnapshot = allPreviousSnapshots
                .FirstOrDefault(s => s.UtcmResourceType == resource.ResourceType
                                    && s.ResourceDisplayName == resource.DisplayName);

            if (previousSnapshot != null)
            {
                var diffResult = diffEngine.Compare(previousSnapshot, newSnapshot);

                if (diffResult.HasChanges)
                {
                    _logger.LogInformation(
                        "Drift detected in {Workload}/{ResourceType}/{ResourceName}: {ChangeCount} change(s)",
                        workload,
                        resource.ResourceType,
                        resource.DisplayName,
                        diffResult.ChangeCount);

                    var driftRecord = new DriftRecord
                    {
                        SnapshotId = newSnapshot.Id,
                        BaselineSnapshotId = previousSnapshot.Id,
                        Workload = workload,
                        DiffJson = diffResult.RawDiffJson ?? "{}",
                        ChangeCount = diffResult.ChangeCount,
                        Severity = DetermineSeverity(diffResult),
                        ResourceType = resource.ResourceType
                    };

                    dbContext.DriftRecords.Add(driftRecord);
                }
                else
                {
                    _logger.LogDebug("No changes detected in {Workload}/{ResourceType}/{ResourceName}",
                        workload, resource.ResourceType, resource.DisplayName);
                }
            }

            await snapshotRepository.CreateAsync(newSnapshot, cancellationToken);
            _logger.LogDebug("Snapshot saved for {Workload}/{ResourceType}: {SnapshotId}",
                workload, resource.ResourceType, newSnapshot.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DriftSeverity DetermineSeverity(DiffResult diffResult)
    {
        if (diffResult.ChangeCount > 10)
        {
            return DriftSeverity.Critical;
        }

        if (diffResult.ChangeCount > 3)
        {
            return DriftSeverity.Warning;
        }

        return DriftSeverity.Info;
    }
}
