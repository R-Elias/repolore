using System.Globalization;
using RepoLore.Core.Json;
using RepoLore.Core.Snapshot;

namespace RepoLore.Core.Restore;

public sealed class PendingPlanEntry
{
    public PendingPlanEntry(string path, string? before, string? after)
    {
        Path = path;
        Before = before;
        After = after;
    }

    public string Path { get; }
    public string? Before { get; }
    public string? After { get; }
}

public sealed class PendingTransaction
{
    public PendingTransaction(
        int historyVersion,
        long targetId,
        long preOperationId,
        int formatVersion,
        bool historyEnabled,
        long maxBytes,
        IReadOnlyList<string> excludeRules,
        IReadOnlyList<PendingPlanEntry> plan)
    {
        HistoryVersion = historyVersion;
        TargetId = targetId;
        PreOperationId = preOperationId;
        FormatVersion = formatVersion;
        HistoryEnabled = historyEnabled;
        MaxBytes = maxBytes;
        ExcludeRules = excludeRules;
        Plan = plan;
    }

    public int HistoryVersion { get; }
    public long TargetId { get; }
    public long PreOperationId { get; }
    public int FormatVersion { get; }
    public bool HistoryEnabled { get; }
    public long MaxBytes { get; }
    public IReadOnlyList<string> ExcludeRules { get; }
    public IReadOnlyList<PendingPlanEntry> Plan { get; }
}

public sealed class PendingFormatException : Exception
{
    public PendingFormatException(string message) : base(message) { }
}

public static class PendingCodec
{
    public static JsonValue Encode(PendingTransaction transaction)
    {
        var root = new JsonObject();
        root.Members.Add(new JsonMember("historyVersion", Number(transaction.HistoryVersion)));
        root.Members.Add(new JsonMember("targetId", Number(transaction.TargetId)));
        root.Members.Add(new JsonMember("preOperationId", Number(transaction.PreOperationId)));

        var config = new JsonObject();
        config.Members.Add(new JsonMember("formatVersion", Number(transaction.FormatVersion)));
        config.Members.Add(new JsonMember("historyEnabled", new JsonBooleanValue(transaction.HistoryEnabled)));
        config.Members.Add(new JsonMember("maxBytes", Number(transaction.MaxBytes)));
        var exclude = new JsonArray();
        foreach (var rule in transaction.ExcludeRules)
            exclude.Items.Add(new JsonStringValue(rule));
        config.Members.Add(new JsonMember("exclude", exclude));
        root.Members.Add(new JsonMember("config", config));

        var plan = new JsonArray();
        foreach (var entry in transaction.Plan)
        {
            var obj = new JsonObject();
            obj.Members.Add(new JsonMember("path", new JsonStringValue(entry.Path)));
            obj.Members.Add(new JsonMember("before", entry.Before is null ? new JsonNullValue() : new JsonStringValue(entry.Before)));
            obj.Members.Add(new JsonMember("after", entry.After is null ? new JsonNullValue() : new JsonStringValue(entry.After)));
            plan.Items.Add(obj);
        }
        root.Members.Add(new JsonMember("plan", plan));

        return root;
    }

    public static PendingTransaction Decode(JsonValue value)
    {
        if (value is not JsonObject root)
            throw new PendingFormatException("pending recovery record must be a JSON object");

        var historyVersion = ReadInt(root, "historyVersion");
        if (historyVersion != HistoryVersion.Current)
            throw new PendingFormatException($"unknown history version {historyVersion}; this build supports {HistoryVersion.Current}");

        var targetId = ReadLong(root, "targetId");
        if (targetId < 0)
            throw new PendingFormatException("pending 'targetId' must not be negative");

        var preOperationId = ReadLong(root, "preOperationId");
        if (preOperationId < 0)
            throw new PendingFormatException("pending 'preOperationId' must not be negative");

        var config = ReadObject(root, "config");
        var formatVersion = ReadInt(config, "formatVersion");
        var historyEnabled = ReadBool(config, "historyEnabled");
        var maxBytes = ReadLong(config, "maxBytes");
        if (maxBytes < 0)
            throw new PendingFormatException("pending 'maxBytes' must not be negative");

        var exclude = new List<string>();
        var excludeValue = Required(config, "exclude");
        if (excludeValue is not JsonArray excludeArray)
            throw new PendingFormatException("pending 'exclude' must be an array");
        foreach (var item in excludeArray.Items)
        {
            if (item is not JsonStringValue rule)
                throw new PendingFormatException("pending 'exclude' must contain only strings");
            exclude.Add(rule.Value);
        }

        var plan = new List<PendingPlanEntry>();
        var planValue = Required(root, "plan");
        if (planValue is not JsonArray planArray)
            throw new PendingFormatException("pending 'plan' must be an array");
        foreach (var item in planArray.Items)
            plan.Add(ReadPlanEntry(item));

        return new PendingTransaction(historyVersion, targetId, preOperationId, formatVersion, historyEnabled, maxBytes, exclude, plan);
    }

    private static PendingPlanEntry ReadPlanEntry(JsonValue value)
    {
        if (value is not JsonObject obj)
            throw new PendingFormatException("pending plan entries must be objects");
        var path = ReadString(obj, "path");
        return new PendingPlanEntry(path, ReadNullableString(obj, "before"), ReadNullableString(obj, "after"));
    }

    private static JsonNumberValue Number(long value) => new(value.ToString(CultureInfo.InvariantCulture));

    private static JsonValue Required(JsonObject obj, string name)
    {
        foreach (var member in obj.Members)
            if (string.Equals(member.Name, name, StringComparison.Ordinal))
                return member.Value;
        throw new PendingFormatException($"pending record is missing the required '{name}' field");
    }

    private static string ReadString(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is not JsonStringValue text)
            throw new PendingFormatException($"pending '{name}' must be a string");
        return text.Value;
    }

    private static string? ReadNullableString(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is JsonNullValue)
            return null;
        if (value is JsonStringValue text)
            return text.Value;
        throw new PendingFormatException($"pending '{name}' must be a string or null");
    }

    private static bool ReadBool(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is JsonBooleanValue boolean)
            return boolean.Value;
        throw new PendingFormatException($"pending '{name}' must be a boolean");
    }

    private static JsonObject ReadObject(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is JsonObject child)
            return child;
        throw new PendingFormatException($"pending '{name}' must be an object");
    }

    private static long ReadLong(JsonObject obj, string name)
    {
        var value = Required(obj, name);
        if (value is not JsonNumberValue number)
            throw new PendingFormatException($"pending '{name}' must be a number");
        if (!JsonNumbers.TryParseInteger(number.Raw, out var parsed))
            throw new PendingFormatException($"pending '{name}' must be an integer");
        return parsed;
    }

    private static int ReadInt(JsonObject obj, string name)
    {
        var parsed = ReadLong(obj, name);
        if (parsed is < int.MinValue or > int.MaxValue)
            throw new PendingFormatException($"pending '{name}' is out of range");
        return (int)parsed;
    }
}
