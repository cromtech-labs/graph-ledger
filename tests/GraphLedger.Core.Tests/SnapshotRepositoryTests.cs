using GraphLedger.Core.Models;
using GraphLedger.Core.Services;
using GraphLedger.Core.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GraphLedger.Core.Tests;

public class SnapshotRepositoryTests : IDisposable
{
    private readonly GraphLedgerDbContext _context;
    private readonly SnapshotRepository _repository;

    public SnapshotRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<GraphLedgerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GraphLedgerDbContext(options);
        var hashService = new ConfigurationHashService();
        _repository = new SnapshotRepository(_context, hashService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CreateAsync_AddsSnapshotToDatabase()
    {
        var snapshot = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = """{"test": true}"""
        };

        var result = await _repository.CreateAsync(snapshot);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(1, await _context.Snapshots.CountAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectSnapshot()
    {
        var snapshot = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = """{"test": true}"""
        };
        await _repository.CreateAsync(snapshot);

        var result = await _repository.GetByIdAsync(snapshot.Id);

        Assert.NotNull(result);
        Assert.Equal(snapshot.Id, result.Id);
        Assert.Equal(snapshot.TenantId, result.TenantId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForNonexistent()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSnapshotsOrderedByCreatedAt()
    {
        var snapshot1 = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "workload1",
            ConfigurationJson = "{}",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        var snapshot2 = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "workload2",
            ConfigurationJson = "{}",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        var snapshot3 = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "workload3",
            ConfigurationJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(snapshot1);
        await _repository.CreateAsync(snapshot2);
        await _repository.CreateAsync(snapshot3);

        var result = await _repository.GetAllAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(snapshot3.Id, result[0].Id);
        Assert.Equal(snapshot2.Id, result[1].Id);
        Assert.Equal(snapshot1.Id, result[2].Id);
    }

    [Fact]
    public async Task GetByWorkloadAsync_FiltersCorrectly()
    {
        var snapshot1 = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = "{}"
        };
        var snapshot2 = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "conditionalAccess",
            ConfigurationJson = "{}"
        };

        await _repository.CreateAsync(snapshot1);
        await _repository.CreateAsync(snapshot2);

        var result = await _repository.GetByWorkloadAsync("deviceManagement");

        Assert.Single(result);
        Assert.Equal("deviceManagement", result[0].Workload);
    }

    [Fact]
    public async Task GetLatestByWorkloadAsync_ReturnsNewest()
    {
        var olderSnapshot = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = "{}",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        var newerSnapshot = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(olderSnapshot);
        await _repository.CreateAsync(newerSnapshot);

        var result = await _repository.GetLatestByWorkloadAsync("deviceManagement");

        Assert.NotNull(result);
        Assert.Equal(newerSnapshot.Id, result.Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSnapshot()
    {
        var snapshot = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = "{}"
        };
        await _repository.CreateAsync(snapshot);

        var deleted = await _repository.DeleteAsync(snapshot.Id);

        Assert.True(deleted);
        Assert.Equal(0, await _context.Snapshots.CountAsync());
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldSnapshots()
    {
        var oldSnapshot = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = "{}",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };
        var newSnapshot = new Snapshot
        {
            TenantId = "test-tenant",
            Workload = "deviceManagement",
            ConfigurationJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(oldSnapshot);
        await _repository.CreateAsync(newSnapshot);

        var deletedCount = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-5));

        Assert.Equal(1, deletedCount);
        Assert.Equal(1, await _context.Snapshots.CountAsync());
    }
}
