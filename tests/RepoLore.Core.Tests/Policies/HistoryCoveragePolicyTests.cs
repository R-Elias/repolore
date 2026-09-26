using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Policies;

public static class HistoryCoveragePolicyTests
{
    public static void Run()
    {
        TestRunner.Check("history coverage captures Gitignored sessions and honors history.exclude", () =>
        {
            var none = RuleSet.Empty;

            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/root.md", false, false, none));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/investigation.md", false, false, none));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repoloreignore", false, false, none));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/repolore.json", false, false, none));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/method.md", false, false, none));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/root.md", false, false, none));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/sparse-tree/src/src.md", false, false, none));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/architecture/a.md", false, false, none));

            TestRunner.True(!HistoryCoveragePolicy.IsCovered("_repolore/data.txt", false, false, none));
            TestRunner.True(!HistoryCoveragePolicy.IsCovered("src/app.cs", false, false, none));
            TestRunner.True(!HistoryCoveragePolicy.IsCovered("_repolore/.history/objects/x", false, false, none));
            TestRunner.True(!HistoryCoveragePolicy.IsCovered("_repolore/.history/checkpoints/1.json", false, false, none));
            TestRunner.True(!HistoryCoveragePolicy.IsCovered("_repolore/sessions/a/root.md", true, false, none));
            TestRunner.True(!HistoryCoveragePolicy.IsCovered("_repolore/sessions/a/root.md", false, true, none));

            var exclude = RuleSet.Compile(new[] { "_repolore/sessions/session-a/" });
            TestRunner.True(!HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/root.md", false, false, exclude));
            TestRunner.True(!HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-a/investigation.md", false, false, exclude));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/sessions/session-b/root.md", false, false, exclude));
            TestRunner.True(HistoryCoveragePolicy.IsCovered("_repolore/root.md", false, false, exclude));
        });
    }
}
