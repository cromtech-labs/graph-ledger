using System.Text.Json;
using System.Text.Json.Nodes;
using GraphLedger.Core.Models;
using JsonDiffPatchDotNet;

namespace GraphLedger.Core.Diff;

public class JsonDiffEngine : IDiffEngine
{
    private readonly JsonDiffPatch _diffPatch;

    public JsonDiffEngine()
    {
        _diffPatch = new JsonDiffPatch();
    }

    public DiffResult Compare(Snapshot left, Snapshot right)
    {
        var result = Compare(left.ConfigurationJson, right.ConfigurationJson);
        result.LeftSnapshotId = left.Id;
        result.RightSnapshotId = right.Id;
        return result;
    }

    public DiffResult Compare(string leftJson, string rightJson)
    {
        var result = new DiffResult();

        try
        {
            var leftToken = Newtonsoft.Json.Linq.JToken.Parse(leftJson);
            var rightToken = Newtonsoft.Json.Linq.JToken.Parse(rightJson);

            var diff = _diffPatch.Diff(leftToken, rightToken);

            if (diff == null)
            {
                return result;
            }

            result.RawDiffJson = diff.ToString(Newtonsoft.Json.Formatting.Indented);
            result.Changes = ParseDiffChanges(diff, "");
        }
        catch (Exception ex)
        {
            result.Changes.Add(new DiffChange
            {
                Operation = DiffOperation.Replace,
                Path = "$",
                OldValue = $"Error parsing: {ex.Message}",
                NewValue = null
            });
        }

        return result;
    }

    private List<DiffChange> ParseDiffChanges(Newtonsoft.Json.Linq.JToken diff, string currentPath)
    {
        var changes = new List<DiffChange>();

        if (diff is Newtonsoft.Json.Linq.JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                var path = string.IsNullOrEmpty(currentPath)
                    ? property.Name
                    : $"{currentPath}.{property.Name}";

                if (property.Value is Newtonsoft.Json.Linq.JArray arr)
                {
                    var change = ParseArrayChange(arr, path);
                    if (change != null)
                    {
                        changes.Add(change);
                    }
                }
                else if (property.Value is Newtonsoft.Json.Linq.JObject nestedObj)
                {
                    if (nestedObj.ContainsKey("_t") && nestedObj["_t"]?.ToString() == "a")
                    {
                        changes.AddRange(ParseArrayDiff(nestedObj, path));
                    }
                    else
                    {
                        changes.AddRange(ParseDiffChanges(nestedObj, path));
                    }
                }
            }
        }

        return changes;
    }

    private DiffChange? ParseArrayChange(Newtonsoft.Json.Linq.JArray arr, string path)
    {
        if (arr.Count == 1)
        {
            return new DiffChange
            {
                Operation = DiffOperation.Add,
                Path = path,
                NewValue = arr[0]?.ToString()
            };
        }
        else if (arr.Count == 2)
        {
            return new DiffChange
            {
                Operation = DiffOperation.Replace,
                Path = path,
                OldValue = arr[0]?.ToString(),
                NewValue = arr[1]?.ToString()
            };
        }
        else if (arr.Count == 3 && arr[2]?.ToString() == "0")
        {
            return new DiffChange
            {
                Operation = DiffOperation.Remove,
                Path = path,
                OldValue = arr[0]?.ToString()
            };
        }

        return null;
    }

    private List<DiffChange> ParseArrayDiff(Newtonsoft.Json.Linq.JObject arrayDiff, string path)
    {
        var changes = new List<DiffChange>();

        foreach (var property in arrayDiff.Properties())
        {
            if (property.Name == "_t") continue;

            var indexPath = $"{path}[{property.Name.TrimStart('_')}]";

            if (property.Name.StartsWith("_"))
            {
                if (property.Value is Newtonsoft.Json.Linq.JArray arr && arr.Count == 3)
                {
                    changes.Add(new DiffChange
                    {
                        Operation = DiffOperation.Remove,
                        Path = indexPath,
                        OldValue = arr[0]?.ToString()
                    });
                }
            }
            else
            {
                if (property.Value is Newtonsoft.Json.Linq.JArray arr)
                {
                    var change = ParseArrayChange(arr, indexPath);
                    if (change != null) changes.Add(change);
                }
                else if (property.Value is Newtonsoft.Json.Linq.JObject nestedDiff)
                {
                    changes.AddRange(ParseDiffChanges(nestedDiff, indexPath));
                }
            }
        }

        return changes;
    }
}
