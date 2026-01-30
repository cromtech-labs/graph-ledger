using GraphLedger.Core.Diff;
using GraphLedger.Core.Models;
using Xunit;

namespace GraphLedger.Core.Tests;

public class DiffEngineTests
{
    private readonly JsonDiffEngine _diffEngine = new();

    [Fact]
    public void Compare_IdenticalJson_ReturnsNoChanges()
    {
        var json = """{"name": "test", "value": 123}""";

        var result = _diffEngine.Compare(json, json);

        Assert.False(result.HasChanges);
        Assert.Equal(0, result.ChangeCount);
    }

    [Fact]
    public void Compare_DifferentJson_ReturnsChanges()
    {
        var left = """{"name": "test", "value": 123}""";
        var right = """{"name": "test", "value": 456}""";

        var result = _diffEngine.Compare(left, right);

        Assert.True(result.HasChanges);
        Assert.True(result.ChangeCount > 0);
    }

    [Fact]
    public void Compare_AddedProperty_DetectsAddition()
    {
        var left = """{"name": "test"}""";
        var right = """{"name": "test", "value": 123}""";

        var result = _diffEngine.Compare(left, right);

        Assert.True(result.HasChanges);
        Assert.Contains(result.Changes, c => c.Operation == DiffOperation.Add);
    }

    [Fact]
    public void Compare_RemovedProperty_DetectsRemoval()
    {
        var left = """{"name": "test", "value": 123}""";
        var right = """{"name": "test"}""";

        var result = _diffEngine.Compare(left, right);

        Assert.True(result.HasChanges);
        Assert.Contains(result.Changes, c => c.Operation == DiffOperation.Remove);
    }

    [Fact]
    public void Compare_Snapshots_SetsSnapshotIds()
    {
        var leftSnapshot = new Snapshot
        {
            Id = Guid.NewGuid(),
            ConfigurationJson = """{"name": "test"}"""
        };

        var rightSnapshot = new Snapshot
        {
            Id = Guid.NewGuid(),
            ConfigurationJson = """{"name": "test2"}"""
        };

        var result = _diffEngine.Compare(leftSnapshot, rightSnapshot);

        Assert.Equal(leftSnapshot.Id, result.LeftSnapshotId);
        Assert.Equal(rightSnapshot.Id, result.RightSnapshotId);
    }
}
