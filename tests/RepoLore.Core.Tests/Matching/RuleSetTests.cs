using FluentAssertions;
using RepoLore.Core.Matching;
using Xunit;

namespace RepoLore.Core.Tests.Matching;

public class RuleSetTests
{
    [Fact]
    public void Frozen_rule_table_produces_the_authored_decisions()
    {
        var rulesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ignore-rules.txt");
        var rules = RuleSet.Compile(File.ReadAllLines(rulesPath));

        var tablePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "expected-matcher.tsv");
        var rows = File.ReadAllLines(tablePath).Where(static l => l.Trim().Length > 0).ToArray();
        rows.Length.Should().BeGreaterThanOrEqualTo(10, $"expected at least 10 rows, got {rows.Length}");

        foreach (var line in rows)
        {
            var fields = line.Split('\t');
            fields.Length.Should().Be(3, line);
            var path = fields[0];
            var isDirectory = fields[1] == "dir";
            var expectedExcluded = fields[2] == "exclude";
            rules.IsExcluded(path, isDirectory).Should().Be(expectedExcluded, line);
        }
    }

    [Fact]
    public void Invalid_rules_are_rejected_with_their_line_numbers()
    {
        foreach (var bad in new[] { "ab**cd", "a**", "**a", "a[bc]", "a\\b", "a//b", "\\x", "!" })
        {
            var ex = new Action(() => RuleSet.Compile(new[] { "ok.md", bad }))
                .Should().Throw<RuleSyntaxException>().Which;
            ex.Line.Should().Be(2, bad);
            ex.Message.Should().NotBeEmpty(bad);
        }
    }

    [Fact]
    public void Last_matching_rule_wins_so_negation_order_matters()
    {
        var reinclude = RuleSet.Compile(new[] { "a/", "!a/keep.md" });
        reinclude.IsExcluded("a/keep.md", false).Should().BeFalse();

        var reexclude = RuleSet.Compile(new[] { "!a/keep.md", "a/" });
        reexclude.IsExcluded("a/keep.md", false).Should().BeTrue();
    }

    [Fact]
    public void Double_star_as_a_whole_segment_matches_zero_or_more_directories()
    {
        var build = RuleSet.Compile(new[] { "**/build/" });
        build.IsExcluded("build", true).Should().BeTrue();
        build.IsExcluded("a/build", true).Should().BeTrue();
        build.IsExcluded("a/b/build/x", false).Should().BeTrue();
        build.IsExcluded("build", false).Should().BeFalse();

        var zero = RuleSet.Compile(new[] { "a/**/b.md" });
        zero.IsExcluded("a/b.md", false).Should().BeTrue();
        zero.IsExcluded("a/x/b.md", false).Should().BeTrue();
        zero.IsExcluded("a/x/y/b.md", false).Should().BeTrue();
        zero.IsExcluded("a/x/c.md", false).Should().BeFalse();
    }
}
