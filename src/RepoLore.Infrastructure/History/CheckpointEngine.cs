using System.Globalization;
using RepoLore.Core.Configuration;
using RepoLore.Core.Format;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using RepoLore.Core.Snapshot;

namespace RepoLore.Infrastructure.History;

public sealed class Clock
{
    private readonly Func<DateTimeOffset> _now;

    public Clock() : this(static () => DateTimeOffset.UtcNow) { }

    public Clock(Func<DateTimeOffset> now) { _now = now; }

    public string NowIso() => _now().ToString("o", CultureInfo.InvariantCulture);
}

public sealed class CheckpointResult
{
    public CheckpointResult(bool wasNoOp, long? publishedId, IReadOnlyList<string> added, IReadOnlyList<string> changed, IReadOnlyList<string> removed, int capturedFileCount, CleanupResult cleanup)
    {
        WasNoOp = wasNoOp;
        PublishedId = publishedId;
        Added = added;
        Changed = changed;
        Removed = removed;
        CapturedFileCount = capturedFileCount;
        Cleanup = cleanup;
    }

    public bool WasNoOp { get; }
    public long? PublishedId { get; }
    public IReadOnlyList<string> Added { get; }
    public IReadOnlyList<string> Changed { get; }
    public IReadOnlyList<string> Removed { get; }
    public int CapturedFileCount { get; }
    public CleanupResult Cleanup { get; }
}

public sealed class CheckpointEngine
{
    private readonly string _repositoryRoot;
    private readonly RepositoryPathResolver _resolver;
    private readonly ObjectStore _objects;
    private readonly CheckpointStore _checkpoints;
    private readonly HistoryCleanup _cleanup;
    private readonly Clock _clock;

    public CheckpointEngine(string repositoryRoot, string historyDirectory, Clock? clock = null, Func<string, bool>? deleteFile = null)
    {
        _repositoryRoot = repositoryRoot;
        _resolver = new RepositoryPathResolver(repositoryRoot);
        _objects = new ObjectStore(historyDirectory);
        _checkpoints = new CheckpointStore(historyDirectory);
        _cleanup = new HistoryCleanup(_objects, _checkpoints, deleteFile);
        _clock = clock ?? new Clock();
    }

    public CheckpointResult Capture(RepoLoreConfig config, Action? beforeVerification = null, Action? beforeManifestPublish = null, IReadOnlySet<long>? protectedIds = null)
    {
        var historyExclude = config.HistoryExclude;
        _objects.EnsureDirectories();
        _checkpoints.EnsureDirectory();

        var first = Collect(historyExclude, storeObjects: true);

        beforeVerification?.Invoke();

        var second = Collect(historyExclude, storeObjects: false);
        if (!SameInventory(first, second))
            throw new HistoryStoreException("files changed while capturing; retry after pausing edits");

        var scope = BuildScope(config, historyExclude);
        var previous = _checkpoints.ReadHighestCompleted();
        var diff = SnapshotDiffer.Compare(previous, scope, first);

        if (diff.IsNoOp)
            return new CheckpointResult(true, null, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), first.Count, _cleanup.Clean(config.HistoryMaxBytes, protectedIds));

        var nextId = (previous is null ? 0 : previous.Id) + 1;
        var manifest = new CheckpointManifest(nextId, _clock.NowIso(), HistoryVersion.Current, KnowledgeFormat.Current, scope, first);

        var snapshotBytes = HistoryCleanup.SnapshotBytes(manifest);
        if (snapshotBytes > config.HistoryMaxBytes)
            throw new HistoryStoreException(
                $"this checkpoint needs {snapshotBytes} bytes, which exceeds history.maxBytes ({config.HistoryMaxBytes}); raise history.maxBytes or reduce coverage");

        beforeManifestPublish?.Invoke();

        _checkpoints.Publish(manifest);

        var cleanup = _cleanup.Clean(config.HistoryMaxBytes, protectedIds);

        return new CheckpointResult(false, nextId, diff.Added, diff.Changed, diff.Removed, first.Count, cleanup);
    }

    private List<FileEntry> Collect(RuleSet historyExclude, bool storeObjects)
    {
        var paths = HistoryEnumerator.EnumerateEligible(_repositoryRoot, historyExclude);
        var files = new List<FileEntry>(paths.Count);
        foreach (var relative in paths)
        {
            var full = _resolver.Resolve(relative);
            var bytes = File.ReadAllBytes(full);
            var hash = storeObjects ? _objects.Store(bytes) : ObjectStore.Hash(bytes);
            files.Add(new FileEntry(relative, hash, bytes.Length));
        }
        return files;
    }

    private CaptureScope BuildScope(RepoLoreConfig config, RuleSet historyExclude)
    {
        return new CaptureScope(config.HistoryEnabled, config.HistoryMaxBytes, config.HistoryExcludeRules, RepoLoreJsonStatus(historyExclude));
    }

    private string RepoLoreJsonStatus(RuleSet historyExclude)
    {
        var covered = HistoryCoveragePolicy.IsCovered("_repolore/repolore.json", false, false, historyExclude);
        if (!covered)
            return CaptureScope.RepoLoreJsonExcluded;
        return File.Exists(Path.Combine(_repositoryRoot, "_repolore", "repolore.json"))
            ? CaptureScope.RepoLoreJsonPresent
            : CaptureScope.RepoLoreJsonAbsent;
    }

    private static bool SameInventory(List<FileEntry> a, List<FileEntry> b)
    {
        if (a.Count != b.Count)
            return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].Path, b[i].Path, StringComparison.Ordinal))
                return false;
            if (!string.Equals(a[i].Hash, b[i].Hash, StringComparison.Ordinal))
                return false;
            if (a[i].Size != b[i].Size)
                return false;
        }
        return true;
    }
}
