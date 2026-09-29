using RepoLore.Core.Configuration;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using RepoLore.Core.Snapshot;

namespace RepoLore.Core.Restore;

public sealed class RestorePlanException : Exception
{
    public RestorePlanException(string message) : base(message) { }
}

public enum RestoreAction
{
    Add,
    Replace,
    Delete,
    Unchanged
}

public sealed class RestorePlanEntry
{
    public RestorePlanEntry(string path, RestoreAction action, string? before, string? after, long afterSize, string reason)
    {
        Path = path;
        Action = action;
        Before = before;
        After = after;
        AfterSize = afterSize;
        Reason = reason;
    }

    public string Path { get; }
    public RestoreAction Action { get; }
    public string? Before { get; }
    public string? After { get; }
    public long AfterSize { get; }
    public string Reason { get; }
}

public sealed class RestorePlan
{
    public RestorePlan(IReadOnlyList<RestorePlanEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<RestorePlanEntry> Entries { get; }

    public bool IsNoOp
    {
        get
        {
            foreach (var entry in Entries)
                if (entry.Action != RestoreAction.Unchanged)
                    return false;
            return true;
        }
    }

    public int Count(RestoreAction action)
    {
        var count = 0;
        foreach (var entry in Entries)
            if (entry.Action == action)
                count++;
        return count;
    }
}

public static class RestorePlanner
{
    public static RestorePlan Plan(CheckpointManifest target, IReadOnlyList<FileEntry> currentFiles, RepoLoreConfig currentConfig, string? path)
    {
        RuleSet savedExclude;
        try
        {
            savedExclude = RuleSet.Compile(target.Scope.ExcludeRules);
        }
        catch (RuleSyntaxException ex)
        {
            throw new RestorePlanException($"target checkpoint has an invalid coverage rule (line {ex.Line}): {ex.Message}");
        }

        var saved = ToMap(target.Files);
        var current = ToMap(currentFiles);

        return path is null
            ? PlanFull(saved, current, savedExclude, currentConfig.HistoryExclude)
            : PlanSingle(path, saved, current, savedExclude, currentConfig.HistoryExclude);
    }

    private static RestorePlan PlanFull(
        Dictionary<string, FileEntry> saved,
        Dictionary<string, FileEntry> current,
        RuleSet savedExclude,
        RuleSet currentExclude)
    {
        var candidates = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in saved.Keys)
            candidates.Add(path);
        foreach (var path in current.Keys)
            candidates.Add(path);

        var entries = new List<RestorePlanEntry>(candidates.Count);
        foreach (var path in candidates)
        {
            if (!HistoryCoveragePolicy.IsCovered(path, false, false, savedExclude)
                || !HistoryCoveragePolicy.IsCovered(path, false, false, currentExclude))
                continue;

            saved.TryGetValue(path, out var savedFile);
            current.TryGetValue(path, out var currentFile);

            if (savedFile is not null && currentFile is not null)
            {
                entries.Add(string.Equals(savedFile.Hash, currentFile.Hash, StringComparison.Ordinal)
                    ? new RestorePlanEntry(path, RestoreAction.Unchanged, currentFile.Hash, savedFile.Hash, savedFile.Size, "identical in saved and current state")
                    : new RestorePlanEntry(path, RestoreAction.Replace, currentFile.Hash, savedFile.Hash, savedFile.Size, "content differs from the snapshot"));
            }
            else if (savedFile is not null)
            {
                entries.Add(new RestorePlanEntry(path, RestoreAction.Add, null, savedFile.Hash, savedFile.Size, "present in snapshot, absent now"));
            }
            else
            {
                entries.Add(new RestorePlanEntry(path, RestoreAction.Delete, currentFile!.Hash, null, 0, "absent in snapshot, present now"));
            }
        }

        return new RestorePlan(entries);
    }

    private static RestorePlan PlanSingle(
        string path,
        Dictionary<string, FileEntry> saved,
        Dictionary<string, FileEntry> current,
        RuleSet savedExclude,
        RuleSet currentExclude)
    {
        if (!HistoryCoveragePolicy.IsCovered(path, false, false, savedExclude))
            throw new RestorePlanException($"'{path}' is outside the target checkpoint's saved coverage; nothing can be restored for it");
        if (!HistoryCoveragePolicy.IsCovered(path, false, false, currentExclude))
            throw new RestorePlanException($"'{path}' is excluded by the current history coverage and is outside ordinary restore scope");

        saved.TryGetValue(path, out var savedFile);
        current.TryGetValue(path, out var currentFile);

        var savedHash = savedFile?.Hash;
        var currentHash = currentFile?.Hash;

        RestorePlanEntry entry;
        if (savedHash is not null && currentHash is not null)
            entry = string.Equals(savedHash, currentHash, StringComparison.Ordinal)
                ? new RestorePlanEntry(path, RestoreAction.Unchanged, currentHash, savedHash, savedFile!.Size, "identical in saved and current state")
                : new RestorePlanEntry(path, RestoreAction.Replace, currentHash, savedHash, savedFile!.Size, "content differs from the snapshot");
        else if (savedHash is not null)
            entry = new RestorePlanEntry(path, RestoreAction.Add, null, savedHash, savedFile!.Size, "present in snapshot, absent now");
        else if (currentHash is not null)
            entry = new RestorePlanEntry(path, RestoreAction.Delete, currentHash, null, 0, "absent in snapshot, present now");
        else
            entry = new RestorePlanEntry(path, RestoreAction.Unchanged, null, null, 0, "absent in both saved and current state");

        return new RestorePlan(new[] { entry });
    }

    private static Dictionary<string, FileEntry> ToMap(IReadOnlyList<FileEntry> files)
    {
        var map = new Dictionary<string, FileEntry>(files.Count, StringComparer.Ordinal);
        foreach (var file in files)
            map[file.Path] = file;
        return map;
    }
}
