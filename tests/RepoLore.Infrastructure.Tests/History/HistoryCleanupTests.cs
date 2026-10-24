using System.Text;
using FluentAssertions;
using RepoLore.Core.Configuration;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Infrastructure.Tests.History;

public class HistoryCleanupTests
{
    private static readonly CaptureScope Scope = new(true, 100, new List<string>(), CaptureScope.RepoLoreJsonPresent);

    [Fact]
    public void Evicts_the_oldest_manifest_first_and_reclaims_its_object()
    {
        TempDir.WithTemp(temp =>
        {
            var history = Path.Combine(temp, ".history");
            var objects = new ObjectStore(history);
            var checkpoints = new CheckpointStore(history);
            objects.EnsureDirectories();
            checkpoints.EnsureDirectory();

            var h1 = objects.Store(Encoding.UTF8.GetBytes("a"));
            var h2 = objects.Store(Encoding.UTF8.GetBytes("bb"));
            var h3 = objects.Store(Encoding.UTF8.GetBytes("ccc"));

            checkpoints.Publish(Manifest(1, ("a.md", h1, 1)));
            checkpoints.Publish(Manifest(2, ("b.md", h2, 2)));
            checkpoints.Publish(Manifest(3, ("c.md", h3, 3)));

            var mb1 = HistoryCleanup.SnapshotBytes(Manifest(1, ("a.md", h1, 1))) - 1;
            long total = HistoryCleanup.SnapshotBytes(Manifest(1, ("a.md", h1, 1)))
                + HistoryCleanup.SnapshotBytes(Manifest(2, ("b.md", h2, 2)))
                + HistoryCleanup.SnapshotBytes(Manifest(3, ("c.md", h3, 3)));

            var result = new HistoryCleanup(objects, checkpoints).Clean(total - (mb1 + 1));

            result.Evicted.Should().Be(1);
            result.BudgetOk.Should().BeTrue();
            File.Exists(checkpoints.ManifestPath(1)).Should().BeFalse();
            File.Exists(checkpoints.ManifestPath(2)).Should().BeTrue();
            File.Exists(checkpoints.ManifestPath(3)).Should().BeTrue();
            File.Exists(Path.Combine(objects.ObjectsDirectory, h1)).Should().BeFalse();
            File.Exists(Path.Combine(objects.ObjectsDirectory, h2)).Should().BeTrue();
            File.Exists(Path.Combine(objects.ObjectsDirectory, h3)).Should().BeTrue();
        });
    }

    [Fact]
    public void A_shared_object_survives_while_any_manifest_references_it()
    {
        TempDir.WithTemp(temp =>
        {
            var history = Path.Combine(temp, ".history");
            var objects = new ObjectStore(history);
            var checkpoints = new CheckpointStore(history);
            objects.EnsureDirectories();
            checkpoints.EnsureDirectory();

            var shared = objects.Store(Encoding.UTF8.GetBytes("S"));
            var only1 = objects.Store(Encoding.UTF8.GetBytes("x"));
            var only2 = objects.Store(Encoding.UTF8.GetBytes("y"));

            var m1 = Manifest(1, ("a.md", shared, 1), ("b.md", only1, 1));
            var m2 = Manifest(2, ("a.md", shared, 1), ("c.md", only2, 1));
            checkpoints.Publish(m1);
            checkpoints.Publish(m2);

            long total = HistoryCleanup.SnapshotBytes(m1) + HistoryCleanup.SnapshotBytes(m2) - 1;
            long mb1 = HistoryCleanup.SnapshotBytes(m1) - 2;

            var result = new HistoryCleanup(objects, checkpoints).Clean(total - (mb1 + 1));

            result.Evicted.Should().Be(1);
            File.Exists(checkpoints.ManifestPath(1)).Should().BeFalse();
            File.Exists(checkpoints.ManifestPath(2)).Should().BeTrue();
            File.Exists(Path.Combine(objects.ObjectsDirectory, shared)).Should().BeTrue();
            File.Exists(Path.Combine(objects.ObjectsDirectory, only1)).Should().BeFalse();
            File.Exists(Path.Combine(objects.ObjectsDirectory, only2)).Should().BeTrue();
        });
    }

    [Fact]
    public void The_latest_manifest_is_never_evicted()
    {
        TempDir.WithTemp(temp =>
        {
            var history = Path.Combine(temp, ".history");
            var objects = new ObjectStore(history);
            var checkpoints = new CheckpointStore(history);
            objects.EnsureDirectories();
            checkpoints.EnsureDirectory();

            var h1 = objects.Store(Encoding.UTF8.GetBytes("aa"));
            var h2 = objects.Store(Encoding.UTF8.GetBytes("bb"));
            checkpoints.Publish(Manifest(1, ("a.md", h1, 2)));
            checkpoints.Publish(Manifest(2, ("b.md", h2, 2)));

            var result = new HistoryCleanup(objects, checkpoints).Clean(1);

            File.Exists(checkpoints.ManifestPath(1)).Should().BeFalse();
            File.Exists(checkpoints.ManifestPath(2)).Should().BeTrue();
            result.BudgetOk.Should().BeFalse();
        });
    }

    [Fact]
    public void A_failed_delete_reports_budget_not_achieved_and_keeps_history_valid()
    {
        TempDir.WithTemp(temp =>
        {
            var history = Path.Combine(temp, ".history");
            var objects = new ObjectStore(history);
            var checkpoints = new CheckpointStore(history);
            objects.EnsureDirectories();
            checkpoints.EnsureDirectory();

            var h1 = objects.Store(Encoding.UTF8.GetBytes("a"));
            var h2 = objects.Store(Encoding.UTF8.GetBytes("bb"));
            var m1 = Manifest(1, ("a.md", h1, 1));
            var m2 = Manifest(2, ("b.md", h2, 2));
            checkpoints.Publish(m1);
            checkpoints.Publish(m2);

            var mb1 = HistoryCleanup.SnapshotBytes(m1) - 1;
            long total = HistoryCleanup.SnapshotBytes(m1) + HistoryCleanup.SnapshotBytes(m2);
            var failOn = Path.Combine(objects.ObjectsDirectory, h1);

            var cleanup = new HistoryCleanup(objects, checkpoints, path =>
            {
                if (path == failOn)
                    return false;
                File.Delete(path);
                return true;
            });

            var result = cleanup.Clean(total - (mb1 + 1));

            result.Evicted.Should().Be(1);
            result.BudgetOk.Should().BeFalse();
            File.Exists(failOn).Should().BeTrue();
            File.Exists(checkpoints.ManifestPath(1)).Should().BeFalse();
            checkpoints.ReadHighestCompleted()!.Id.Should().Be(2L);
        });
    }

    private static CheckpointManifest Manifest(long id, params (string Path, string Hash, long Size)[] files)
    {
        var entries = new List<FileEntry>(files.Length);
        foreach (var (path, hash, size) in files)
            entries.Add(new FileEntry(path, hash, size));
        return new CheckpointManifest(id, "t", HistoryVersion.Current, 1, Scope, entries);
    }
}

public class RetentionEngineTests
{
    [Fact]
    public void A_checkpoint_exactly_at_budget_succeeds_and_evicts_the_older_one()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            engine.Capture(Load(temp));

            var s1 = RetainedBytes(temp);
            File.WriteAllText(Root(temp), "B");
            SetMaxBytes(temp, s1);

            var result = engine.Capture(Load(temp));

            result.WasNoOp.Should().BeFalse();
            result.PublishedId.Should().Be(2L);
            HighestId(temp).Should().Be(2L);
            File.Exists(ManifestPath(temp, 1)).Should().BeFalse();
            File.Exists(ManifestPath(temp, 2)).Should().BeTrue();
            result.Cleanup.BudgetOk.Should().BeTrue();
            RetainedBytes(temp).Should().Be(s1);
        });
    }

    [Fact]
    public void A_checkpoint_one_byte_over_budget_fails_and_leaves_prior_state_usable()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            engine.Capture(Load(temp));

            var s1 = RetainedBytes(temp);
            File.WriteAllText(Root(temp), "AB");
            SetMaxBytes(temp, s1);

            var ex = new Action(() => engine.Capture(Load(temp))).Should().Throw<HistoryStoreException>().Which;

            ex.Message.Should().Contain("exceeds");
            HighestId(temp).Should().Be(1L);
            File.Exists(ManifestPath(temp, 1)).Should().BeTrue();
            File.ReadAllText(Root(temp)).Should().Be("AB");
        });
    }

    [Fact]
    public void Shrinking_the_budget_evicts_older_checkpoints_on_the_next_capture()
    {
        WithRepo(temp =>
        {
            var engine = NewEngine(temp);
            engine.Capture(Load(temp));
            File.WriteAllText(Root(temp), "B");
            engine.Capture(Load(temp));

            var total = RetainedBytes(temp);
            var mb1 = new FileInfo(ManifestPath(temp, 1)).Length;
            SetMaxBytes(temp, total - (mb1 + 1));

            var result = engine.Capture(Load(temp));

            result.PublishedId.Should().Be(3L);
            File.Exists(ManifestPath(temp, 1)).Should().BeFalse();
            File.Exists(ManifestPath(temp, 2)).Should().BeFalse();
            File.Exists(ManifestPath(temp, 3)).Should().BeTrue();
            result.Cleanup.BudgetOk.Should().BeTrue();
            RetainedBytes(temp).Should().BeLessThanOrEqualTo(total - (mb1 + 1));
        });
    }

    private static void WithRepo(Action<string> action) =>
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"maxBytes\":999,\"exclude\":[\"_repolore/repolore.json\"]}}");
            File.WriteAllText(Root(temp), "A");
            action(temp);
        });

    private static CheckpointEngine NewEngine(string temp) =>
        new(temp, HistoryDir(temp), new Clock(() => new DateTimeOffset(2026, 9, 27, 11, 0, 0, TimeSpan.Zero)));

    private static string HistoryDir(string temp) => Path.Combine(temp, "_repolore", ".history");

    private static string Root(string temp) => Path.Combine(temp, "_repolore", "root.md");

    private static void SetMaxBytes(string temp, long maxBytes) =>
        File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"maxBytes\":" + maxBytes + ",\"exclude\":[\"_repolore/repolore.json\"]}}");

    private static RepoLoreConfig Load(string temp) => HistoryConfigLoader.Load(temp);

    private static string ManifestPath(string temp, long id) =>
        Path.Combine(HistoryDir(temp), "checkpoints", ManifestId.Format(id) + ".json");

    private static long HighestId(string temp) =>
        new CheckpointStore(HistoryDir(temp)).ReadHighestCompleted()?.Id ?? 0;

    private static long RetainedBytes(string temp)
    {
        var checkpoints = new CheckpointStore(HistoryDir(temp));
        var manifests = checkpoints.ListCompleted();
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var manifest in manifests)
        {
            total += new FileInfo(checkpoints.ManifestPath(manifest.Id)).Length;
            foreach (var file in manifest.Files)
                referenced.Add(file.Hash);
        }

        var objectsDir = new ObjectStore(HistoryDir(temp)).ObjectsDirectory;
        if (Directory.Exists(objectsDir))
            foreach (var file in Directory.EnumerateFiles(objectsDir))
                if (referenced.Contains(Path.GetFileName(file)))
                    total += new FileInfo(file).Length;
        return total;
    }
}
