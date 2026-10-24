using FluentAssertions;
using RepoLore.Core.Configuration;
using RepoLore.Core.Json;
using RepoLore.Core.Matching;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using Xunit;

namespace RepoLore.Core.Tests.Restore;

public class RestorePlannerTests
{
    [Fact]
    public void Full_restore_classifies_add_replace_delete_and_unchanged()
    {
        var target = Target(Array.Empty<string>(),
            ("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_OLD"), ("_repolore/c.md", "C"));
        var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_NEW"), ("_repolore/b.md", "B"));

        var plan = RestorePlanner.Plan(target, current, Config(), null);

        plan.Entries.Should().HaveCount(4);
        Find(plan, "_repolore/root.md").Action.Should().Be(RestoreAction.Unchanged);
        Find(plan, "_repolore/a.md").Action.Should().Be(RestoreAction.Replace);
        Find(plan, "_repolore/b.md").Action.Should().Be(RestoreAction.Delete);
        Find(plan, "_repolore/c.md").Action.Should().Be(RestoreAction.Add);
        plan.IsNoOp.Should().BeFalse();
    }

    [Fact]
    public void Identical_saved_and_current_state_is_a_no_op()
    {
        var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
        var current = Files(("_repolore/root.md", "ROOT"));

        var plan = RestorePlanner.Plan(target, current, Config(), null);

        plan.IsNoOp.Should().BeTrue();
        Find(plan, "_repolore/root.md").Action.Should().Be(RestoreAction.Unchanged);
    }

    [Fact]
    public void A_currently_excluded_path_is_never_deleted()
    {
        var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
        var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/b.md", "B"));

        var plan = RestorePlanner.Plan(target, current, Config("_repolore/b.md"), null);

        plan.IsNoOp.Should().BeTrue();
        plan.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void A_path_excluded_by_saved_coverage_is_never_deleted_when_newly_covered()
    {
        var target = Target(new[] { "_repolore/b.md" }, ("_repolore/root.md", "ROOT"));
        var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/b.md", "B"));

        var plan = RestorePlanner.Plan(target, current, Config(), null);

        plan.IsNoOp.Should().BeTrue();
        plan.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void Path_selector_selects_one_file_and_classifies_a_replace()
    {
        var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_OLD"));
        var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_NEW"));

        var plan = RestorePlanner.Plan(target, current, Config(), "_repolore/a.md");

        plan.Entries.Should().HaveCount(1);
        plan.Entries[0].Action.Should().Be(RestoreAction.Replace);
        plan.Entries[0].Before.Should().Be("A_NEW");
        plan.Entries[0].After.Should().Be("A_OLD");
    }

    [Fact]
    public void Path_selector_adds_a_file_absent_now()
    {
        var target = Target(Array.Empty<string>(), ("_repolore/a.md", "A"));
        var plan = RestorePlanner.Plan(target, Files(), Config(), "_repolore/a.md");

        plan.Entries[0].Action.Should().Be(RestoreAction.Add);
    }

    [Fact]
    public void Path_selector_deletes_a_file_absent_in_the_snapshot()
    {
        var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
        var plan = RestorePlanner.Plan(target, Files(("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A")), Config(), "_repolore/a.md");

        plan.Entries[0].Action.Should().Be(RestoreAction.Delete);
    }

    [Fact]
    public void Path_selector_absent_in_both_states_is_an_unchanged_no_op()
    {
        var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
        var plan = RestorePlanner.Plan(target, Files(("_repolore/root.md", "ROOT")), Config(), "_repolore/a.md");

        plan.IsNoOp.Should().BeTrue();
        plan.Entries[0].Action.Should().Be(RestoreAction.Unchanged);
    }

    [Fact]
    public void Path_selector_excluded_on_the_current_side_fails_with_a_scope_explanation()
    {
        var target = Target(Array.Empty<string>(), ("_repolore/a.md", "A"));
        var ex = new Action(() =>
            RestorePlanner.Plan(target, Files(("_repolore/a.md", "A")), Config("_repolore/a.md"), "_repolore/a.md"))
            .Should().Throw<RestorePlanException>().Which;
        ex.Message.Should().Contain("scope");
    }

    [Fact]
    public void Path_selector_excluded_on_the_saved_side_fails_with_a_scope_explanation()
    {
        var target = Target(new[] { "_repolore/a.md" }, ("_repolore/root.md", "ROOT"));
        var ex = new Action(() =>
            RestorePlanner.Plan(target, Files(("_repolore/root.md", "ROOT")), Config(), "_repolore/a.md"))
            .Should().Throw<RestorePlanException>().Which;
        ex.Message.Should().Contain("saved coverage");
    }

    private static RestorePlanEntry Find(RestorePlan plan, string path)
    {
        foreach (var entry in plan.Entries)
            if (entry.Path == path)
                return entry;
        throw new InvalidOperationException("missing plan entry " + path);
    }

    private static RepoLoreConfig Config(params string[] exclude) =>
        new(1, true, 209715200, RuleSet.Compile(exclude), exclude, new JsonObject());

    private static IReadOnlyList<FileEntry> Files(params (string Path, string Hash)[] entries)
    {
        var list = new List<FileEntry>(entries.Length);
        foreach (var (path, hash) in entries)
            list.Add(new FileEntry(path, hash, hash.Length));
        list.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return list;
    }

    private static CheckpointManifest Target(IReadOnlyList<string> savedExclude, params (string Path, string Hash)[] files)
    {
        var entries = new List<FileEntry>(files.Length);
        foreach (var (path, hash) in files)
            entries.Add(new FileEntry(path, hash, hash.Length));
        entries.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        var scope = new CaptureScope(true, 209715200, savedExclude, CaptureScope.RepoLoreJsonPresent);
        return new CheckpointManifest(1, "t", HistoryVersion.Current, 1, scope, entries);
    }
}
