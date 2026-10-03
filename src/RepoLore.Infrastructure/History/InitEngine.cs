using RepoLore.Core.Configuration;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;

namespace RepoLore.Infrastructure.History;

public enum InitDirKind
{
    Absent,
    Empty,
    Alpha,
    V1
}

public sealed class InitException : Exception
{
    public InitException(string message, long? recoveryId = null, Exception? inner = null) : base(message, inner)
    {
        RecoveryId = recoveryId;
    }

    public long? RecoveryId { get; }
}

public sealed class InitIncompleteException : Exception
{
    public InitIncompleteException(IReadOnlyList<string> created, Exception inner)
        : base("initialization is incomplete: " + inner.Message, inner)
    {
        Created = created;
    }

    public IReadOnlyList<string> Created { get; }
}

public sealed class InitResult
{
    public InitResult(IReadOnlyList<string> created, bool historyDisabled, long? publishedId, bool checkpointNoOp, bool methodUpdated)
    {
        Created = created;
        HistoryDisabled = historyDisabled;
        PublishedId = publishedId;
        CheckpointNoOp = checkpointNoOp;
        MethodUpdated = methodUpdated;
    }

    public IReadOnlyList<string> Created { get; }
    public bool HistoryDisabled { get; }
    public long? PublishedId { get; }
    public bool CheckpointNoOp { get; }
    public bool MethodUpdated { get; }

    public bool WasNoOp => Created.Count == 0 && CheckpointNoOp && !MethodUpdated;
}

public sealed class InitEngine
{
    private const string MethodPath = "_repolore/method.md";
    private const string RootPath = "_repolore/root.md";
    private const string SparseTreePath = "_repolore/sparse-tree/";

    private readonly string _repositoryRoot;
    private readonly string _historyDirectory;
    private readonly RepositoryPathResolver _resolver;
    private readonly ObjectStore _objects;
    private readonly CheckpointStore _checkpoints;
    private readonly CheckpointEngine _engine;
    private readonly PendingStore _pending;

    public InitEngine(string repositoryRoot, string historyDirectory, Clock? clock = null, Func<string, bool>? deleteFile = null)
    {
        _repositoryRoot = repositoryRoot;
        _historyDirectory = historyDirectory;
        _resolver = new RepositoryPathResolver(repositoryRoot);
        _objects = new ObjectStore(historyDirectory);
        _checkpoints = new CheckpointStore(historyDirectory);
        _engine = new CheckpointEngine(repositoryRoot, historyDirectory, clock, deleteFile);
        _pending = new PendingStore(historyDirectory);
    }

    public InitDirKind Classify()
    {
        var dir = Path.Combine(_repositoryRoot, "_repolore");
        if (!Directory.Exists(dir))
            return InitDirKind.Absent;

        var marker = Path.Combine(dir, "repolore.json");
        if (File.Exists(marker))
        {
            ConfigParser.Parse(File.ReadAllText(marker));
            return InitDirKind.V1;
        }

        return HasAuthoredContent(dir) ? InitDirKind.Alpha : InitDirKind.Empty;
    }

    public InitResult Run(
        string methodTemplate,
        string rootTemplate,
        bool updateMethod,
        Action? beforeMarkerPublish = null,
        Action? beforeFirstCapture = null,
        Action? beforeFirstReplacement = null,
        Action? beforePostCheckpoint = null)
    {
        var kind = Classify();
        if (kind == InitDirKind.Alpha)
            throw new InitException("_repolore/ holds alpha knowledge without a format marker; migration is required (repolore migrate), not init");

        var fresh = kind != InitDirKind.V1;
        var created = new List<string>();

        if (fresh)
        {
            beforeMarkerPublish?.Invoke();
            PublishMarker();
            created.Add("_repolore/repolore.json");
        }

        var config = HistoryConfigLoader.Load(_repositoryRoot);

        EnsureMissing(MethodPath, methodTemplate, created);
        EnsureMissing(RootPath, rootTemplate, created);
        EnsureSparseTree(created);

        if (updateMethod && !fresh)
            return UpdateMethod(config, methodTemplate, created, beforeFirstReplacement, beforePostCheckpoint);

        if (!config.HistoryEnabled)
            return new InitResult(created, true, null, false, false);

        CheckpointResult capture;
        try
        {
            capture = _engine.Capture(config, beforeFirstCapture);
        }
        catch (Exception ex)
        {
            throw new InitIncompleteException(created, ex);
        }

        return new InitResult(created, false, capture.PublishedId, capture.WasNoOp, false);
    }

    private InitResult UpdateMethod(
        RepoLoreConfig config,
        string methodTemplate,
        IReadOnlyList<string> created,
        Action? beforeFirstReplacement,
        Action? beforePostCheckpoint)
    {
        if (!config.HistoryEnabled)
            throw new InitException("history is disabled in _repolore/repolore.json; the method update needs an enabled pre-operation checkpoint");

        if (_pending.Read() is not null)
            throw new InitException("a mutation is already pending; recover it before updating the method");

        var before = CurrentHash(MethodPath);
        var after = HashOfTemplate(methodTemplate);

        if (string.Equals(before, after, StringComparison.Ordinal))
            return new InitResult(created, false, null, true, false);

        _engine.Capture(config);
        var preOpId = _checkpoints.ReadHighestCompleted()?.Id
            ?? throw new InitException("no checkpoint exists after the pre-operation capture");

        _pending.Write(new PendingTransaction(
            HistoryVersion.Current,
            preOpId,
            preOpId,
            config.FormatVersion,
            config.HistoryEnabled,
            config.HistoryMaxBytes,
            config.HistoryExcludeRules,
            new[] { new PendingPlanEntry(MethodPath, before, after) }));

        try
        {
            beforeFirstReplacement?.Invoke();
            WriteMethod(methodTemplate);
            beforePostCheckpoint?.Invoke();
            _engine.Capture(config, protectedIds: new HashSet<long> { preOpId });
            _pending.Clear();
        }
        catch (InitException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InitException("method update was interrupted; recover with 'repolore restore " + preOpId + "'", preOpId, ex);
        }

        return new InitResult(created, false, null, false, true);
    }

    private static bool HasAuthoredContent(string dir)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
        {
            if (string.Equals(Path.GetFileName(entry), ".history", StringComparison.Ordinal))
                continue;
            return true;
        }

        return false;
    }

    private void PublishMarker()
    {
        Directory.CreateDirectory(Path.Combine(_repositoryRoot, "_repolore"));
        var tmpDir = Path.Combine(_historyDirectory, "tmp");
        Directory.CreateDirectory(tmpDir);
        var marker = Path.Combine(_repositoryRoot, "_repolore", "repolore.json");
        var tmp = Path.Combine(tmpDir, "repolore.json-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(tmp, "{\"formatVersion\":1}");
        File.Move(tmp, marker, overwrite: false);
    }

    private void EnsureMissing(string relativePath, string content, List<string> created)
    {
        var full = _resolver.ResolveForWrite(relativePath);
        if (File.Exists(full))
            return;

        var parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        File.WriteAllText(full, content);
        created.Add(relativePath);
    }

    private void EnsureSparseTree(List<string> created)
    {
        var dir = Path.Combine(_repositoryRoot, "_repolore", "sparse-tree");
        if (Directory.Exists(dir))
            return;

        Directory.CreateDirectory(dir);
        created.Add(SparseTreePath);
    }

    private string? CurrentHash(string relativePath)
    {
        var full = _resolver.Resolve(relativePath);
        if (!File.Exists(full))
            return null;
        return ObjectStore.Hash(File.ReadAllBytes(full));
    }

    private string HashOfTemplate(string template)
    {
        var tmpDir = Path.Combine(_historyDirectory, "tmp");
        Directory.CreateDirectory(tmpDir);
        var tmp = Path.Combine(tmpDir, Guid.NewGuid().ToString("N"));
        File.WriteAllText(tmp, template);
        var bytes = File.ReadAllBytes(tmp);
        File.Delete(tmp);
        return ObjectStore.Hash(bytes);
    }

    private void WriteMethod(string methodTemplate)
    {
        var full = _resolver.ResolveForWrite(MethodPath);
        var parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var tmpDir = Path.Combine(_historyDirectory, "tmp");
        Directory.CreateDirectory(tmpDir);
        var tmp = Path.Combine(tmpDir, Guid.NewGuid().ToString("N"));
        File.WriteAllText(tmp, methodTemplate);
        File.Move(tmp, full, overwrite: true);
    }
}
