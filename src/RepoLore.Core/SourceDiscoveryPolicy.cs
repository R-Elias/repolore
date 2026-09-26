namespace RepoLore.Core;

public sealed class SourceDiscoveryPolicy
{
    public static readonly IReadOnlyList<string> DefaultExclusions = new[]
    {
        "**/bin/",
        "**/obj/",
        "**/node_modules/",
        "**/build/",
        "**/vendor/"
    };

    public static readonly IReadOnlyList<string> HardExclusions = new[]
    {
        "_repolore/",
        ".git/",
        ".hg/",
        ".svn/"
    };

    private readonly RuleSet _hard = RuleSet.Compile(HardExclusions);
    private readonly RuleSet _combined;

    public SourceDiscoveryPolicy(RuleSet userRules)
    {
        _combined = RuleSet.Compile(DefaultExclusions).Append(userRules);
    }

    public SourceDiscoveryPolicy() : this(RuleSet.Empty) { }

    public bool HasNegation => _combined.HasNegation;

    public bool IsExcluded(string relativePath, bool isDirectory)
    {
        if (_hard.IsExcluded(relativePath, isDirectory))
            return true;
        return _combined.IsExcluded(relativePath, isDirectory);
    }

    public CompiledRule? Explain(string relativePath, bool isDirectory)
    {
        var hard = _hard.Evaluate(relativePath, isDirectory);
        if (hard is not null && !hard.Negated)
            return hard;
        return _combined.Evaluate(relativePath, isDirectory);
    }
}
