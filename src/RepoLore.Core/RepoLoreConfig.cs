namespace RepoLore.Core;

public sealed class RepoLoreConfig
{
    public const bool DefaultHistoryEnabled = true;
    public const long DefaultMaxBytes = 209715200;

    public int FormatVersion { get; }
    public bool HistoryEnabled { get; }
    public long HistoryMaxBytes { get; }
    public RuleSet HistoryExclude { get; }
    public JsonObject Raw { get; }

    public RepoLoreConfig(int formatVersion, bool historyEnabled, long historyMaxBytes, RuleSet historyExclude, JsonObject raw)
    {
        FormatVersion = formatVersion;
        HistoryEnabled = historyEnabled;
        HistoryMaxBytes = historyMaxBytes;
        HistoryExclude = historyExclude;
        Raw = raw;
    }
}
