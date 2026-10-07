using RepoLore.Core.Configuration;
using RepoLore.Core.Context;
using RepoLore.Core.Migration;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;

namespace RepoLore.Infrastructure.History;

public sealed class MigrationException : Exception
{
    public MigrationException(string message, long? recoveryId = null, Exception? inner = null) : base(message, inner)
    {
        RecoveryId = recoveryId;
    }

    public long? RecoveryId { get; }
}

public sealed class MigrationInventory
{
    public MigrationInventory(
        IReadOnlyList<NoteVariant> notes,
        IReadOnlyList<string> legacyToRemove,
        IReadOnlyList<string> unknownFiles,
        IReadOnlyList<string> customAreas,
        IReadOnlyList<string> sessionAreas,
        bool sessionsNamingConflict)
    {
        Notes = notes;
        LegacyToRemove = legacyToRemove;
        UnknownFiles = unknownFiles;
        CustomAreas = customAreas;
        SessionAreas = sessionAreas;
        SessionsNamingConflict = sessionsNamingConflict;
    }

    public IReadOnlyList<NoteVariant> Notes { get; }
    public IReadOnlyList<string> LegacyToRemove { get; }
    public IReadOnlyList<string> UnknownFiles { get; }
    public IReadOnlyList<string> CustomAreas { get; }
    public IReadOnlyList<string> SessionAreas { get; }
    public bool SessionsNamingConflict { get; }
}

public sealed class MigrationResult
{
    public MigrationResult(long? publishedId, int created, int removed, IReadOnlyList<string> leftBehind)
    {
        PublishedId = publishedId;
        Created = created;
        Removed = removed;
        LeftBehind = leftBehind;
    }

    public long? PublishedId { get; }
    public int Created { get; }
    public int Removed { get; }
    public IReadOnlyList<string> LeftBehind { get; }
}

public sealed class MigrationEngine
{
    public const string MarkerPath = "_repolore/repolore.json";
    public const string MarkerContent = "{\"formatVersion\":1}";

    private const string TreePrefix = "_repolore/tree";
    private const string SparsePrefix = "_repolore/sparse-tree";

    private readonly string _repositoryRoot;
    private readonly string _historyDirectory;
    private readonly RepositoryPathResolver _resolver;
    private readonly ObjectStore _objects;
    private readonly CheckpointStore _checkpoints;
    private readonly CheckpointEngine _engine;
    private readonly PendingStore _pending;

    public MigrationEngine(string repositoryRoot, string historyDirectory, Clock? clock = null, Func<string, bool>? deleteFile = null)
    {
        _repositoryRoot = repositoryRoot;
        _historyDirectory = historyDirectory;
        _resolver = new RepositoryPathResolver(repositoryRoot);
        _objects = new ObjectStore(historyDirectory);
        _checkpoints = new CheckpointStore(historyDirectory);
        _engine = new CheckpointEngine(repositoryRoot, historyDirectory, clock, deleteFile);
        _pending = new PendingStore(historyDirectory);
    }

    public InitDirKind Classify() => new InitEngine(_repositoryRoot, _historyDirectory).Classify();

    public MigrationInventory Inventory()
    {
        var notes = new List<NoteVariant>();
        var legacyToRemove = new List<string>();
        var unknownFiles = new List<string>();
        var customAreas = new List<string>();
        var sessionAreas = new List<string>();
        var sessionsNamingConflict = false;

        var repoLore = Path.Combine(_repositoryRoot, "_repolore");

        var tree = Path.Combine(repoLore, "tree");
        if (Directory.Exists(tree))
            WalkAlpha(tree, TreePrefix, TreePrefix, NoteSource.Tree, notes, legacyToRemove, unknownFiles);

        var sparse = Path.Combine(repoLore, "sparse-tree");
        if (Directory.Exists(sparse))
            WalkAlpha(sparse, SparsePrefix, SparsePrefix, NoteSource.Sparse, notes, legacyToRemove, unknownFiles, removeRootNote: true);

        var topRoot = Path.Combine(repoLore, "root.md");
        if (File.Exists(topRoot))
        {
            var text = ReadNormalized(topRoot);
            if (text is not null)
                notes.Add(new NoteVariant(NoteSource.TopRoot, "_repolore/root.md", "root.md", text));
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(repoLore))
        {
            var name = Path.GetFileName(entry);
            if (name is "tree" or "sparse-tree" or "sessions" or ".history" or "repolore.json" or "root.md")
                continue;
            customAreas.Add("_repolore/" + name + (Directory.Exists(entry) ? "/" : ""));
        }

        var sessions = Path.Combine(repoLore, "sessions");
        if (Directory.Exists(sessions))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(sessions))
            {
                var name = Path.GetFileName(entry);
                if (Directory.Exists(entry))
                {
                    if (SessionId.IsValid(name, out _))
                        sessionAreas.Add("_repolore/sessions/" + name);
                    else
                        sessionsNamingConflict = true;
                }
                else
                {
                    sessionsNamingConflict = true;
                }
            }
        }

        notes.Sort((a, b) => string.CompareOrdinal(a.PhysicalPath, b.PhysicalPath));
        legacyToRemove.Sort(StringComparer.Ordinal);
        unknownFiles.Sort(StringComparer.Ordinal);
        customAreas.Sort(StringComparer.Ordinal);
        sessionAreas.Sort(StringComparer.Ordinal);

        return new MigrationInventory(notes, legacyToRemove, unknownFiles, customAreas, sessionAreas, sessionsNamingConflict);
    }

    public MigrationResult Apply(
        MigrationInventory inventory,
        IReadOnlyList<MigrationMapping> mappings,
        Action? beforeFirstReplacement = null,
        Action? afterFirstReplacement = null,
        Action? beforePostCheckpoint = null)
    {
        if (inventory.SessionsNamingConflict)
            throw new MigrationException("the reserved _repolore/sessions/ area does not match the session layout; relocate or rename it before migrating");
        foreach (var mapping in mappings)
            if (mapping.Status != MappingStatus.Candidate)
                throw new MigrationException("migration has unresolved conflicts; run 'repolore migrate --check' to see them");

        if (_pending.Read() is not null)
            throw new MigrationException("a mutation is already pending; recover it before migrating");

        var config = HistoryConfigLoader.Load(_repositoryRoot);

        _engine.Capture(config, migrationScope: true);
        var preOpId = _checkpoints.ReadHighestCompleted()?.Id
            ?? throw new MigrationException("no checkpoint exists after the migration baseline capture");

        var entries = BuildPendingEntries(inventory, mappings);
        _pending.Write(new PendingTransaction(
            HistoryVersion.Current,
            preOpId,
            preOpId,
            config.FormatVersion,
            config.HistoryEnabled,
            config.HistoryMaxBytes,
            config.HistoryExcludeRules,
            entries));

        try
        {
            ApplyMutations(entries, beforeFirstReplacement, afterFirstReplacement);
            RemoveEmptyDirectories();
            beforePostCheckpoint?.Invoke();
            var postConfig = HistoryConfigLoader.Load(_repositoryRoot);
            var post = _engine.Capture(postConfig, protectedIds: new HashSet<long> { preOpId });
            _pending.Clear();
            return new MigrationResult(post.PublishedId, CountCopies(mappings), inventory.LegacyToRemove.Count, inventory.UnknownFiles);
        }
        catch (MigrationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MigrationException("migration was interrupted before it could finish: " + ex.Message, preOpId, ex);
        }
    }

    private List<PendingPlanEntry> BuildPendingEntries(MigrationInventory inventory, IReadOnlyList<MigrationMapping> mappings)
    {
        var entries = new List<PendingPlanEntry>();

        foreach (var mapping in mappings)
        {
            if (!mapping.NeedsCopy)
                continue;
            entries.Add(new PendingPlanEntry(mapping.Destination, CurrentHash(mapping.Destination), CurrentHash(mapping.SourcePath!)));
        }

        foreach (var legacy in inventory.LegacyToRemove)
            entries.Add(new PendingPlanEntry(legacy, CurrentHash(legacy), null));

        entries.Add(new PendingPlanEntry(MarkerPath, null, MarkerHash()));

        return entries;
    }

    private void ApplyMutations(List<PendingPlanEntry> entries, Action? beforeFirstReplacement, Action? afterFirstReplacement)
    {
        beforeFirstReplacement?.Invoke();
        var applied = 0;

        foreach (var entry in entries)
        {
            RecheckBefore(entry.Path, entry.Before);

            if (entry.Path == MarkerPath)
                WriteMarker();
            else if (entry.After is null)
                RemoveFile(entry.Path);
            else
                WriteFile(entry.Path, _objects.Read(entry.After));

            applied++;
            if (applied == 1)
                afterFirstReplacement?.Invoke();
        }
    }

    private void RecheckBefore(string relativePath, string? before)
    {
        if (!string.Equals(CurrentHash(relativePath), before, StringComparison.Ordinal))
            throw new MigrationException($"file changed during migration: '{relativePath}' no longer matches its pre-migration state");
    }

    private void RemoveEmptyDirectories()
    {
        var tree = Path.Combine(_repositoryRoot, "_repolore", "tree");
        if (Directory.Exists(tree))
            RemoveEmptyDirectoriesUnder(tree);
    }

    private void RemoveEmptyDirectoriesUnder(string directory)
    {
        foreach (var child in Directory.EnumerateDirectories(directory))
            RemoveEmptyDirectoriesUnder(child);

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
            Directory.Delete(directory);
    }

    private void WriteMarker()
    {
        var full = _resolver.ResolveForWrite(MarkerPath);
        var parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var tmpDir = Path.Combine(_historyDirectory, "tmp");
        Directory.CreateDirectory(tmpDir);
        var tmp = Path.Combine(tmpDir, "marker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(tmp, MarkerContent);
        File.Move(tmp, full, overwrite: true);
    }

    private string MarkerHash()
    {
        var tmpDir = Path.Combine(_historyDirectory, "tmp");
        Directory.CreateDirectory(tmpDir);
        var tmp = Path.Combine(tmpDir, "markerhash-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(tmp, MarkerContent);
        var bytes = File.ReadAllBytes(tmp);
        File.Delete(tmp);
        return ObjectStore.Hash(bytes);
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

    private string? CurrentHash(string relativePath)
    {
        var full = _resolver.Resolve(relativePath);
        if (!File.Exists(full))
            return null;
        return ObjectStore.Hash(File.ReadAllBytes(full));
    }

    private static int CountCopies(IReadOnlyList<MigrationMapping> mappings)
    {
        var count = 0;
        foreach (var mapping in mappings)
            if (mapping.NeedsCopy)
                count++;
        return count;
    }

    private void WalkAlpha(
        string directory,
        string relativeDirectory,
        string alphaRootPrefix,
        NoteSource source,
        List<NoteVariant> notes,
        List<string> legacyToRemove,
        List<string> unknownFiles,
        bool removeRootNote = false)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            if (RepositoryPathResolver.IsReparsePoint(entry))
                continue;

            var name = Path.GetFileName(entry);
            var relative = relativeDirectory + "/" + name;

            if (Directory.Exists(entry))
            {
                WalkAlpha(entry, relative, alphaRootPrefix, source, notes, legacyToRemove, unknownFiles, removeRootNote);
                continue;
            }

            if (!name.EndsWith(".md", StringComparison.Ordinal))
            {
                unknownFiles.Add(relative);
                continue;
            }

            var text = ReadNormalized(entry);
            if (text is null)
            {
                unknownFiles.Add(relative);
                continue;
            }

            if (source == NoteSource.Tree)
                legacyToRemove.Add(relative);
            else if (removeRootNote && relative == SparsePrefix + "/root.md")
                legacyToRemove.Add(relative);

            notes.Add(new NoteVariant(source, relative, relative.Substring(alphaRootPrefix.Length + 1), text));
        }
    }

    private static string? ReadNormalized(string fullPath)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return NoteContent.TryNormalize(bytes, out var text, out _) ? text : null;
    }
}
