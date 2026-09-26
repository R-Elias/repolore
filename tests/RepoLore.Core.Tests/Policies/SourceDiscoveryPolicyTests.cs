using RepoLore.Core.Matching;
using RepoLore.Core.Policies;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Policies;

public static class SourceDiscoveryPolicyTests
{
    public static void Run()
    {
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
    }
}
