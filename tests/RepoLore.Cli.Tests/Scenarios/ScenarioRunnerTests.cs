using FluentAssertions;
using RepoLore.Cli.Tests.Packaging.Journey;
using Xunit;

namespace RepoLore.Cli.Tests.Scenarios;

// Self-tests of the scenario runner, run in ordinary CI: a tiny two-stage Git repository stands in for the reference
// repository and a fake tool stands in for the installed package, so a runner that cannot fail is caught here.
public sealed class ScenarioRunnerTests : IDisposable
{
    private const string Note = "_repolore/note.md";

    private readonly string root = Path.Combine(Path.GetTempPath(), "repolore-scenario-runner-" + Guid.NewGuid().ToString("N"));
    private readonly string reference;

    public ScenarioRunnerTests()
    {
        reference = Path.Combine(root, "reference");
        Directory.CreateDirectory(Path.Combine(reference, "_repolore"));
        var git = GitRepository.Initialize(reference);
        File.WriteAllText(Path.Combine(reference, ".gitignore"), "_repolore/.history/\n");
        File.WriteAllText(Path.Combine(reference, Note), "STAGE-A\n");
        git.CommitAll("stage a");
        git.Run("tag", "stage-a");
        File.WriteAllText(Path.Combine(reference, Note), "STAGE-B\n");
        git.CommitAll("stage b");
        git.Run("tag", "stage-b");
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Satisfied_expectations_of_every_kind_pass_and_the_log_records_each_command()
    {
        var outcome = Run(
            """
            { "name": "all-kinds", "steps": [
              { "checkout": "stage-b" },
              { "run": "echo {\"error\":{\"id\":\"x\"},\"items\":[1,2]}", "expect": {
                "exit": 0, "stdout": "\"id\"", "stdout-not": "absent", "stderr": "warned", "stderr-not": "fatal",
                "json": { "error.id": "x", "items.1": 2 },
                "file-equals": { "path": "_repolore/note.md", "at": "stage-b" },
                "file-absent": "_repolore/repolore.json" } }
            ] }
            """,
            FakeTool);

        outcome.Failures.Should().BeEmpty();
        outcome.Log.Should().Contain("$ checkout stage-b").And.Contain("$ repolore echo").And.Contain("[exit 0]");
    }

    [Theory]
    [InlineData("\"exit\": 1", "exit: expected 1, got 0")]
    [InlineData("\"exit\": 0, \"stdout\": \"never printed\"", "stdout: missing 'never printed'")]
    [InlineData("\"exit\": 0, \"stdout-not\": \"hello\"", "stdout-not: found 'hello'")]
    [InlineData("\"exit\": 0, \"stderr\": \"never printed\"", "stderr: missing 'never printed'")]
    [InlineData("\"exit\": 0, \"stderr-not\": \"warned\"", "stderr-not: found 'warned'")]
    [InlineData("\"exit\": 0, \"json\": { \"a\": 1 }", "json: stdout is not one JSON document")]
    [InlineData("\"exit\": 0, \"file-equals\": { \"path\": \"_repolore/note.md\", \"at\": \"stage-a\" }", "file-equals: '_repolore/note.md' differs from its content at stage-a")]
    [InlineData("\"exit\": 0, \"file-absent\": \"_repolore/note.md\"", "file-absent: '_repolore/note.md' exists")]
    public void Each_unmet_expectation_fails_the_scenario_and_names_the_step(string expect, string failure)
    {
        var outcome = Run($$"""{ "name": "unmet", "steps": [ { "checkout": "stage-b" }, { "run": "echo hello", "expect": { {{expect}} } } ] }""", FakeTool);

        outcome.Passed.Should().BeFalse();
        outcome.Failures.Should().ContainSingle().Which.Should().Be("step 2 (repolore echo hello): " + failure);
    }

    [Fact]
    public void Json_values_are_compared_by_value_and_missing_paths_are_reported()
    {
        var outcome = Run(
            """
            { "name": "json", "steps": [ { "checkout": "stage-b" },
              { "run": "echo {\"id\":3,\"items\":[]}", "expect": { "exit": 0, "json": { "id": "3", "items.0": 1, "missing": null } } } ] }
            """,
            FakeTool);

        outcome.Failures.Should().Equal(
            "step 2 (repolore echo {\"id\":3,\"items\":[]}): json: 'id' expected \"3\", got 3",
            "step 2 (repolore echo {\"id\":3,\"items\":[]}): json: 'items.0' is absent",
            "step 2 (repolore echo {\"id\":3,\"items\":[]}): json: 'missing' is absent");
    }

    [Theory]
    [InlineData("health-check")]
    [InlineData("context src/")]
    [InlineData("restore 1 --dry-run")]
    [InlineData("migrate --check")]
    public void A_read_only_command_that_writes_anything_fails_the_scenario(string command)
    {
        var outcome = Run(
            $$"""{ "name": "writes", "steps": [ { "checkout": "stage-b" }, { "run": "{{command}}", "expect": { "exit": 0 } } ] }""",
            (repository, _) =>
            {
                Directory.CreateDirectory(Path.Combine(repository, "_repolore", ".history"));
                File.WriteAllText(Path.Combine(repository, "_repolore", ".history", "write.lock"), string.Empty);
                return (0, string.Empty, string.Empty);
            });

        outcome.Failures.Should().ContainSingle().Which.Should()
            .Contain("a read-only command changed the working tree or the history: + D _repolore/.history; + F _repolore/.history/write.lock ");
    }

    [Fact]
    public void A_read_only_command_that_rewrites_a_file_with_identical_bytes_is_still_caught()
    {
        var outcome = Run(
            """{ "name": "touches", "steps": [ { "checkout": "stage-b" }, { "run": "health-check", "expect": { "exit": 0 } } ] }""",
            (repository, _) =>
            {
                var note = Path.Combine(repository, Note);
                File.WriteAllBytes(note, File.ReadAllBytes(note));
                File.SetLastWriteTimeUtc(note, File.GetLastWriteTimeUtc(note).AddMinutes(1));
                return (0, string.Empty, string.Empty);
            });

        outcome.Failures.Should().ContainSingle().Which.Should().Contain("- F _repolore/note.md ").And.Contain("+ F _repolore/note.md ");
    }

    [Fact]
    public void A_write_command_may_change_the_tree()
    {
        var outcome = Run(
            """{ "name": "writes", "steps": [ { "checkout": "stage-b" }, { "run": "checkpoint", "expect": { "exit": 0 } } ] }""",
            (repository, _) =>
            {
                File.WriteAllText(Path.Combine(repository, Note), "CHANGED\n");
                return (0, string.Empty, string.Empty);
            });

        outcome.Failures.Should().BeEmpty();
    }

    [Fact]
    public void Checkout_resets_tracked_files_removes_untracked_ones_and_keeps_ignored_history()
    {
        var seen = new List<string>();
        var outcome = Run(
            """
            { "name": "checkout", "steps": [
              { "checkout": "stage-b" },
              { "run": "checkpoint", "expect": { "exit": 0 } },
              { "checkout": "stage-a" },
              { "run": "version", "expect": { "exit": 0 } } ] }
            """,
            (repository, arguments) =>
            {
                if (arguments[0] == "checkpoint")
                {
                    Directory.CreateDirectory(Path.Combine(repository, "_repolore", ".history"));
                    File.WriteAllText(Path.Combine(repository, "_repolore", ".history", "kept"), "x");
                    File.WriteAllText(Path.Combine(repository, "stray.md"), "x");
                    File.WriteAllText(Path.Combine(repository, Note), "EDITED\n");
                }
                else
                {
                    seen.Add(File.ReadAllText(Path.Combine(repository, Note)));
                    seen.Add(File.Exists(Path.Combine(repository, "_repolore", ".history", "kept")) ? "history kept" : "history lost");
                    seen.Add(File.Exists(Path.Combine(repository, "stray.md")) ? "stray kept" : "stray removed");
                }
                return (0, string.Empty, string.Empty);
            });

        outcome.Failures.Should().BeEmpty();
        seen.Should().Equal("STAGE-A\n", "history kept", "stray removed");
    }

    [Fact]
    public void The_first_failing_step_ends_the_scenario()
    {
        var runs = 0;
        var outcome = Run(
            """
            { "name": "stops", "steps": [ { "checkout": "stage-b" },
              { "run": "checkpoint", "expect": { "exit": 0 } },
              { "run": "checkpoint", "expect": { "exit": 0 } } ] }
            """,
            (_, _) => (++runs == 1 ? 3 : 0, string.Empty, string.Empty));

        outcome.Failures.Should().ContainSingle().Which.Should().StartWith("step 2 ");
        runs.Should().Be(1);
    }

    [Fact]
    public void An_unknown_stage_fails_the_scenario_with_a_clear_message_and_runs_nothing()
    {
        var runs = 0;
        var outcome = Run(
            """{ "name": "unknown", "steps": [ { "checkout": "s99-missing" }, { "run": "checkpoint", "expect": { "exit": 0 } } ] }""",
            (_, _) => (++runs, string.Empty, string.Empty));

        outcome.Failures.Should().Equal(
            "step 1 (checkout s99-missing): unknown stage 's99-missing' in the reference repository; check the tag or move the pin forward");
        runs.Should().Be(0);
    }

    [Fact]
    public void Selection_keeps_only_this_operating_system_and_the_requested_tags()
    {
        var linuxOnly = ScenarioFile.Parse("linux-only", """{ "name": "linux-only", "os": ["linux"], "tags": ["hazards"], "steps": [ { "checkout": "a" } ] }""");
        var everywhere = ScenarioFile.Parse("everywhere", """{ "name": "everywhere", "tags": ["smoke"], "steps": [ { "checkout": "a" } ] }""");
        Scenario[] all = [linuxOnly, everywhere];

        ScenarioCatalog.Select(all, "windows", "").Select(scenario => scenario.Name).Should().Equal("everywhere");
        ScenarioCatalog.Select(all, "linux", "").Select(scenario => scenario.Name).Should().Equal("linux-only", "everywhere");
        ScenarioCatalog.Select(all, "linux", "smoke").Select(scenario => scenario.Name).Should().Equal("everywhere");
        ScenarioCatalog.Select(all, "linux", " hazards , other").Select(scenario => scenario.Name).Should().Equal("linux-only");
    }

    [Fact]
    public void The_reference_repository_is_never_written()
    {
        var before = TreeSnapshot.Take(reference);

        Run("""{ "name": "pristine", "steps": [ { "checkout": "stage-a" }, { "run": "checkpoint", "expect": { "exit": 0 } } ] }""",
            (repository, _) =>
            {
                File.WriteAllText(Path.Combine(repository, Note), "EDITED\n");
                return (0, string.Empty, string.Empty);
            });

        TreeSnapshot.Take(reference).Should().Be(before);
    }

    [Theory]
    [InlineData("""{ "name": "bad", "steps": [ { "checkout": "a" } ], "extra": 1 }""", "scenario: unknown key 'extra'")]
    [InlineData("""{ "name": "other", "steps": [ { "checkout": "a" } ] }""", "scenario name 'other' must equal its file name 'bad'")]
    [InlineData("""{ "name": "bad", "steps": [] }""", "'steps' must be a non-empty array")]
    [InlineData("""{ "name": "bad", "steps": [ { "run": "tree", "expect": { "exit": 0 } } ] }""", "the first step must be a checkout")]
    [InlineData("""{ "name": "bad", "steps": [ { "checkout": "a" }, { "run": "tree" } ] }""", "step 2: a run step needs 'expect' with at least 'exit'")]
    [InlineData("""{ "name": "bad", "steps": [ { "checkout": "a" }, { "run": "tree", "expect": { "stdout": "x" } } ] }""", "step 2: 'expect.exit' is required and must be an integer")]
    [InlineData("""{ "name": "bad", "steps": [ { "checkout": "a" }, { "run": "tree", "expect": { "exit": 0, "stdout_not": "x" } } ] }""", "step 2 expect: unknown key 'stdout_not'")]
    [InlineData("""{ "name": "bad", "steps": [ { "checkout": "a", "run": "tree" } ] }""", "step 1: unknown key 'run'")]
    [InlineData("""{ "name": "bad", "os": ["solaris"], "steps": [ { "checkout": "a" } ] }""", "unknown os 'solaris'; use windows, linux, macos")]
    public void Malformed_scenario_files_are_rejected_with_a_precise_message(string text, string message)
    {
        var parse = () => ScenarioFile.Parse("bad", text);

        parse.Should().Throw<FormatException>().WithMessage(message);
    }

    [Fact]
    public void Comments_trailing_commas_argument_arrays_and_default_operating_systems_are_accepted()
    {
        var scenario = ScenarioFile.Parse("ok",
            """
            // a comment
            { "name": "ok", "steps": [ { "checkout": "a" }, { "run": ["context", "docs/Design Docs/"], "expect": { "exit": 0, }, }, ], }
            """);

        scenario.Os.Should().Equal("windows", "linux", "macos");
        scenario.Steps[1].Run.Should().Equal("context", "docs/Design Docs/");
    }

    [Fact]
    public void Every_scenario_in_the_catalog_loads_and_uses_only_stage_tags()
    {
        var scenarios = ScenarioCatalog.All();

        scenarios.Should().NotBeEmpty();
        scenarios.SelectMany(scenario => scenario.Steps).Where(step => step.Checkout is not null)
            .Should().OnlyContain(step => System.Text.RegularExpressions.Regex.IsMatch(step.Checkout!, "^s[0-9]{2}-[a-z0-9-]+$"));
        scenarios.Where(scenario => scenario.Tags.Contains("smoke")).Should().HaveCountGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void The_pin_names_the_repository_a_stage_tag_and_a_full_commit()
    {
        var pin = ReferencePin.Load();

        pin.Repository.Should().Be("https://github.com/R-Elias/repolore-e2e-test-reference.git");
        pin.Tag.Should().MatchRegex("^s[0-9]{2}-");
        pin.Commit.Should().MatchRegex("^[0-9a-f]{40}$");
        var malformed = () => ReferencePin.Parse(["repository x", "tag s01-a", "commit abc"]);
        malformed.Should().Throw<FormatException>().WithMessage("*full 40-character hash*");
    }

    private ScenarioOutcome Run(string scenarioText, Func<string, string[], (int Code, string Out, string Error)> tool)
    {
        var scenario = ScenarioFile.Parse(System.Text.Json.JsonDocument.Parse(scenarioText).RootElement.GetProperty("name").GetString()!, scenarioText);
        var work = Path.Combine(root, "work-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        return new ScenarioRunner(tool).Run(scenario, reference, work);
    }

    // "echo <text>" prints the rest of the arguments on stdout and "warned" on stderr; anything else succeeds silently.
    private static (int Code, string Out, string Error) FakeTool(string repository, string[] arguments) =>
        arguments[0] == "echo" ? (0, string.Join(' ', arguments.Skip(1)) + "\n", "warned\n") : (0, string.Empty, string.Empty);
}
