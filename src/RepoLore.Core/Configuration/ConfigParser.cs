using RepoLore.Core.Format;
using RepoLore.Core.Json;
using RepoLore.Core.Matching;

namespace RepoLore.Core.Configuration;

public sealed class ConfigException : Exception
{
    public ConfigException(string message) : base(message) { }
}

public static class ConfigParser
{
    public static RepoLoreConfig Parse(string jsonText)
    {
        JsonObject root;
        try
        {
            root = JsonParser.Parse(jsonText) as JsonObject
                ?? throw new ConfigException("repolore.json must contain a JSON object.");
        }
        catch (JsonParseException ex)
        {
            throw new ConfigException($"repolore.json is not valid JSON: {ex.Message}");
        }

        DetectDuplicates(root, "repolore.json");

        var hasFormatVersion = false;
        var formatVersion = 0;
        JsonValue? historyRaw = null;

        foreach (var member in root.Members)
        {
            switch (member.Name)
            {
                case "formatVersion":
                    hasFormatVersion = true;
                    formatVersion = ReadFormatVersion(member.Value);
                    break;
                case "history":
                    historyRaw = member.Value;
                    break;
                default:
                    break;
            }
        }

        if (!hasFormatVersion)
            throw new ConfigException("repolore.json is missing the required 'formatVersion' field.");

        var enabled = RepoLoreConfig.DefaultHistoryEnabled;
        var maxBytes = RepoLoreConfig.DefaultMaxBytes;
        var exclude = new List<string>();

        if (historyRaw is not null)
            ReadHistory(historyRaw, out enabled, out maxBytes, exclude);

        RuleSet historyExclude;
        try
        {
            historyExclude = RuleSet.Compile(exclude);
        }
        catch (RuleSyntaxException ex)
        {
            throw new ConfigException($"history.exclude rule #{ex.Line} is invalid: {ex.Message}");
        }

        return new RepoLoreConfig(formatVersion, enabled, maxBytes, historyExclude, root);
    }

    private static void ReadHistory(JsonValue historyRaw, out bool enabled, out long maxBytes, List<string> exclude)
    {
        if (historyRaw is not JsonObject history)
            throw new ConfigException("'history' must be an object.");

        DetectDuplicates(history, "history");

        enabled = RepoLoreConfig.DefaultHistoryEnabled;
        maxBytes = RepoLoreConfig.DefaultMaxBytes;

        foreach (var member in history.Members)
        {
            switch (member.Name)
            {
                case "enabled":
                    enabled = ReadBool(member.Value, "history.enabled");
                    break;
                case "maxBytes":
                    maxBytes = ReadMaxBytes(member.Value);
                    break;
                case "exclude":
                    ReadExclude(member.Value, exclude);
                    break;
                default:
                    break;
            }
        }
    }

    private static int ReadFormatVersion(JsonValue value)
    {
        if (value is not JsonNumberValue number)
            throw new ConfigException("'formatVersion' must be an integer number.");
        if (!TryParseInteger(number.Raw, out var parsed))
            throw new ConfigException("'formatVersion' must be a non-negative integer.");
        if (parsed != KnowledgeFormat.Current)
        {
            if (parsed > KnowledgeFormat.Current)
                throw new ConfigException(
                    $"Unknown format version {parsed}; this build supports {KnowledgeFormat.Current}. Upgrade RepoLore to open this knowledge base.");
            throw new ConfigException($"Unsupported format version {parsed}; expected {KnowledgeFormat.Current}.");
        }
        return (int)parsed;
    }

    private static long ReadMaxBytes(JsonValue value)
    {
        if (value is not JsonNumberValue number)
            throw new ConfigException("'history.maxBytes' must be an integer number.");
        if (!TryParseInteger(number.Raw, out var parsed))
            throw new ConfigException("'history.maxBytes' must be a non-negative integer that fits in a 64-bit signed value.");
        if (parsed < 0)
            throw new ConfigException("'history.maxBytes' must not be negative.");
        return parsed;
    }

    private static bool ReadBool(JsonValue value, string field)
    {
        if (value is JsonBooleanValue boolean)
            return boolean.Value;
        throw new ConfigException($"'{field}' must be a boolean (true or false).");
    }

    private static void ReadExclude(JsonValue value, List<string> exclude)
    {
        if (value is not JsonArray array)
            throw new ConfigException("'history.exclude' must be an array of strings.");
        foreach (var item in array.Items)
        {
            if (item is not JsonStringValue text)
                throw new ConfigException("'history.exclude' must contain only strings.");
            exclude.Add(text.Value);
        }
    }

    private static void DetectDuplicates(JsonObject obj, string label)
    {
        for (var i = 0; i < obj.Members.Count; i++)
            for (var j = i + 1; j < obj.Members.Count; j++)
                if (string.Equals(obj.Members[i].Name, obj.Members[j].Name, StringComparison.Ordinal))
                    throw new ConfigException($"{label} contains a duplicate key '{obj.Members[i].Name}'.");
    }

    private static bool TryParseInteger(string raw, out long value)
    {
        value = 0;
        if (raw.Length == 0)
            return false;
        if (raw.Contains('.') || raw.Contains('e') || raw.Contains('E'))
            return false;

        var negative = false;
        var start = 0;
        if (raw[0] == '-')
        {
            negative = true;
            start = 1;
        }
        if (start == raw.Length)
            return false;

        long accumulated = 0;
        for (var i = start; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c is < '0' or > '9')
                return false;
            var digit = c - '0';
            if (accumulated > (long.MaxValue - digit) / 10)
                return false;
            accumulated = accumulated * 10 + digit;
        }

        value = negative ? -accumulated : accumulated;
        return true;
    }
}
