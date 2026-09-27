using RepoLore.Core.Json;
using RepoLore.Core.Snapshot;

namespace RepoLore.Infrastructure.History;

public sealed class CleanupResult
{
    public CleanupResult(long retainedBytes, int evicted, int reclaimed, bool budgetOk)
    {
        RetainedBytes = retainedBytes;
        Evicted = evicted;
        Reclaimed = reclaimed;
        BudgetOk = budgetOk;
    }

    public long RetainedBytes { get; }
    public int Evicted { get; }
    public int Reclaimed { get; }
    public bool BudgetOk { get; }
}

public sealed class HistoryCleanup
{
    private readonly ObjectStore _objects;
    private readonly CheckpointStore _checkpoints;
    private readonly Func<string, bool> _delete;

    public HistoryCleanup(ObjectStore objects, CheckpointStore checkpoints, Func<string, bool>? deleteFile = null)
    {
        _objects = objects;
        _checkpoints = checkpoints;
        _delete = deleteFile ?? DeleteFile;
    }

    public static long SnapshotBytes(CheckpointManifest manifest) =>
        ManifestBytes(manifest) + ObjectBytes(manifest.Files);

    public CleanupResult Clean(long maxBytes)
    {
        var all = _checkpoints.ListCompleted();
        if (all.Count == 0)
            return new CleanupResult(0, 0, 0, true);

        var manifestBytes = new Dictionary<long, long>(all.Count);
        var refCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var refSize = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var manifest in all)
        {
            manifestBytes[manifest.Id] = ManifestBytes(manifest);
            foreach (var file in manifest.Files)
            {
                if (refCount.TryGetValue(file.Hash, out var count))
                    refCount[file.Hash] = count + 1;
                else
                {
                    refCount[file.Hash] = 1;
                    refSize[file.Hash] = file.Size;
                }
            }
        }

        long total = 0;
        foreach (var bytes in manifestBytes.Values)
            total += bytes;
        foreach (var hash in refCount.Keys)
            total += refSize[hash];

        long latest = all[^1].Id;
        var evict = new List<long>();
        foreach (var manifest in all)
        {
            if (total <= maxBytes)
                break;
            if (manifest.Id == latest)
                continue;
            evict.Add(manifest.Id);
            total -= manifestBytes[manifest.Id];
            foreach (var file in manifest.Files)
            {
                var count = refCount[file.Hash] - 1;
                refCount[file.Hash] = count;
                if (count == 0)
                    total -= refSize[file.Hash];
            }
        }

        var ok = true;
        var evicted = 0;
        foreach (var id in evict)
        {
            if (_delete(_checkpoints.ManifestPath(id)))
                evicted++;
            else
                ok = false;
        }

        var keep = new HashSet<long>(all.Count);
        foreach (var manifest in all)
            keep.Add(manifest.Id);
        foreach (var id in evict)
            keep.Remove(id);

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var manifest in all)
            if (keep.Contains(manifest.Id))
                foreach (var file in manifest.Files)
                    referenced.Add(file.Hash);

        var toDelete = new List<string>();
        if (Directory.Exists(_objects.ObjectsDirectory))
            foreach (var file in Directory.EnumerateFiles(_objects.ObjectsDirectory))
                if (!referenced.Contains(Path.GetFileName(file)))
                    toDelete.Add(file);

        var reclaimed = 0;
        foreach (var file in toDelete)
        {
            if (_delete(file))
                reclaimed++;
            else
                ok = false;
        }

        return new CleanupResult(total, evicted, reclaimed, ok && total <= maxBytes);
    }

    private static long ManifestBytes(CheckpointManifest manifest) =>
        Utf8Len(JsonWriter.Write(ManifestCodec.Encode(manifest)));

    private static long ObjectBytes(IReadOnlyList<FileEntry> files)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var file in files)
            if (seen.Add(file.Hash))
                total += file.Size;
        return total;
    }

    private static long Utf8Len(string text)
    {
        long length = 0;
        foreach (var c in text)
        {
            if (c <= 0x7F)
                length += 1;
            else if (c <= 0x7FF)
                length += 2;
            else if (char.IsHighSurrogate(c))
                length += 4;
            else if (!char.IsLowSurrogate(c))
                length += 3;
        }
        return length;
    }

    private static bool DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
