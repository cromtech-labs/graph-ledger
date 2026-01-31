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

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<DriftRecord> DriftRecords => Set<DriftRecord>();

    public DbSet<UtcmMonitorRecord> UtcmMonitors => Set<UtcmMonitorRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Resource configuration
        modelBuilder.Entity<Resource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DisplayName).HasMaxLength(500);
            entity.Property(e => e.Workload).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(100);

            // Unique constraint: ExternalId + ResourceType identifies a resource
            entity.HasIndex(e => new { e.ExternalId, e.ResourceType }).IsUnique();
            entity.HasIndex(e => e.ResourceType);
            entity.HasIndex(e => e.Workload);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.LastChangedAt);

            // Latest snapshot reference (no cascade - just nullify if snapshot deleted)
            entity.HasOne(e => e.LatestSnapshot)
                .WithMany()
                .HasForeignKey(e => e.LatestSnapshotId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Snapshot configuration
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

            // New resource tracking fields
            entity.Property(e => e.ExternalId).HasMaxLength(200);
            entity.Property(e => e.ConfigurationHash).HasMaxLength(64); // SHA256 hex

            // Relationship to Resource
            entity.HasOne(e => e.Resource)
                .WithMany(r => r.Snapshots)
                .HasForeignKey(e => e.ResourceId)
                .OnDelete(DeleteBehavior.SetNull);

            // Self-reference for previous snapshot
            entity.HasOne(e => e.PreviousSnapshot)
                .WithMany()
                .HasForeignKey(e => e.PreviousSnapshotId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => new { e.TenantId, e.Workload, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UtcmJobId);
            entity.HasIndex(e => e.UtcmResourceType);
            entity.HasIndex(e => e.ResourceId);
            entity.HasIndex(e => e.ExternalId);
            entity.HasIndex(e => e.ConfigurationHash);
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
