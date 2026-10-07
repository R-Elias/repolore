namespace RepoLore.Core.Snapshot;

public static class HistoryVersion
{
    public const int Current = 1;
}

public sealed class FileEntry
{
    public FileEntry(string path, string hash, long size)
    {
        Path = path;
        Hash = hash;
        Size = size;
    }

    public string Path { get; }
    public string Hash { get; }
    public long Size { get; }
}

public sealed class CaptureScope
{
    public const string RepoLoreJsonPresent = "present";
    public const string RepoLoreJsonAbsent = "absent";
    public const string RepoLoreJsonExcluded = "excluded";

    public CaptureScope(bool historyEnabled, long maxBytes, IReadOnlyList<string> excludeRules, string repoLoreJsonStatus, bool migrationScope = false)
    {
        HistoryEnabled = historyEnabled;
        MaxBytes = maxBytes;
        ExcludeRules = excludeRules;
        RepoLoreJsonStatus = repoLoreJsonStatus;
        MigrationScope = migrationScope;
    }

    public bool HistoryEnabled { get; }
    public long MaxBytes { get; }
    public IReadOnlyList<string> ExcludeRules { get; }
    public string RepoLoreJsonStatus { get; }
    public bool MigrationScope { get; }
}

public sealed class CheckpointManifest
{
    public CheckpointManifest(long id, string timestamp, int historyVersion, int knowledgeFormat, CaptureScope scope, IReadOnlyList<FileEntry> files)
    {
        Id = id;
        Timestamp = timestamp;
        HistoryVersion = historyVersion;
        KnowledgeFormat = knowledgeFormat;
        Scope = scope;
        Files = files;
    }

    public long Id { get; }
    public string Timestamp { get; }
    public int HistoryVersion { get; }
    public int KnowledgeFormat { get; }
    public CaptureScope Scope { get; }
    public IReadOnlyList<FileEntry> Files { get; }
}
