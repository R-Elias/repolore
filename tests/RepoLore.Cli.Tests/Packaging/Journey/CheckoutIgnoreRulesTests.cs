using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests.Packaging.Journey;

public class CheckoutIgnoreRulesTests
{
    [Theory]
    [InlineData("_repolore/sessions/any-session/root.md", true)]
    [InlineData("_repolore/.history/checkpoints/0000000000000001.json", true)]
    [InlineData("_repolore/sparse-tree/tools/tools.md", false)]
    [InlineData("_repolore/roadmap/roadmap.md", false)]
    public void This_checkout_ignores_local_sessions_and_history_but_not_durable_knowledge(string path, bool expectedIgnored)
    {
        TestSupport.WithTemp(temp =>
        {
            File.Copy(Path.Combine(TestSupport.Repo, ".gitignore"), Path.Combine(temp, ".gitignore"));
            var git = GitRepository.Initialize(temp);

            git.IsIgnored(path).Should().Be(expectedIgnored);
        });
    }
}
