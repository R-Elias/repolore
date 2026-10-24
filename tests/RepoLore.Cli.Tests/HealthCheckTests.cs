using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests;

public class HealthCheckTests
{
    [Fact]
    public void A_healthy_knowledge_base_exits_0_in_text_and_json()
    {
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("minimal", temp);
            var text = TestSupport.Run(temp, TestSupport.Cli, "health-check");
            text.Code.Should().Be(0);
            text.Out.Should().Contain("healthy");
            TestSupport.Run(temp, TestSupport.Cli, "health-check", "--quiet").Out.Should().BeEmpty();
        });
    }

    [Fact]
    public void Conflicting_alpha_copies_and_a_missing_marker_are_reported_with_exit_3()
    {
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("alpha-conflict", temp);
            var before = TestSupport.Snapshot(temp);

            var result = TestSupport.Run(temp, TestSupport.Cli, "health-check", "--json");

            result.Code.Should().Be(3);
            var ids = FindingIds(result.Out);
            ids.Should().Contain("conflicting-alpha-notes").And.Contain("migration-needed");
            TestSupport.Snapshot(temp).Should().Be(before);
        });
    }

    [Fact]
    public void A_clean_alpha_clone_needs_migration_with_exit_1()
    {
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("alpha-sparse", temp);
            Arrangements.AddSourceDirectory(temp);

            var result = TestSupport.Run(temp, TestSupport.Cli, "health-check", "--json");

            result.Code.Should().Be(1);
            FindingIds(result.Out).Should().Contain("migration-needed");
        });
    }

    [Fact]
    public void Default_checks_never_read_session_contents_and_an_explicit_session_checks_only_itself()
    {
        TestSupport.WithFixture("two-sessions", temp =>
        {
            var sessionB = Path.Combine(temp, "_repolore", "sessions", "session-b");
            File.WriteAllText(Path.Combine(sessionB, "root.md"), "[gone](missing.md)");
            File.WriteAllBytes(Path.Combine(sessionB, "investigation.md"), new byte[] { 0xFF, 0xFE });
            var before = TestSupport.Snapshot(temp);

            TestSupport.Run(temp, TestSupport.Cli, "health-check").Code.Should().Be(0);
            TestSupport.Run(temp, TestSupport.Cli, "health-check", "--session", "session-a").Code.Should().Be(0);

            var selected = TestSupport.Run(temp, TestSupport.Cli, "health-check", "--session", "session-b", "--json");
            selected.Code.Should().Be(1);
            FindingIds(selected.Out).Should().BeEquivalentTo("broken-link", "invalid-utf8");
            TestSupport.Snapshot(temp).Should().Be(before);
        });
    }

    [Fact]
    public void A_missing_session_is_a_finding_and_an_invalid_session_id_is_a_usage_error()
    {
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("minimal", temp);
            var missing = TestSupport.Run(temp, TestSupport.Cli, "health-check", "--session", "absent", "--json");
            missing.Code.Should().Be(1);
            FindingIds(missing.Out).Should().Equal("invalid-session");

            TestSupport.Run(temp, TestSupport.Cli, "health-check", "--session", "../escape").Code.Should().Be(2);
        });
    }

    [Fact]
    public void Explain_reports_repolore_decisions_and_leaves_git_status_to_git()
    {
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("minimal", temp);

            var json = TestSupport.Run(temp, TestSupport.Cli, "health-check", "--explain", "src/build/out.txt", "--json");
            json.Code.Should().Be(0);
            var explain = JsonDocument.Parse(json.Out).RootElement.GetProperty("explain");
            explain.GetProperty("sourceDiscovery").GetProperty("layer").GetString().Should().Be("default");
            explain.GetProperty("sourceDiscovery").GetProperty("rule").GetString().Should().Be("**/build/");
            explain.GetProperty("historyCoverage").GetProperty("covered").GetBoolean().Should().BeFalse();
            explain.GetProperty("gitTracking").GetString().Should().Be("not evaluated");

            var text = TestSupport.Run(temp, TestSupport.Cli, "health-check", "--explain", "_repolore/sessions/a/root.md");
            text.Out.Should().Contain("hard-exclusion rule '_repolore/'").And.Contain("git tracking: not evaluated");

            TestSupport.Run(temp, TestSupport.Cli, "health-check", "--explain", "../outside").Code.Should().Be(2);
        });
    }

    [Fact]
    public void A_pending_operation_is_reported_as_history_trouble_with_exit_3()
    {
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("pending", temp);

            var result = TestSupport.Run(temp, TestSupport.Cli, "health-check", "--json");

            result.Code.Should().Be(3);
            FindingIds(result.Out).Should().Contain("history-pending");
        });
    }

    private static string[] FindingIds(string stdout) =>
        JsonDocument.Parse(stdout).RootElement.GetProperty("findings").EnumerateArray()
            .Select(finding => finding.GetProperty("id").GetString()!).ToArray();
}
