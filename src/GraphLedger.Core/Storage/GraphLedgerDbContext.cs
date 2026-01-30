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
            entity.HasIndex(e => new { e.TenantId, e.Workload, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<DriftRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Workload).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DiffJson).IsRequired();
            entity.Property(e => e.Severity).HasConversion<string>();
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
        });
    }
}
