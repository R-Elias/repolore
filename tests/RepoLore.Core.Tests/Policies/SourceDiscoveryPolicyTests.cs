using FluentAssertions;
using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using Xunit;

namespace RepoLore.Core.Tests.Policies;

public class SourceDiscoveryPolicyTests
{
    [Fact]
    public void Source_discovery_applies_hard_exclusions_defaults_and_user_overrides()
    {
        var policy = new SourceDiscoveryPolicy();
        policy.IsExcluded(".git/config", false).Should().BeTrue();
        policy.IsExcluded("_repolore/root.md", false).Should().BeTrue();
        policy.IsExcluded("bin/a.dll", false).Should().BeTrue();
        policy.IsExcluded("src/bin/a.dll", false).Should().BeTrue();
        policy.IsExcluded("obj/a.o", false).Should().BeTrue();
        policy.IsExcluded("node_modules/x", false).Should().BeTrue();
        policy.IsExcluded("build/a", false).Should().BeTrue();
        policy.IsExcluded("vendor/a", false).Should().BeTrue();
        policy.IsExcluded("src/app.cs", false).Should().BeFalse();
        policy.IsExcluded("readme.md", false).Should().BeFalse();
        policy.HasNegation.Should().BeFalse();

        var user = RuleSet.Compile(new[] { "!vendor/keep.md", "!.git/config" });
        var overridden = new SourceDiscoveryPolicy(user);
        overridden.HasNegation.Should().BeTrue();
        overridden.IsExcluded("vendor/keep.md", false).Should().BeFalse();
        overridden.IsExcluded("vendor/other.md", false).Should().BeTrue();
        overridden.IsExcluded(".git/config", false).Should().BeTrue();
        overridden.IsExcluded("_repolore/root.md", false).Should().BeTrue();
    }
}
