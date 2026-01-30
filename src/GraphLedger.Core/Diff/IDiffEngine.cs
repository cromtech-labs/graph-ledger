using GraphLedger.Core.Models;

namespace GraphLedger.Core.Diff;

public interface IDiffEngine
{
    DiffResult Compare(Snapshot left, Snapshot right);

    DiffResult Compare(string leftJson, string rightJson);
}
