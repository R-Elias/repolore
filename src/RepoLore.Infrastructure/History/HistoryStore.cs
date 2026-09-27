using System.Security.Cryptography;
using RepoLore.Core.Json;
using RepoLore.Core.Snapshot;

namespace RepoLore.Infrastructure.History;

public sealed class HistoryStoreException : Exception
{
    public HistoryStoreException(string message) : base(message) { }
}

public sealed class ObjectStore
{
    private readonly string _objectsDirectory;
    private readonly string _tmpDirectory;

    public ObjectStore(string historyDirectory)
    {
        _objectsDirectory = Path.Combine(historyDirectory, "objects");
        _tmpDirectory = Path.Combine(historyDirectory, "tmp");
    }

    public string ObjectsDirectory => _objectsDirectory;

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(_objectsDirectory);
        Directory.CreateDirectory(_tmpDirectory);
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public bool Exists(string hash) => File.Exists(ObjectPath(hash));

    public byte[] Read(string hash)
    {
        var path = ObjectPath(hash);
        if (!File.Exists(path))
            throw new HistoryStoreException($"object {hash} is missing");
        return File.ReadAllBytes(path);
    }

    public bool Validate(string hash)
    {
        var path = ObjectPath(hash);
        if (!File.Exists(path))
            return false;
        return string.Equals(Hash(File.ReadAllBytes(path)), hash, StringComparison.Ordinal);
    }

    public string Store(byte[] bytes)
    {
        var hash = Hash(bytes);
        var final = ObjectPath(hash);
        if (File.Exists(final))
        {
            if (!Validate(hash))
                throw new HistoryStoreException($"stored object {hash} is corrupt");
            return hash;
        }

        Directory.CreateDirectory(_tmpDirectory);
        var tmp = Path.Combine(_tmpDirectory, hash + "." + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(tmp, bytes);
        File.Move(tmp, final);
        return hash;
    }

    private string ObjectPath(string hash) => Path.Combine(_objectsDirectory, hash);
}

public sealed class CheckpointStore
{
    private readonly string _checkpointsDirectory;

    public CheckpointStore(string historyDirectory)
    {
        _checkpointsDirectory = Path.Combine(historyDirectory, "checkpoints");
    }

    public string CheckpointsDirectory => _checkpointsDirectory;

    public void EnsureDirectory() => Directory.CreateDirectory(_checkpointsDirectory);

    public string ManifestPath(long id) => Path.Combine(_checkpointsDirectory, ManifestId.Format(id) + ".json");

    public List<CheckpointManifest> ListCompleted()
    {
        var manifests = new List<CheckpointManifest>();
        if (!Directory.Exists(_checkpointsDirectory))
            return manifests;

        foreach (var file in Directory.EnumerateFiles(_checkpointsDirectory))
        {
            if (!ManifestId.TryParse(Path.GetFileName(file), out _))
                continue;

            var text = File.ReadAllText(file);
            JsonValue value;
            try
            {
                value = JsonParser.Parse(text);
            }
            catch (JsonParseException ex)
            {
                throw new HistoryStoreException($"corrupt manifest '{Path.GetFileName(file)}': {ex.Message}");
            }

            CheckpointManifest manifest;
            try
            {
                manifest = ManifestCodec.Decode(value);
            }
            catch (ManifestFormatException ex)
            {
                throw new HistoryStoreException($"corrupt manifest '{Path.GetFileName(file)}': {ex.Message}");
            }

            manifests.Add(manifest);
        }

        manifests.Sort((a, b) => a.Id.CompareTo(b.Id));
        return manifests;
    }

    public CheckpointManifest? ReadHighestCompleted()
    {
        var manifests = ListCompleted();
        return manifests.Count == 0 ? null : manifests[^1];
    }

    public void Publish(CheckpointManifest manifest)
    {
        EnsureDirectory();
        var jsonText = JsonWriter.Write(ManifestCodec.Encode(manifest));
        var final = ManifestPath(manifest.Id);
        var tmp = Path.Combine(_checkpointsDirectory, ManifestId.Format(manifest.Id) + ".tmp-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(tmp, jsonText);
        File.Move(tmp, final);
    }
}
