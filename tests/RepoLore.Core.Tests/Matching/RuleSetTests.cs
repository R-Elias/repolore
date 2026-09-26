using RepoLore.Core.Matching;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Matching;

public static class RuleSetTests
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
    }
}
