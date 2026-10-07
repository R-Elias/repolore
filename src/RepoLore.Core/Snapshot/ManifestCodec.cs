using System.Globalization;
using RepoLore.Core.Json;

namespace RepoLore.Core.Snapshot;

public sealed class ManifestFormatException : Exception
{
    public ManifestFormatException(string message) : base(message) { }
}

public static class ManifestId
{
    public const int Width = 16;
    private const string Suffix = ".json";

    public static string Format(long id) => id.ToString("D" + Width, CultureInfo.InvariantCulture);

    public static bool TryParse(string fileName, out long id)
    {
        id = 0;
        if (!fileName.EndsWith(Suffix, StringComparison.Ordinal))
            return false;

        var stem = fileName.Substring(0, fileName.Length - Suffix.Length);
        if (stem.Length != Width)
            return false;

        long value = 0;
        for (var i = 0; i < stem.Length; i++)
        {
            var c = stem[i];
            if (c < '0' || c > '9')
                return false;
            value = value * 10 + (c - '0');
        }

        id = value;
        return true;
    }
}

public static class ManifestCodec
{
    public static JsonValue Encode(CheckpointManifest manifest)
    {
        var root = new JsonObject();
        root.Members.Add(new JsonMember("historyVersion", Number(manifest.HistoryVersion)));
        root.Members.Add(new JsonMember("id", Number(manifest.Id)));
        root.Members.Add(new JsonMember("timestamp", new JsonStringValue(manifest.Timestamp)));
        root.Members.Add(new JsonMember("knowledgeFormat", Number(manifest.KnowledgeFormat)));

        var scope = new JsonObject();
        scope.Members.Add(new JsonMember("historyEnabled", new JsonBooleanValue(manifest.Scope.HistoryEnabled)));
        scope.Members.Add(new JsonMember("maxBytes", Number(manifest.Scope.MaxBytes)));
        var exclude = new JsonArray();
        foreach (var rule in manifest.Scope.ExcludeRules)
            exclude.Items.Add(new JsonStringValue(rule));
        scope.Members.Add(new JsonMember("exclude", exclude));
        scope.Members.Add(new JsonMember("repoloreJson", new JsonStringValue(manifest.Scope.RepoLoreJsonStatus)));
        if (manifest.Scope.MigrationScope)
            scope.Members.Add(new JsonMember("migrationScope", new JsonBooleanValue(true)));
        root.Members.Add(new JsonMember("scope", scope));

        var files = new JsonArray();
        foreach (var file in manifest.Files)
        {
            var obj = new JsonObject();
            obj.Members.Add(new JsonMember("path", new JsonStringValue(file.Path)));
            obj.Members.Add(new JsonMember("hash", new JsonStringValue(file.Hash)));
            obj.Members.Add(new JsonMember("size", Number(file.Size)));
            files.Items.Add(obj);
        }
        root.Members.Add(new JsonMember("files", files));

        return root;
    }

    public static CheckpointManifest Decode(JsonValue value)
    {
        if (value is not JsonObject root)
            throw new ManifestFormatException("manifest must be a JSON object");

        var historyVersion = ReadInt(root, "historyVersion");
        if (historyVersion != HistoryVersion.Current)
            throw new ManifestFormatException($"unknown history version {historyVersion}; this build supports {HistoryVersion.Current}");

        var id = ReadLong(root, "id");
        if (id < 0)
            throw new ManifestFormatException("manifest 'id' must not be negative");

        var knowledgeFormat = ReadInt(root, "knowledgeFormat");
        if (knowledgeFormat < 0)
            throw new ManifestFormatException("manifest 'knowledgeFormat' must not be negative");

        var timestamp = ReadString(root, "timestamp");
        var scope = ReadScope(ReadObject(root, "scope"));
        var files = ReadFiles(ReadArray(root, "files"));

        return new CheckpointManifest(id, timestamp, historyVersion, knowledgeFormat, scope, files);
    }

    private static CaptureScope ReadScope(JsonObject obj)
    {
        var historyEnabled = ReadBool(obj, "historyEnabled");
        var maxBytes = ReadLong(obj, "maxBytes");
        if (maxBytes < 0)
            throw new ManifestFormatException("manifest scope 'maxBytes' must not be negative");

        var exclude = new List<string>();
        var excludeValue = Required(obj, "exclude");
        if (excludeValue is not JsonArray array)
            throw new ManifestFormatException("manifest scope 'exclude' must be an array");
        foreach (var item in array.Items)
        {
            if (item is not JsonStringValue text)
                throw new ManifestFormatException("manifest scope 'exclude' must contain only strings");
            exclude.Add(text.Value);
        }

        var repoLoreJson = ReadString(obj, "repoloreJson");
        if (repoLoreJson is not (CaptureScope.RepoLoreJsonPresent or CaptureScope.RepoLoreJsonAbsent or CaptureScope.RepoLoreJsonExcluded))
            throw new ManifestFormatException($"manifest scope 'repoloreJson' has an unknown status '{repoLoreJson}'");

        var migrationScope = ReadOptionalBool(obj, "migrationScope");

        return new CaptureScope(historyEnabled, maxBytes, exclude, repoLoreJson, migrationScope);
    }

    private static bool ReadOptionalBool(JsonObject obj, string name)
    {
        foreach (var member in obj.Members)
            if (string.Equals(member.Name, name, StringComparison.Ordinal))
                return member.Value is JsonBooleanValue boolean
                    ? boolean.Value
                    : throw new ManifestFormatException($"manifest scope '{name}' must be a boolean");
        return false;
    }

    private static List<FileEntry> ReadFiles(JsonArray array)
    {
        var files = new List<FileEntry>(array.Items.Count);
        foreach (var item in array.Items)
        {
            if (item is not JsonObject obj)
                throw new ManifestFormatException("manifest 'files' entries must be objects");
            var path = ReadString(obj, "path");
            var hash = ReadString(obj, "hash");
            var size = ReadLong(obj, "size");
            if (size < 0)
                throw new ManifestFormatException($"manifest file '{path}' has a negative size");
            files.Add(new FileEntry(path, hash, size));
        }
        return files;
    }

    private static JsonNumberValue Number(long value) => new(value.ToString(CultureInfo.InvariantCulture));

    private static JsonValue Required(JsonObject obj, string name)
    {
        foreach (var member in obj.Members)
            if (string.Equals(member.Name, name, StringComparison.Ordinal))
                return member.Value;
        throw new ManifestFormatException($"manifest is missing the required '{name}' field");
    }

    private static string ReadString(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is not JsonStringValue text)
            throw new ManifestFormatException($"manifest '{name}' must be a string");
        return text.Value;
    }

    private static bool ReadBool(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is not JsonBooleanValue boolean)
            throw new ManifestFormatException($"manifest '{name}' must be a boolean");
        return boolean.Value;
    }

    private static JsonObject ReadObject(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is not JsonObject child)
            throw new ManifestFormatException($"manifest '{name}' must be an object");
        return child;
    }

    private static JsonArray ReadArray(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is not JsonArray array)
            throw new ManifestFormatException($"manifest '{name}' must be an array");
        return array;
    }

    private static long ReadLong(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is not JsonNumberValue number)
            throw new ManifestFormatException($"manifest '{name}' must be a number");
        if (!JsonNumbers.TryParseInteger(number.Raw, out var parsed))
            throw new ManifestFormatException($"manifest '{name}' must be an integer");
        return parsed;
    }

    private static int ReadInt(JsonObject obj, string name)
    {
        var parsed = ReadLong(obj, name);
        if (parsed is < int.MinValue or > int.MaxValue)
            throw new ManifestFormatException($"manifest '{name}' is out of range");
        return (int)parsed;
    }
}
