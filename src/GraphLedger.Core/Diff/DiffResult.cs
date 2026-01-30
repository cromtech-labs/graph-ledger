namespace GraphLedger.Core.Diff;

public class DiffResult
{
    public bool HasChanges => Changes.Count > 0;

    public int ChangeCount => Changes.Count;

    public List<DiffChange> Changes { get; set; } = new();

    public string? RawDiffJson { get; set; }

    public DateTime ComparedAt { get; set; } = DateTime.UtcNow;

    public Guid? LeftSnapshotId { get; set; }

    public Guid? RightSnapshotId { get; set; }
}

public class DiffChange
{
    public DiffOperation Operation { get; set; }

    public string Path { get; set; } = string.Empty;

    public object? OldValue { get; set; }

    public object? NewValue { get; set; }

    public override string ToString()
    {
        return Operation switch
        {
            DiffOperation.Add => $"+ {Path}: {NewValue}",
            DiffOperation.Remove => $"- {Path}: {OldValue}",
            DiffOperation.Replace => $"~ {Path}: {OldValue} -> {NewValue}",
            _ => $"? {Path}"
        };
    }
}

public enum DiffOperation
{
    Add,
    Remove,
    Replace
}
