using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests.Scenarios;

// Every finding and error ID in the frozen expected-ids.tsv must be asserted by at least one scenario, either as a JSON
// "...id" value or as a finding line "<id> <path>" in stdout. The few IDs that checking out a commit cannot produce are
// listed below with the reason and the ordinary test that asserts them instead; an exemption must disappear as soon as
// a scenario covers the ID, so the list can only shrink without review.
public sealed class ScenarioCoverageTests
{
    private sealed record Exemption(string Reason, string? TestFile, string? TestMethod);

    private static readonly Dictionary<string, Exemption> Exemptions = new(StringComparer.Ordinal)
    {
        ["history-locked"] = new("needs a second writer holding the lock while the command runs",
            "tests/RepoLore.Cli.Tests/FailureIdTests.cs", "Checkpoint_while_another_writer_holds_the_lock_fails_with_history_locked"),
        ["operation-interrupted"] = new("needs a write to fail in the middle of a restore",
            "tests/RepoLore.Cli.Tests/FailureIdTests.cs", "A_restore_that_cannot_write_a_file_is_interrupted_and_names_its_recovery_id"),
        ["init-incomplete"] = new("needs the first checkpoint to fail after init created its files; only fault injection does that",
            "tests/RepoLore.Infrastructure.Tests/History/InitEngineTests.cs", "Failure_creating_the_first_checkpoint_is_visible_and_retryable"),
        ["migration-blocked"] = new("migrate reports a blocked plan before the engine runs; the engine refusal behind it is defensive",
            "tests/RepoLore.Infrastructure.Tests/History/MigrationEngineTests.cs", "Differing_copies_conflict_and_write_nothing"),
        ["internal-error"] = new("raised only by a defect", null, null),
    };

    [Fact]
    public void Every_finding_and_error_id_is_asserted_by_a_scenario_or_has_a_reviewed_exemption()
    {
        var ids = ExpectedIds();
        var covered = CoveredIds(ids);

        ids.Except(covered).Except(Exemptions.Keys).Should().BeEmpty("each of these IDs needs a scenario that asserts it");
        Exemptions.Keys.Intersect(covered).Should().BeEmpty("an exemption must be removed once a scenario covers its ID");
        Exemptions.Keys.Should().BeSubsetOf(ids);
    }

    [Fact]
    public void Each_exemption_names_an_existing_test_except_the_defect_only_id()
    {
        Exemptions.Where(exemption => exemption.Value.TestFile is null).Select(exemption => exemption.Key).Should().Equal("internal-error");
        foreach (var (id, exemption) in Exemptions.Where(exemption => exemption.Value.TestFile is not null))
            File.ReadAllText(Path.Combine(TestSupport.Repo, exemption.TestFile!)).Should()
                .Contain($"public void {exemption.TestMethod}(", $"the exemption for {id} must point at a real test");
    }

    private static string[] ExpectedIds() =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "expected-ids.tsv"))
            .Skip(1)
            .Select(line => line.Split('\t')[1])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static HashSet<string> CoveredIds(string[] ids)
    {
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expect in ScenarioCatalog.All().SelectMany(scenario => scenario.Steps).Select(step => step.Expect).OfType<Expectation>())
        {
            foreach (var (path, value) in expect.Json)
                if ((path == "id" || path.EndsWith(".id", StringComparison.Ordinal)) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                    covered.Add(value.GetString()!);
            foreach (var text in expect.Stdout)
                foreach (var id in ids.Where(id => text.StartsWith(id + " _repolore", StringComparison.Ordinal)))
                    covered.Add(id);
        }
        return covered;
    }
}
