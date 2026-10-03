using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests;

public class DurableSessionContextTests
{
    [Fact]
    public void Default_durable_context_excludes_both_sessions()
    {
        WithFixture("two-sessions", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", ".");
            result.Code.Should().Be(0);
            var outText = TestSupport.Normalize(result.Out);
            outText.Should().Contain("TWO_SESSIONS_DURABLE_SENTINEL");
            outText.Should().NotContain("SESSION_A");
            outText.Should().NotContain("SESSION_B");
        });
    }

    [Fact]
    public void Selecting_session_A_reads_only_As_root_and_named_nodes()
    {
        WithFixture("two-sessions", temp =>
        {
            var root = TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-a");
            root.Code.Should().Be(0);
            var rootText = TestSupport.Normalize(root.Out);
            rootText.Should().Contain("SESSION_A_ROOT_SENTINEL");
            rootText.Should().NotContain("SESSION_B");
            rootText.Should().NotContain("TWO_SESSIONS_DURABLE");

            var withNode = TestSupport.Run(temp, TestSupport.Cli, "context",
                "--session", "session-a", "--node", "_repolore/sessions/session-a/investigation.md");
            withNode.Code.Should().Be(0);
            var nodeText = TestSupport.Normalize(withNode.Out);
            nodeText.Should().Contain("SESSION_A_ROOT_SENTINEL");
            nodeText.Should().Contain("SESSION_A_DETAIL_SENTINEL");
            nodeText.Should().NotContain("SESSION_B");
        });
    }

    [Fact]
    public void Missing_session_is_a_finding()
    {
        WithFixture("two-sessions", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-missing");
            result.Code.Should().Be(1);
            result.Error.Should().Contain("missing-session");
        });
    }

    [Fact]
    public void Missing_explicit_node_is_a_finding()
    {
        WithFixture("two-sessions", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "--node", "_repolore/does-not-exist.md");
            result.Code.Should().Be(1);
            result.Error.Should().Contain("missing-note");
        });
    }

    [Fact]
    public void Invalid_session_is_a_usage_error_even_with_a_tiny_budget()
    {
        WithFixture("two-sessions", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "bad id!", "--budget-tokens", "1");
            result.Code.Should().Be(2);
            result.Error.Should().Contain("invalid --session");
        });
    }

    [Fact]
    public void Cross_session_node_is_rejected()
    {
        WithFixture("two-sessions", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context",
                "--session", "session-a", "--node", "_repolore/sessions/session-b/investigation.md");
            result.Code.Should().Be(2);
        });
    }

    [Fact]
    public void No_selector_and_non_positive_budget_are_usage_errors()
    {
        WithFixture("two-sessions", temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "context").Code.Should().Be(2);
            TestSupport.Run(temp, TestSupport.Cli, "context", ".", "--budget-tokens", "0").Code.Should().Be(2);
            TestSupport.Run(temp, TestSupport.Cli, "context", ".", "--budget-tokens", "-5").Code.Should().Be(2);
        });
    }

    [Fact]
    public void Duplicate_nodes_appear_once()
    {
        WithFixture("two-sessions", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context",
                "--node", "_repolore/root.md", "--node", "_repolore/root.md");
            result.Code.Should().Be(0);
            Count(TestSupport.Normalize(result.Out), "## _repolore/root.md").Should().Be(1);
        });
    }

    [Fact]
    public void Oversized_ancestor_cannot_hide_a_later_fitting_target_note()
    {
        TestSupport.WithTemp(temp =>
        {
            WriteBudgetFixture(temp);
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50");
            result.Code.Should().Be(0);
            var outText = TestSupport.Normalize(result.Out);
            outText.Should().Contain("TARGET_SENTINEL");
            outText.Should().NotContain("ANCESTOR_MARKER");
            result.Error.Should().Contain("omitted: _repolore/sparse-tree/src/src.md");
        });
    }

    [Fact]
    public void Budget_omissions_with_strict_exit_1_and_keep_the_partial_result()
    {
        TestSupport.WithTemp(temp =>
        {
            WriteBudgetFixture(temp);
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50", "--strict");
            result.Code.Should().Be(1);
            var outText = TestSupport.Normalize(result.Out);
            outText.Should().Contain("TARGET_SENTINEL");
            outText.Should().NotContain("ANCESTOR_MARKER");
            result.Error.Should().Contain("omitted:");
        });
    }

    [Fact]
    public void Alpha_read_support_sparse_only_tree_only_and_conflict()
    {
        WithFixture("alpha-sparse-only", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/");
            result.Code.Should().Be(0);
            TestSupport.Normalize(result.Out).Should().Contain("ALPHA_SPARSE_ONLY_SENTINEL");
        });

        WithFixture("alpha-local-only", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/");
            result.Code.Should().Be(0);
            TestSupport.Normalize(result.Out).Should().Contain("ALPHA_LOCAL_ONLY_SENTINEL");
        });

        WithFixture("alpha-conflict", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "context", "src/");
            result.Code.Should().Be(3);
            result.Error.Should().Contain("conflict");
        });
    }

    [Fact]
    public void Path_lists_durable_note_status()
    {
        WithFixture("minimal-v1", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "path", "src/");
            result.Code.Should().Be(0);
            var outText = TestSupport.Normalize(result.Out);
            outText.Should().Contain("present _repolore/root.md");
            outText.Should().Contain("present _repolore/sparse-tree/src/src.md");
        });
    }

    [Fact]
    public void Tree_lists_durable_knowledge_and_session_ids_only_when_requested()
    {
        WithFixture("two-sessions", temp =>
        {
            var durable = TestSupport.Run(temp, TestSupport.Cli, "tree");
            durable.Code.Should().Be(0);
            var durableText = TestSupport.Normalize(durable.Out);
            durableText.Should().Contain("_repolore/root.md");
            durableText.Should().NotContain("sessions");

            var sessions = TestSupport.Run(temp, TestSupport.Cli, "tree", "--start", "_repolore/sessions/");
            sessions.Code.Should().Be(0);
            var sessionsText = TestSupport.Normalize(sessions.Out);
            sessionsText.Should().Contain("_repolore/sessions/session-a/");
            sessionsText.Should().Contain("_repolore/sessions/session-b/");
            sessionsText.Should().NotContain("root.md");

            var inside = TestSupport.Run(temp, TestSupport.Cli, "tree", "--start", "_repolore/sessions/session-a/");
            inside.Code.Should().Be(0);
            var insideText = TestSupport.Normalize(inside.Out);
            insideText.Should().Contain("_repolore/sessions/session-a/root.md");
            insideText.Should().Contain("_repolore/sessions/session-a/investigation.md");
        });
    }

    [Fact]
    public void Json_and_plain_context_render_the_same_logical_sets()
    {
        TestSupport.WithTemp(temp =>
        {
            WriteBudgetFixture(temp);
            var plain = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50");
            var json = TestSupport.Run(temp, TestSupport.Cli, "context", "src/sub/", "--budget-tokens", "50", "--json");
            json.Code.Should().Be(plain.Code);
            var plainText = TestSupport.Normalize(plain.Out);
            var jsonText = TestSupport.Normalize(json.Out);
            jsonText.Should().Contain("TARGET_SENTINEL");
            jsonText.Should().NotContain("ANCESTOR_MARKER");
            plainText.Contains("TARGET_SENTINEL", StringComparison.Ordinal).Should().Be(jsonText.Contains("TARGET_SENTINEL", StringComparison.Ordinal));
            jsonText.Should().Contain("omitted");
        });
    }

    [Fact]
    public void Read_only_commands_leave_repository_and_history_files_unchanged()
    {
        WithFixture("two-sessions", temp =>
        {
            var before = TestSupport.Snapshot(temp);
            TestSupport.Run(temp, TestSupport.Cli, "context", ".");
            TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-a");
            TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-b", "--node", "_repolore/sessions/session-b/investigation.md");
            TestSupport.Run(temp, TestSupport.Cli, "path", ".");
            TestSupport.Run(temp, TestSupport.Cli, "tree");
            TestSupport.Run(temp, TestSupport.Cli, "tree", "--start", "_repolore/sessions/");
            TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "session-missing");
            TestSupport.Snapshot(temp).Should().Be(before);
        });
    }

    private static void WriteBudgetFixture(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "_repolore", "sparse-tree", "src", "sub"));
        File.WriteAllText(Path.Combine(root, "_repolore", "root.md"), "ROOT\n");
        File.WriteAllText(Path.Combine(root, "_repolore", "sparse-tree", "src", "src.md"), "ANCESTOR_MARKER" + new string('x', 500));
        File.WriteAllText(Path.Combine(root, "_repolore", "sparse-tree", "src", "sub", "sub.md"), "TARGET_SENTINEL\n");
    }

    private static void WithFixture(string name, Action<string> action) => TestSupport.WithFixture(name, action);

    private static int Count(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
