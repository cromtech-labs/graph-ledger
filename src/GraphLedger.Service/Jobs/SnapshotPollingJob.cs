using GraphLedger.Core.Diff;
using GraphLedger.Core.Graph;
using GraphLedger.Core.Graph.Auth;
using GraphLedger.Core.Models;
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

            foreach (var workload in config.EnabledWorkloads)
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

        var newSnapshot = await utcmClient.TakeSnapshotAsync(workload, cancellationToken);
        newSnapshot.Source = SnapshotSource.Scheduled;

        var previousSnapshot = await snapshotRepository.GetLatestByWorkloadAsync(workload, cancellationToken);

        if (previousSnapshot != null)
        {
            var diffResult = diffEngine.Compare(previousSnapshot, newSnapshot);

            if (diffResult.HasChanges)
            {
                _logger.LogInformation(
                    "Drift detected in {Workload}: {ChangeCount} change(s)",
                    workload,
                    diffResult.ChangeCount);

                var driftRecord = new DriftRecord
                {
                    SnapshotId = newSnapshot.Id,
                    BaselineSnapshotId = previousSnapshot.Id,
                    Workload = workload,
                    DiffJson = diffResult.RawDiffJson ?? "{}",
                    ChangeCount = diffResult.ChangeCount,
                    Severity = DetermineSeverity(diffResult)
                };

                dbContext.DriftRecords.Add(driftRecord);
            }
            else
            {
                _logger.LogDebug("No changes detected in {Workload}", workload);
            }
        }

        await snapshotRepository.CreateAsync(newSnapshot, cancellationToken);
        _logger.LogDebug("Snapshot saved for {Workload}: {SnapshotId}", workload, newSnapshot.Id);
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
