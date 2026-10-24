using FluentAssertions;
using RepoLore.Core.Health;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using Xunit;

namespace RepoLore.Core.Tests.Health;

public class PolicyExplanationTests
{
    private static readonly SourceDiscoveryPolicy NoUserRules = new();

    [Fact]
    public void Default_generated_directory_rule_is_named_with_its_layer()
    {
        var explanation = PolicyExplanation.Explain("src/build/a.txt", false, false, NoUserRules, RuleSet.Empty);
        explanation.SourceDiscovery.Excluded.Should().BeTrue();
        explanation.SourceDiscovery.Layer.Should().Be("default");
        explanation.SourceDiscovery.Rule!.Display.Should().Be("**/build/");
    }

    [Fact]
    public void User_reinclusion_overrides_a_default_and_is_reported_as_a_user_rule()
    {
        var user = new SourceDiscoveryPolicy(RuleSet.Compile(new[] { "!vendor/keep.md" }));
        var explanation = PolicyExplanation.Explain("vendor/keep.md", false, false, user, RuleSet.Empty);
        explanation.SourceDiscovery.Excluded.Should().BeFalse();
        explanation.SourceDiscovery.Layer.Should().Be("user");
        explanation.SourceDiscovery.Rule!.Negated.Should().BeTrue();
    }

    [Fact]
    public void Hard_exclusion_wins_and_a_path_matching_nothing_is_included()
    {
        PolicyExplanation.Explain(".git/config", false, false, NoUserRules, RuleSet.Empty)
            .SourceDiscovery.Layer.Should().Be("hard-exclusion");
        var plain = PolicyExplanation.Explain("src/main.cs", false, false, NoUserRules, RuleSet.Empty).SourceDiscovery;
        plain.Excluded.Should().BeFalse();
        plain.Layer.Should().Be("none");
    }

    [Fact]
    public void History_coverage_is_decided_independently_of_source_discovery()
    {
        var exclude = RuleSet.Compile(new[] { "_repolore/sessions/" });

        var session = PolicyExplanation.Explain("_repolore/sessions/a/root.md", false, false, NoUserRules, exclude);
        session.SourceDiscovery.Excluded.Should().BeTrue();
        session.HistoryCovered.Should().BeFalse();
        session.HistoryRule!.Display.Should().Be("_repolore/sessions/");

        var durable = PolicyExplanation.Explain("_repolore/root.md", false, false, NoUserRules, exclude);
        durable.HistoryCovered.Should().BeTrue();
    }

    [Fact]
    public void Ineligible_paths_are_not_covered_whatever_the_rules_say()
    {
        PolicyExplanation.Explain("src/main.cs", false, false, NoUserRules, RuleSet.Empty).HistoryCovered.Should().BeFalse();
        PolicyExplanation.Explain("_repolore/.history/objects/x", false, false, NoUserRules, RuleSet.Empty).HistoryCovered.Should().BeFalse();
        PolicyExplanation.Explain("_repolore/root.md", false, true, NoUserRules, RuleSet.Empty).HistoryCovered.Should().BeFalse();
    }
}
