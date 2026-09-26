using RepoLore.Core;

namespace RepoLore.Core.Tests;

public static class MatcherTests
{
    public static void Run()
    {
        TestRunner.Check("frozen rule table produces the authored decisions", () =>
        {
            var rulesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ignore-rules.txt");
            var rules = RuleSet.Compile(File.ReadAllLines(rulesPath));

            var tablePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "expected-matcher.tsv");
            var rows = File.ReadAllLines(tablePath).Where(static l => l.Trim().Length > 0).ToArray();
            TestRunner.True(rows.Length >= 10, $"expected at least 10 rows, got {rows.Length}");

            foreach (var line in rows)
            {
                var fields = line.Split('\t');
                TestRunner.Equal(3, fields.Length, line);
                var path = fields[0];
                var isDirectory = fields[1] == "dir";
                var expectedExcluded = fields[2] == "exclude";
                TestRunner.Equal(expectedExcluded, rules.IsExcluded(path, isDirectory), line);
            }
        });

        TestRunner.Check("invalid rules are rejected with their line numbers", () =>
        {
            foreach (var bad in new[] { "ab**cd", "a**", "**a", "a[bc]", "a\\b", "a//b", "\\x", "!" })
            {
                var ex = TestRunner.Capture<RuleSyntaxException>(() => RuleSet.Compile(new[] { "ok.md", bad }), bad);
                TestRunner.Equal(2, ex.Line, bad);
                TestRunner.True(ex.Message.Length > 0, bad);
            }
        });

        TestRunner.Check("last matching rule wins, so negation order matters", () =>
        {
            var reinclude = RuleSet.Compile(new[] { "a/", "!a/keep.md" });
            TestRunner.True(!reinclude.IsExcluded("a/keep.md", false));

            var reexclude = RuleSet.Compile(new[] { "!a/keep.md", "a/" });
            TestRunner.True(reexclude.IsExcluded("a/keep.md", false));
        });

        TestRunner.Check("'**' as a whole segment matches zero or more directories", () =>
        {
            var build = RuleSet.Compile(new[] { "**/build/" });
            TestRunner.True(build.IsExcluded("build", true));
            TestRunner.True(build.IsExcluded("a/build", true));
            TestRunner.True(build.IsExcluded("a/b/build/x", false));
            TestRunner.True(!build.IsExcluded("build", false));

            var zero = RuleSet.Compile(new[] { "a/**/b.md" });
            TestRunner.True(zero.IsExcluded("a/b.md", false));
            TestRunner.True(zero.IsExcluded("a/x/b.md", false));
            TestRunner.True(zero.IsExcluded("a/x/y/b.md", false));
            TestRunner.True(!zero.IsExcluded("a/x/c.md", false));
        });

        TestRunner.Check("source discovery applies hard exclusions, defaults, and user overrides", () =>
        {
            var policy = new SourceDiscoveryPolicy();
            TestRunner.True(policy.IsExcluded(".git/config", false));
            TestRunner.True(policy.IsExcluded("_repolore/root.md", false));
            TestRunner.True(policy.IsExcluded("bin/a.dll", false));
            TestRunner.True(policy.IsExcluded("src/bin/a.dll", false));
            TestRunner.True(policy.IsExcluded("obj/a.o", false));
            TestRunner.True(policy.IsExcluded("node_modules/x", false));
            TestRunner.True(policy.IsExcluded("build/a", false));
            TestRunner.True(policy.IsExcluded("vendor/a", false));
            TestRunner.True(!policy.IsExcluded("src/app.cs", false));
            TestRunner.True(!policy.IsExcluded("readme.md", false));
            TestRunner.True(!policy.HasNegation);

            var user = RuleSet.Compile(new[] { "!vendor/keep.md", "!.git/config" });
            var overridden = new SourceDiscoveryPolicy(user);
            TestRunner.True(overridden.HasNegation);
            TestRunner.True(!overridden.IsExcluded("vendor/keep.md", false));
            TestRunner.True(overridden.IsExcluded("vendor/other.md", false));
            TestRunner.True(overridden.IsExcluded(".git/config", false));
            TestRunner.True(overridden.IsExcluded("_repolore/root.md", false));
        });

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
