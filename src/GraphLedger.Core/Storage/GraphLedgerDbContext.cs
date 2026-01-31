using GraphLedger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Core.Storage;

public class GraphLedgerDbContext : DbContext
{
    public GraphLedgerDbContext(DbContextOptions<GraphLedgerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Snapshot> Snapshots => Set<Snapshot>();

    public DbSet<DriftRecord> DriftRecords => Set<DriftRecord>();

    public DbSet<UtcmMonitorRecord> UtcmMonitors => Set<UtcmMonitorRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Snapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Workload).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ConfigurationJson).IsRequired();
            entity.Property(e => e.Source).HasConversion<string>();
            entity.Property(e => e.UtcmJobId).HasMaxLength(200);
            entity.Property(e => e.UtcmResourceType).HasMaxLength(200);
            entity.Property(e => e.ResourceDisplayName).HasMaxLength(500);
            entity.HasIndex(e => new { e.TenantId, e.Workload, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UtcmJobId);
            entity.HasIndex(e => e.UtcmResourceType);
        });

        modelBuilder.Entity<DriftRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Workload).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DiffJson).IsRequired();
            entity.Property(e => e.Severity).HasConversion<string>();
            entity.Property(e => e.UtcmDriftId).HasMaxLength(200);
            entity.Property(e => e.UtcmMonitorId).HasMaxLength(200);
            entity.Property(e => e.ResourceType).HasMaxLength(200);
            entity.HasOne(e => e.Snapshot)
                .WithMany()
                .HasForeignKey(e => e.SnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.BaselineSnapshot)
                .WithMany()
                .HasForeignKey(e => e.BaselineSnapshotId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.DetectedAt);
            entity.HasIndex(e => e.IsAcknowledged);
            entity.HasIndex(e => e.UtcmDriftId);
            entity.HasIndex(e => e.UtcmMonitorId);
        });

        modelBuilder.Entity<UtcmMonitorRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UtcmMonitorId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ResourceTypesJson).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.UtcmMonitorId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastSyncedAt);
        });
    }
}
