using RepoLore.Core.Json;
using RepoLore.Core.Matching;

namespace RepoLore.Core.Configuration;

public sealed class RepoLoreConfig
{
    public const bool DefaultHistoryEnabled = true;
    public const long DefaultMaxBytes = 209715200;

    public int FormatVersion { get; }
    public bool HistoryEnabled { get; }
    public long HistoryMaxBytes { get; }
    public RuleSet HistoryExclude { get; }
    public IReadOnlyList<string> HistoryExcludeRules { get; }
    public JsonObject Raw { get; }

    public RepoLoreConfig(int formatVersion, bool historyEnabled, long historyMaxBytes, RuleSet historyExclude, IReadOnlyList<string> historyExcludeRules, JsonObject raw)
    {
        FormatVersion = formatVersion;
        HistoryEnabled = historyEnabled;
        HistoryMaxBytes = historyMaxBytes;
        HistoryExclude = historyExclude;
        HistoryExcludeRules = historyExcludeRules;
        Raw = raw;
    }
}
