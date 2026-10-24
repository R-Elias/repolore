using RepoLore.Core.Health;

namespace RepoLore.Infrastructure.Health;

public sealed class HealthReport
{
    public HealthReport(IReadOnlyList<Finding> findings, PolicyExplanation? explanation)
    {
        Findings = findings;
        Explanation = explanation;
    }

    public IReadOnlyList<Finding> Findings { get; }
    public PolicyExplanation? Explanation { get; }

    public bool HasBlockingFinding => Findings.Any(static f => FindingIds.IsBlocking(f.Id));
}
