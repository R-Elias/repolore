using RepoLore.Core.Matching;
using RepoLore.Core.Policies;

namespace RepoLore.Core.Health;

public sealed class PolicyExplanation
{
    public PolicyExplanation(string path, bool isDirectory, SourceDiscoveryDecision sourceDiscovery, bool historyCovered, string historyReason, CompiledRule? historyRule)
    {
        Path = path;
        IsDirectory = isDirectory;
        SourceDiscovery = sourceDiscovery;
        HistoryCovered = historyCovered;
        HistoryReason = historyReason;
        HistoryRule = historyRule;
    }

    public string Path { get; }
    public bool IsDirectory { get; }
    public SourceDiscoveryDecision SourceDiscovery { get; }
    public bool HistoryCovered { get; }
    public string HistoryReason { get; }
    public CompiledRule? HistoryRule { get; }

    public static PolicyExplanation Explain(string path, bool isDirectory, bool isSymlink, SourceDiscoveryPolicy discovery, RuleSet historyExclude)
    {
        var sourceDecision = discovery.Decide(path, isDirectory);

        if (!HistoryCoveragePolicy.IsEligible(path, isDirectory, isSymlink))
            return new PolicyExplanation(path, isDirectory, sourceDecision, false,
                "not eligible: history covers only _repoloreignore, _repolore/repolore.json and regular Markdown files under _repolore/ outside .history/", null);

        var rule = historyExclude.Evaluate(path, isDirectory);
        if (rule is null)
            return new PolicyExplanation(path, isDirectory, sourceDecision, true, "eligible; no history.exclude rule matches", null);
        if (rule.Negated)
            return new PolicyExplanation(path, isDirectory, sourceDecision, true, "eligible; re-included by a history.exclude rule", rule);
        return new PolicyExplanation(path, isDirectory, sourceDecision, false, "eligible but excluded by a history.exclude rule", rule);
    }
}
