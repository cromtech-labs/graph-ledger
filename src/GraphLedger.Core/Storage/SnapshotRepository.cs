using GraphLedger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GraphLedger.Core.Storage;

public class SnapshotRepository : ISnapshotRepository
{
    private readonly GraphLedgerDbContext _context;

    public SnapshotRepository(GraphLedgerDbContext context)
    {
        _context = context;
    }

    public async Task<Snapshot> CreateAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        _context.Snapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public async Task<Snapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> GetAllAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> GetByWorkloadAsync(string workload, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.Workload == workload)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Snapshot>> GetByTenantAsync(string tenantId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Snapshot?> GetLatestByWorkloadAsync(string workload, CancellationToken cancellationToken = default)
    {
        return await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.Workload == workload)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var snapshot = await _context.Snapshots.FindAsync(new object[] { id }, cancellationToken);
        if (snapshot == null)
        {
            return false;
        }

        _context.Snapshots.Remove(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        var oldSnapshots = await _context.Snapshots
            .Where(s => s.CreatedAt < cutoffDate)
            .ToListAsync(cancellationToken);

        _context.Snapshots.RemoveRange(oldSnapshots);
        await _context.SaveChangesAsync(cancellationToken);
        return oldSnapshots.Count;
    }
}
