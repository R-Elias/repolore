using RepoLore.Core.Configuration;
using RepoLore.Core.Json;
using RepoLore.Core.Matching;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using RepoLore.Core.Tests;

namespace RepoLore.Core.Tests.Restore;

public static class RestorePlannerTests
{
    public static void Run()
    {
        TestRunner.Check("full restore classifies add, replace, delete, and unchanged", () =>
        {
            var target = Target(Array.Empty<string>(),
                ("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_OLD"), ("_repolore/c.md", "C"));
            var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_NEW"), ("_repolore/b.md", "B"));

            var plan = RestorePlanner.Plan(target, current, Config(), null);

            TestRunner.Equal(4, plan.Entries.Count);
            TestRunner.Equal(RestoreAction.Unchanged, Find(plan, "_repolore/root.md").Action);
            TestRunner.Equal(RestoreAction.Replace, Find(plan, "_repolore/a.md").Action);
            TestRunner.Equal(RestoreAction.Delete, Find(plan, "_repolore/b.md").Action);
            TestRunner.Equal(RestoreAction.Add, Find(plan, "_repolore/c.md").Action);
            TestRunner.True(!plan.IsNoOp);
        });

        TestRunner.Check("identical saved and current state is a no-op", () =>
        {
            var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
            var current = Files(("_repolore/root.md", "ROOT"));

            var plan = RestorePlanner.Plan(target, current, Config(), null);

            TestRunner.True(plan.IsNoOp);
            TestRunner.Equal(RestoreAction.Unchanged, Find(plan, "_repolore/root.md").Action);
        });

        TestRunner.Check("a currently-excluded path is never deleted", () =>
        {
            var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
            var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/b.md", "B"));

            var plan = RestorePlanner.Plan(target, current, Config("_repolore/b.md"), null);

            TestRunner.True(plan.IsNoOp);
            TestRunner.Equal(1, plan.Entries.Count);
        });

        TestRunner.Check("a path excluded by saved coverage is never deleted when newly covered", () =>
        {
            var target = Target(new[] { "_repolore/b.md" }, ("_repolore/root.md", "ROOT"));
            var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/b.md", "B"));

            var plan = RestorePlanner.Plan(target, current, Config(), null);

            TestRunner.True(plan.IsNoOp);
            TestRunner.Equal(1, plan.Entries.Count);
        });

        TestRunner.Check("--path selects one file and classifies a replace", () =>
        {
            var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_OLD"));
            var current = Files(("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A_NEW"));

            var plan = RestorePlanner.Plan(target, current, Config(), "_repolore/a.md");

            TestRunner.Equal(1, plan.Entries.Count);
            TestRunner.Equal(RestoreAction.Replace, plan.Entries[0].Action);
            TestRunner.Equal("A_NEW", plan.Entries[0].Before);
            TestRunner.Equal("A_OLD", plan.Entries[0].After);
        });

        TestRunner.Check("--path adds a file absent now", () =>
        {
            var target = Target(Array.Empty<string>(), ("_repolore/a.md", "A"));
            var plan = RestorePlanner.Plan(target, Files(), Config(), "_repolore/a.md");

            TestRunner.Equal(RestoreAction.Add, plan.Entries[0].Action);
        });

        TestRunner.Check("--path deletes a file absent in the snapshot", () =>
        {
            var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
            var plan = RestorePlanner.Plan(target, Files(("_repolore/root.md", "ROOT"), ("_repolore/a.md", "A")), Config(), "_repolore/a.md");

            TestRunner.Equal(RestoreAction.Delete, plan.Entries[0].Action);
        });

        TestRunner.Check("--path absent in both states is an unchanged no-op", () =>
        {
            var target = Target(Array.Empty<string>(), ("_repolore/root.md", "ROOT"));
            var plan = RestorePlanner.Plan(target, Files(("_repolore/root.md", "ROOT")), Config(), "_repolore/a.md");

            TestRunner.True(plan.IsNoOp);
            TestRunner.Equal(RestoreAction.Unchanged, plan.Entries[0].Action);
        });

        TestRunner.Check("--path excluded on the current side fails with a scope explanation", () =>
        {
            var target = Target(Array.Empty<string>(), ("_repolore/a.md", "A"));
            var ex = TestRunner.Capture<RestorePlanException>(() =>
                RestorePlanner.Plan(target, Files(("_repolore/a.md", "A")), Config("_repolore/a.md"), "_repolore/a.md"));
            TestRunner.True(ex.Message.Contains("scope", StringComparison.Ordinal), ex.Message);
        });

        TestRunner.Check("--path excluded on the saved side fails with a scope explanation", () =>
        {
            var target = Target(new[] { "_repolore/a.md" }, ("_repolore/root.md", "ROOT"));
            var ex = TestRunner.Capture<RestorePlanException>(() =>
                RestorePlanner.Plan(target, Files(("_repolore/root.md", "ROOT")), Config(), "_repolore/a.md"));
            TestRunner.True(ex.Message.Contains("saved coverage", StringComparison.Ordinal), ex.Message);
        });
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
