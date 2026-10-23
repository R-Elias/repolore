using System.Text.Json;
using FluentAssertions;
using RepoLore.Cli.Failures;
using RepoLore.Core.Health;
using Xunit;

namespace RepoLore.Cli.Tests;

public class CommandContractTests
{
    public static TheoryData<string, string, string, int, string> NormalCases => new()
    {
        { "version", "empty", "version --json", 0, "version" },
        { "path", "minimal", "path src/ --json", 0, "notes" },
        { "context", "minimal", "context src/ --json", 0, "included" },
        { "tree", "minimal", "tree --json", 0, "entries" },
        { "history", "checkpointed", "history --json", 0, "checkpoints" },
        { "restore --dry-run", "checkpointed", "restore 1 --dry-run --json", 0, "entries" },
        { "migrate --check", "alpha-sparse", "migrate --check --json", 1, "migrationNeeded" },
        { "health-check", "minimal", "health-check --json", 0, "healthy" }
    };

    public static TheoryData<string, string, string, int, string> NormalMutatingCases => new()
    {
        { "checkpoint", "minimal", "checkpoint --json", 0, "noOp" },
        { "init", "empty", "init --json", 0, "created" }
    };

    public static TheoryData<string, string, string, int, string> FailureCases => new()
    {
        { "version", "empty", "version extra --json", 2, "usage" },
        { "path", "minimal", "path --json", 2, "usage" },
        { "context", "minimal", "context --json", 2, "usage" },
        { "tree", "minimal", "tree --start nowhere/ --json", 2, "usage" },
        { "history", "corrupt-history", "history --json", 3, "history-failure" },
        { "restore", "minimal", "restore 99 --dry-run --json", 3, "history-failure" },
        { "checkpoint", "disabled-history", "checkpoint --json", 3, "history-disabled" },
        { "checkpoint pending", "pending", "checkpoint --json", 3, "history-pending" },
        { "init", "alpha-sparse", "init --json", 3, "alpha-needs-migration" },
        { "context unsupported", "unsupported-format", "context src/ --json", 3, "unsupported-format" },
        { "path unsupported", "unsupported-format", "path src/ --json", 3, "unsupported-format" },
        { "checkpoint unsupported", "unsupported-format", "checkpoint --json", 3, "unsupported-format" }
    };

    [Theory]
    [MemberData(nameof(NormalCases))]
    public void Read_only_commands_succeed_with_a_structured_result_and_write_nothing(string command, string arrangement, string arguments, int exitCode, string expectedKey)
    {
        var result = RunArranged(arrangement, arguments, out var writes);

        result.Code.Should().Be(exitCode, command);
        ParseSingleDocument(result.Out).RootElement.TryGetProperty(expectedKey, out _).Should().BeTrue(command);
        writes.Should().BeFalse(command + " wrote files");
    }

    [Theory]
    [MemberData(nameof(NormalMutatingCases))]
    public void Mutating_commands_succeed_with_a_structured_result(string command, string arrangement, string arguments, int exitCode, string expectedKey)
    {
        var result = RunArranged(arrangement, arguments, out _);

        result.Code.Should().Be(exitCode, command);
        ParseSingleDocument(result.Out).RootElement.TryGetProperty(expectedKey, out _).Should().BeTrue(command);
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public void Failures_exit_with_a_machine_readable_error_concise_diagnostics_and_no_writes(string command, string arrangement, string arguments, int exitCode, string expectedErrorId)
    {
        var result = RunArranged(arrangement, arguments, out var writes);

        result.Code.Should().Be(exitCode, command);
        var error = ParseSingleDocument(result.Out).RootElement.GetProperty("error");
        error.GetProperty("id").GetString().Should().Be(expectedErrorId, command);
        error.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        result.Error.Should().Contain("repolore: ").And.NotContain("   at ");
        writes.Should().BeFalse(command + " wrote files");
    }

    [Fact]
    public void An_interrupted_operation_reports_its_recovery_id_in_the_error_result()
    {
        var result = RunArranged("pending", "checkpoint --json", out _);

        ParseSingleDocument(result.Out).RootElement.GetProperty("error").GetProperty("recoveryId").GetInt64().Should().Be(7);
        result.Error.Should().Contain("repolore restore 7");
    }

    [Fact]
    public void Migrate_conflicts_exit_3_with_a_blocked_plan_instead_of_an_error_envelope()
    {
        var result = RunArranged("alpha-conflict", "migrate --check --json", out var writes);

        result.Code.Should().Be(3);
        ParseSingleDocument(result.Out).RootElement.GetProperty("blocked").GetBoolean().Should().BeTrue();
        writes.Should().BeFalse();
    }

    [Fact]
    public void Unsupported_formats_are_refused_by_every_command_that_would_interpret_or_mutate_knowledge()
    {
        foreach (var arguments in new[] { "tree", "restore 1 --dry-run", "migrate --check" })
        {
            var result = RunArranged("unsupported-format", arguments, out var writes);
            result.Code.Should().NotBe(0, arguments);
            writes.Should().BeFalse(arguments);
        }
    }

    [Fact]
    public void Dry_runs_create_no_history_when_absent_and_change_nothing_when_present()
    {
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("alpha-sparse", temp);
            TestSupport.Run(temp, TestSupport.Cli, "migrate", "--dry-run").Code.Should().Be(0);
            TestSupport.Run(temp, TestSupport.Cli, "migrate", "--check").Code.Should().Be(1);
            TestSupport.Run(temp, TestSupport.Cli, "restore", "1", "--dry-run").Code.Should().Be(3);
            Directory.Exists(Path.Combine(temp, "_repolore", ".history")).Should().BeFalse();
        });

        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply("checkpointed", temp);
            var before = TestSupport.Snapshot(temp);
            TestSupport.Run(temp, TestSupport.Cli, "restore", "1", "--dry-run").Code.Should().Be(0);
            TestSupport.Snapshot(temp).Should().Be(before);
        });
    }

    [Theory]
    [InlineData("history --bogus")]
    [InlineData("context src/ --json --json")]
    [InlineData("context src/ --session a --session b")]
    [InlineData("checkpoint --repo-root . --repo-root .")]
    [InlineData("history --dry-run")]
    [InlineData("path src/ --strict")]
    [InlineData("init --check")]
    [InlineData("migrate --update-method")]
    [InlineData("checkpoint extra")]
    [InlineData("context src/ other/")]
    [InlineData("health-check --node _repolore/root.md")]
    public void Unknown_misplaced_or_duplicated_options_and_extra_arguments_are_usage_errors(string arguments)
    {
        var result = RunArranged("minimal", arguments, out var writes);

        result.Code.Should().Be(2);
        result.Error.Should().StartWith("Usage:");
        writes.Should().BeFalse();
    }

    [Fact]
    public void Repeating_node_is_intentional_and_reads_nodes_in_argument_order()
    {
        var result = RunArranged("minimal", "context --node _repolore/root.md --node _repolore/method.md --json", out _);

        result.Code.Should().Be(0);
        ParseSingleDocument(result.Out).RootElement.GetProperty("included").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Help_and_version_work_outside_a_repository()
    {
        TestSupport.WithTemp(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "--help").Code.Should().Be(0);
            TestSupport.Run(temp, TestSupport.Cli, "version").Out.Should().Contain("RepoLore");
            Directory.EnumerateFileSystemEntries(temp).Should().BeEmpty();
        });
    }

    [Fact]
    public void Finding_and_error_ids_match_the_frozen_fixture()
    {
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "expected-ids.tsv"))
            .Skip(1)
            .Select(line => line.Split('\t'))
            .ToList();

        rows.Where(row => row[0] == "finding").Select(row => row[1]).Should().BeEquivalentTo(FindingIds.All);
        rows.Where(row => row[0] == "error").Select(row => row[1]).Should().BeEquivalentTo(ErrorIds.All);

        foreach (var row in rows.Where(row => row[0] == "finding"))
            (FindingIds.IsBlocking(row[1]) ? "3" : "1").Should().Be(row[2], row[1]);
        foreach (var row in rows.Where(row => row[0] == "error"))
            row[2].Should().Be(row[1] == "usage" ? "2" : "3", row[1]);
    }

    private static (int Code, string Out, string Error) RunArranged(string arrangement, string arguments, out bool writes)
    {
        (int Code, string Out, string Error) result = default;
        var wrote = false;
        TestSupport.WithTemp(temp =>
        {
            Arrangements.Apply(arrangement, temp);
            var before = TestSupport.Snapshot(temp);
            result = TestSupport.Run(temp, new[] { TestSupport.Cli }.Concat(arguments.Split(' ')).ToArray());
            wrote = TestSupport.Snapshot(temp) != before;
        });
        writes = wrote;
        return result;
    }

    private static JsonDocument ParseSingleDocument(string stdout) => JsonDocument.Parse(stdout);
}
