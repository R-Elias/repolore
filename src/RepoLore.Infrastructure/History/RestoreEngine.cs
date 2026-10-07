using RepoLore.Core.Configuration;
using RepoLore.Core.Format;
using RepoLore.Core.Json;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;

namespace RepoLore.Infrastructure.History;

public sealed class RestoreException : Exception
{
    public RestoreException(long recoveryId, string message, Exception? inner = null) : base(message, inner)
    {
        RecoveryId = recoveryId;
    }

    public long RecoveryId { get; }
}

public sealed class RestoreResult
{
    public RestoreResult(bool wasNoOp, bool wasRecovery, long? publishedId, long recoveryId, int added, int replaced, int deleted, int recovered, CleanupResult cleanup)
    {
        WasNoOp = wasNoOp;
        WasRecovery = wasRecovery;
        PublishedId = publishedId;
        RecoveryId = recoveryId;
        Added = added;
        Replaced = replaced;
        Deleted = deleted;
        Recovered = recovered;
        Cleanup = cleanup;
    }

    public bool WasNoOp { get; }
    public bool WasRecovery { get; }
    public long? PublishedId { get; }
    public long RecoveryId { get; }
    public int Added { get; }
    public int Replaced { get; }
    public int Deleted { get; }
    public int Recovered { get; }
    public CleanupResult Cleanup { get; }
}

public sealed class PendingStore
{
    private readonly string _historyDirectory;

    public PendingStore(string historyDirectory)
    {
        _historyDirectory = historyDirectory;
    }

    public string PendingPath => Path.Combine(_historyDirectory, "pending.json");

    public PendingTransaction? Read()
    {
        var path = PendingPath;
        if (!File.Exists(path))
            return null;

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new HistoryStoreException($"cannot read the pending recovery record: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HistoryStoreException($"cannot read the pending recovery record: {ex.Message}");
        }

        JsonValue value;
        try
        {
            value = JsonParser.Parse(text);
        }
        catch (JsonParseException ex)
        {
            throw new HistoryStoreException($"pending recovery record is corrupt: {ex.Message}");
        }

        try
        {
            return PendingCodec.Decode(value);
        }
        catch (PendingFormatException ex)
        {
            throw new HistoryStoreException($"pending recovery record is corrupt: {ex.Message}");
        }
    }

    public void Write(PendingTransaction pending)
    {
        Directory.CreateDirectory(_historyDirectory);
        var text = JsonWriter.Write(PendingCodec.Encode(pending));
        var tmp = Path.Combine(_historyDirectory, "pending.json.tmp-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(tmp, text);
        File.Move(tmp, PendingPath, overwrite: true);
    }

    public void Clear()
    {
        if (File.Exists(PendingPath))
            File.Delete(PendingPath);
    }
}

public sealed class RestoreEngine
{
    private readonly string _repositoryRoot;
    private readonly string _historyDirectory;
    private readonly RepositoryPathResolver _resolver;
    private readonly ObjectStore _objects;
    private readonly CheckpointStore _checkpoints;
    private readonly CheckpointEngine _engine;
    private readonly PendingStore _pending;
    private readonly Clock _clock;

    public RestoreEngine(string repositoryRoot, string historyDirectory, Clock? clock = null, Func<string, bool>? deleteFile = null)
    {
        _repositoryRoot = repositoryRoot;
        _historyDirectory = historyDirectory;
        _resolver = new RepositoryPathResolver(repositoryRoot);
        _objects = new ObjectStore(historyDirectory);
        _checkpoints = new CheckpointStore(historyDirectory);
        _engine = new CheckpointEngine(repositoryRoot, historyDirectory, clock, deleteFile);
        _pending = new PendingStore(historyDirectory);
        _clock = clock ?? new Clock();
    }

    public RestorePlan Plan(RepoLoreConfig config, long targetId, string? path = null)
    {
        var target = LoadTarget(targetId);
        ValidatePath(path);
        var plan = RestorePlanner.Plan(target, CollectCurrent(config.HistoryExclude, target.Scope.MigrationScope), config, path, target.Scope.MigrationScope);
        ValidateTargetObjects(plan);
        return plan;
    }

    public RestoreResult Apply(
        RepoLoreConfig config,
        long targetId,
        string? path = null,
        Action? beforeFirstReplacement = null,
        Action? afterFirstReplacement = null,
        Action? beforePostCheckpoint = null)
    {
        if (!config.HistoryEnabled)
            throw new HistoryStoreException("history is disabled in _repolore/repolore.json; restore needs an enabled pre-operation checkpoint");

        if (_pending.Read() is not null)
            throw new HistoryStoreException("a restore is already pending; recover it before starting another mutation");

        var target = LoadTarget(targetId);
        ValidatePath(path);
        var plan = RestorePlanner.Plan(target, CollectCurrent(config.HistoryExclude, target.Scope.MigrationScope), config, path, target.Scope.MigrationScope);
        ValidateTargetObjects(plan);

        if (plan.IsNoOp)
            return new RestoreResult(true, false, null, targetId, 0, 0, 0, 0, EmptyCleanup);

        var preOp = _engine.Capture(config, protectedIds: new HashSet<long> { targetId });
        var preOpId = _checkpoints.ReadHighestCompleted()?.Id
            ?? throw new HistoryStoreException("no checkpoint exists after the pre-operation capture");
        var preOpManifest = LoadTarget(preOpId);

        var revalidated = RestorePlanner.Plan(target, CollectCurrent(config.HistoryExclude, target.Scope.MigrationScope), config, path, target.Scope.MigrationScope);
        ValidateTargetObjects(revalidated);
        if (revalidated.IsNoOp)
            return new RestoreResult(true, false, null, targetId, 0, 0, 0, 0, EmptyCleanup);

        var resulting = BuildResultingManifest(preOpManifest, revalidated);
        var protectedBytes = HistoryCleanup.ProtectedBytes(new[] { preOpManifest, resulting, target });
        if (protectedBytes > config.HistoryMaxBytes)
            throw new HistoryStoreException(
                $"restore needs {protectedBytes} protected bytes (pre-operation plus resulting checkpoints and the target), which exceeds history.maxBytes ({config.HistoryMaxBytes}); raise history.maxBytes or reduce coverage");

        _pending.Write(new PendingTransaction(
            HistoryVersion.Current,
            targetId,
            preOpId,
            config.FormatVersion,
            config.HistoryEnabled,
            config.HistoryMaxBytes,
            config.HistoryExcludeRules,
            AffectedEntries(revalidated)));

        try
        {
            ApplyMutations(revalidated, beforeFirstReplacement, afterFirstReplacement);
            beforePostCheckpoint?.Invoke();
            var post = _engine.Capture(config, protectedIds: new HashSet<long> { targetId, preOpId });
            _pending.Clear();
            return new RestoreResult(false, false, post.PublishedId, preOpId,
                revalidated.Count(RestoreAction.Add), revalidated.Count(RestoreAction.Replace), revalidated.Count(RestoreAction.Delete),
                0, post.Cleanup);
        }
        catch (RestoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RestoreException(preOpId, "restore was interrupted before it could finish: " + ex.Message, ex);
        }
    }

    public RestoreResult Recover(long preOperationId, Action? beforeFirstReplacement = null)
    {
        var pending = _pending.Read()
            ?? throw new HistoryStoreException("no pending recovery record exists");

        if (pending.HistoryVersion != HistoryVersion.Current)
            throw new HistoryStoreException($"pending recovery record uses history version {pending.HistoryVersion}; this build supports {HistoryVersion.Current}");
        if (pending.PreOperationId != preOperationId)
            throw new HistoryStoreException($"the pending recovery is for pre-operation id {pending.PreOperationId}, not {preOperationId}");

        RuleSet frozenExclude;
        try
        {
            frozenExclude = RuleSet.Compile(pending.ExcludeRules);
        }
        catch (RuleSyntaxException ex)
        {
            throw new HistoryStoreException($"pending recovery record has an invalid coverage rule (line {ex.Line}): {ex.Message}");
        }

        var frozenConfig = new RepoLoreConfig(
            pending.FormatVersion,
            pending.HistoryEnabled,
            pending.MaxBytes,
            frozenExclude,
            pending.ExcludeRules,
            new JsonObject());

        ValidateRecovery(pending.Plan);

        beforeFirstReplacement?.Invoke();
        var recovered = 0;
        foreach (var entry in pending.Plan)
        {
            if (string.Equals(CurrentHash(entry.Path), entry.Before, StringComparison.Ordinal))
                continue;
            ApplyRecoveryEntry(entry);
            recovered++;
        }

        var captured = _engine.Capture(frozenConfig, protectedIds: new HashSet<long> { pending.PreOperationId, pending.TargetId });

        _pending.Clear();

        return new RestoreResult(false, true, captured.PublishedId ?? pending.PreOperationId, preOperationId, 0, 0, 0, recovered, captured.Cleanup);
    }

    private static readonly CleanupResult EmptyCleanup = new(0, 0, 0, true);

    private CheckpointManifest LoadTarget(long targetId)
    {
        foreach (var manifest in _checkpoints.ListCompleted())
            if (manifest.Id == targetId)
                return manifest;
        throw new HistoryStoreException($"unknown checkpoint id {targetId}; run 'history' to list available checkpoints");
    }

    private List<FileEntry> CollectCurrent(RuleSet historyExclude, bool migrationScope)
    {
        var paths = migrationScope
            ? MigrationEnumerator.EnumerateScope(_repositoryRoot)
            : HistoryEnumerator.EnumerateEligible(_repositoryRoot, historyExclude);
        var files = new List<FileEntry>(paths.Count);
        foreach (var relative in paths)
        {
            var bytes = File.ReadAllBytes(_resolver.Resolve(relative));
            files.Add(new FileEntry(relative, ObjectStore.Hash(bytes), bytes.Length));
        }
        return files;
    }

    private void ValidateTargetObjects(RestorePlan plan)
    {
        foreach (var entry in plan.Entries)
        {
            if (entry.Action is not (RestoreAction.Add or RestoreAction.Replace))
                continue;
            if (entry.After is null)
                continue;
            if (!_objects.Validate(entry.After))
                throw new HistoryStoreException($"corrupt or missing object {entry.After} needed to restore '{entry.Path}'");
        }
    }

    private void ValidatePath(string? path)
    {
        if (path is null)
            return;

        string full;
        try
        {
            full = _resolver.Resolve(path);
        }
        catch (PathResolutionException ex)
        {
            throw new RestorePlanException(ex.Message);
        }

        if (Directory.Exists(full))
            throw new RestorePlanException($"--path '{path}' is a directory; restore selects one eligible file");
        if (RepositoryPathResolver.IsReparsePoint(full))
            throw new RestorePlanException($"--path '{path}' is a symlink and is not eligible for restore");
    }

    private CheckpointManifest BuildResultingManifest(CheckpointManifest preOp, RestorePlan plan)
    {
        var map = new Dictionary<string, FileEntry>(preOp.Files.Count, StringComparer.Ordinal);
        foreach (var file in preOp.Files)
            map[file.Path] = file;

        foreach (var entry in plan.Entries)
        {
            switch (entry.Action)
            {
                case RestoreAction.Add:
                case RestoreAction.Replace:
                    map[entry.Path] = new FileEntry(entry.Path, entry.After!, entry.AfterSize);
                    break;
                case RestoreAction.Delete:
                    map.Remove(entry.Path);
                    break;
            }
        }

        var files = map.Values.ToList();
        files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        return new CheckpointManifest(preOp.Id + 1, _clock.NowIso(), HistoryVersion.Current, KnowledgeFormat.Current, preOp.Scope, files);
    }

    private static List<PendingPlanEntry> AffectedEntries(RestorePlan plan)
    {
        var entries = new List<PendingPlanEntry>();
        foreach (var entry in plan.Entries)
            if (entry.Action != RestoreAction.Unchanged)
                entries.Add(new PendingPlanEntry(entry.Path, entry.Before, entry.After));
        return entries;
    }

    private void ApplyMutations(RestorePlan plan, Action? beforeFirstReplacement, Action? afterFirstReplacement)
    {
        var affected = new List<RestorePlanEntry>();
        foreach (var entry in plan.Entries)
            if (entry.Action != RestoreAction.Unchanged)
                affected.Add(entry);

        if (affected.Count == 0)
            return;

        beforeFirstReplacement?.Invoke();

        var applied = 0;
        foreach (var entry in affected)
        {
            RecheckBefore(entry);
            ApplyEntry(entry);
            applied++;
            if (applied == 1)
                afterFirstReplacement?.Invoke();
        }
    }

    private void RecheckBefore(RestorePlanEntry entry)
    {
        if (!string.Equals(CurrentHash(entry.Path), entry.Before, StringComparison.Ordinal))
            throw new HistoryStoreException($"file changed during restore: '{entry.Path}' no longer matches its pre-operation state");
    }

    private void ApplyEntry(RestorePlanEntry entry)
    {
        if (entry.Action == RestoreAction.Delete)
        {
            RemoveFile(entry.Path);
            return;
        }
        WriteFile(entry.Path, _objects.Read(entry.After!));
    }

    private void ValidateRecovery(IReadOnlyList<PendingPlanEntry> plan)
    {
        foreach (var entry in plan)
        {
            var current = CurrentHash(entry.Path);
            if (string.Equals(current, entry.Before, StringComparison.Ordinal))
                continue;
            if (string.Equals(current, entry.After, StringComparison.Ordinal))
                continue;
            throw new HistoryStoreException(
                $"cannot recover '{entry.Path}': it has been edited since the interrupted restore (expected the pre-operation or post-operation state)");
        }
    }

    private void ApplyRecoveryEntry(PendingPlanEntry entry)
    {
        if (entry.Before is null)
        {
            RemoveFile(entry.Path);
            return;
        }
        WriteFile(entry.Path, _objects.Read(entry.Before));
    }

    private string? CurrentHash(string relativePath)
    {
        var full = _resolver.Resolve(relativePath);
        if (!File.Exists(full))
            return null;
        return ObjectStore.Hash(File.ReadAllBytes(full));
    }

    private void WriteFile(string relativePath, byte[] bytes)
    {
        var full = _resolver.ResolveForWrite(relativePath);
        var parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var tmpDir = Path.Combine(_historyDirectory, "tmp");
        Directory.CreateDirectory(tmpDir);
        var tmp = Path.Combine(tmpDir, Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, full, overwrite: true);
    }

    private void RemoveFile(string relativePath)
    {
        var full = _resolver.ResolveForWrite(relativePath);
        File.Delete(full);
    }
}
