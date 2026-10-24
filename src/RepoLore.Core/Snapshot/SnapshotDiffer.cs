namespace RepoLore.Core.Snapshot;

public sealed class SnapshotDiff
{
    public SnapshotDiff(bool isNoOp, IReadOnlyList<string> added, IReadOnlyList<string> changed, IReadOnlyList<string> removed)
    {
        IsNoOp = isNoOp;
        Added = added;
        Changed = changed;
        Removed = removed;
    }

    public bool IsNoOp { get; }
    public IReadOnlyList<string> Added { get; }
    public IReadOnlyList<string> Changed { get; }
    public IReadOnlyList<string> Removed { get; }
}

public static class SnapshotDiffer
{
    public static SnapshotDiff Compare(CheckpointManifest? previous, CaptureScope scope, IReadOnlyList<FileEntry> currentFiles)
    {
        var current = ToMap(currentFiles);

        if (previous is null)
        {
            var initialAdded = new List<string>(current.Keys);
            initialAdded.Sort(StringComparer.Ordinal);
            return new SnapshotDiff(false, initialAdded, Array.Empty<string>(), Array.Empty<string>());
        }

        var previousMap = ToMap(previous.Files);
        var scopeChanged = !ScopeEquals(previous.Scope, scope);
        var hashesChanged = !MapsEqual(previousMap, current);

        var added = new List<string>();
        var changed = new List<string>();
        var removed = new List<string>();

        foreach (var pair in current)
            if (!previousMap.ContainsKey(pair.Key))
                added.Add(pair.Key);

        foreach (var pair in previousMap)
            if (!current.ContainsKey(pair.Key))
                removed.Add(pair.Key);

        foreach (var pair in current)
            if (previousMap.TryGetValue(pair.Key, out var previousHash) && !string.Equals(previousHash, pair.Value, StringComparison.Ordinal))
                changed.Add(pair.Key);

        added.Sort(StringComparer.Ordinal);
        changed.Sort(StringComparer.Ordinal);
        removed.Sort(StringComparer.Ordinal);

        return new SnapshotDiff(!scopeChanged && !hashesChanged, added, changed, removed);
    }

    public static bool ScopeEquals(CaptureScope a, CaptureScope b)
    {
        if (a.HistoryEnabled != b.HistoryEnabled)
            return false;
        if (a.MaxBytes != b.MaxBytes)
            return false;
        if (!string.Equals(a.RepoLoreJsonStatus, b.RepoLoreJsonStatus, StringComparison.Ordinal))
            return false;
        if (a.MigrationScope != b.MigrationScope)
            return false;
        if (a.ExcludeRules.Count != b.ExcludeRules.Count)
            return false;
        for (var i = 0; i < a.ExcludeRules.Count; i++)
            if (!string.Equals(a.ExcludeRules[i], b.ExcludeRules[i], StringComparison.Ordinal))
                return false;
        return true;
    }

    private static Dictionary<string, string> ToMap(IReadOnlyList<FileEntry> files)
    {
        var map = new Dictionary<string, string>(files.Count, StringComparer.Ordinal);
        foreach (var file in files)
            map[file.Path] = file.Hash;
        return map;
    }

    private static bool MapsEqual(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        if (a.Count != b.Count)
            return false;
        foreach (var pair in a)
            if (!b.TryGetValue(pair.Key, out var hash) || !string.Equals(hash, pair.Value, StringComparison.Ordinal))
                return false;
        return true;
    }
}
