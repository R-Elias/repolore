using FluentAssertions;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using Xunit;

namespace RepoLore.Core.Tests.Policies;

public class HistoryCoveragePolicyTests
{
    [Fact]
    public void History_coverage_captures_gitignored_sessions_and_honors_history_exclude()
    {
        var none = RuleSet.Empty;

        HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/root.md", false, false, none).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/investigation.md", false, false, none).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repoloreignore", false, false, none).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repolore/repolore.json", false, false, none).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repolore/method.md", false, false, none).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repolore/root.md", false, false, none).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repolore/sparse-tree/src/src.md", false, false, none).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repolore/architecture/a.md", false, false, none).Should().BeTrue();

        HistoryCoveragePolicy.IsCovered("_repolore/data.txt", false, false, none).Should().BeFalse();
        HistoryCoveragePolicy.IsCovered("src/app.cs", false, false, none).Should().BeFalse();
        HistoryCoveragePolicy.IsCovered("_repolore/.history/objects/x", false, false, none).Should().BeFalse();
        HistoryCoveragePolicy.IsCovered("_repolore/.history/checkpoints/1.json", false, false, none).Should().BeFalse();
        HistoryCoveragePolicy.IsCovered("_repolore/sessions/a/root.md", true, false, none).Should().BeFalse();
        HistoryCoveragePolicy.IsCovered("_repolore/sessions/a/root.md", false, true, none).Should().BeFalse();

        var exclude = RuleSet.Compile(new[] { "_repolore/sessions/session-a/" });
        HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/root.md", false, false, exclude).Should().BeFalse();
        HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/investigation.md", false, false, exclude).Should().BeFalse();
        HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-b/root.md", false, false, exclude).Should().BeTrue();
        HistoryCoveragePolicy.IsCovered("_repolore/root.md", false, false, exclude).Should().BeTrue();
    }
}
