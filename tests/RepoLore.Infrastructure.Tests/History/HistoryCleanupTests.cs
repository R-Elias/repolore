using System.Text;
using RepoLore.Core.Configuration;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;
using RepoLore.Infrastructure.Tests;

namespace RepoLore.Infrastructure.Tests.History;

public static class HistoryCleanupTests
{
    private static readonly CaptureScope Scope = new(true, 100, new List<string>(), CaptureScope.RepoLoreJsonPresent);

    public static void Run()
    {
        TestRunner.Check("evicts the oldest manifest first and reclaims its object", () =>
        {
            TestSupport.WithTemp(temp =>
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

                TestRunner.Equal(1, result.Evicted);
                TestRunner.True(result.BudgetOk);
                TestRunner.True(!File.Exists(checkpoints.ManifestPath(1)));
                TestRunner.True(File.Exists(checkpoints.ManifestPath(2)));
                TestRunner.True(File.Exists(checkpoints.ManifestPath(3)));
                TestRunner.True(!File.Exists(Path.Combine(objects.ObjectsDirectory, h1)));
                TestRunner.True(File.Exists(Path.Combine(objects.ObjectsDirectory, h2)));
                TestRunner.True(File.Exists(Path.Combine(objects.ObjectsDirectory, h3)));
            });
        });

        TestRunner.Check("a shared object survives while any manifest references it", () =>
        {
            TestSupport.WithTemp(temp =>
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

                TestRunner.Equal(1, result.Evicted);
                TestRunner.True(!File.Exists(checkpoints.ManifestPath(1)));
                TestRunner.True(File.Exists(checkpoints.ManifestPath(2)));
                TestRunner.True(File.Exists(Path.Combine(objects.ObjectsDirectory, shared)));
                TestRunner.True(!File.Exists(Path.Combine(objects.ObjectsDirectory, only1)));
                TestRunner.True(File.Exists(Path.Combine(objects.ObjectsDirectory, only2)));
            });
        });

        TestRunner.Check("the latest manifest is never evicted", () =>
        {
            TestSupport.WithTemp(temp =>
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

                TestRunner.True(!File.Exists(checkpoints.ManifestPath(1)));
                TestRunner.True(File.Exists(checkpoints.ManifestPath(2)));
                TestRunner.True(!result.BudgetOk);
            });
        });

        TestRunner.Check("a failed delete reports budget not achieved and keeps history valid", () =>
        {
            TestSupport.WithTemp(temp =>
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

                TestRunner.Equal(1, result.Evicted);
                TestRunner.True(!result.BudgetOk);
                TestRunner.True(File.Exists(failOn));
                TestRunner.True(!File.Exists(checkpoints.ManifestPath(1)));
                TestRunner.Equal(2L, checkpoints.ReadHighestCompleted()!.Id);
            });
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

public static class RetentionEngineTests
{
    public static void Run()
    {
        TestRunner.Check("a checkpoint exactly at budget succeeds and evicts the older one", () =>
        {
            WithRepo(temp =>
            {
                var engine = NewEngine(temp);
                engine.Capture(Load(temp));

                var s1 = RetainedBytes(temp);
                File.WriteAllText(Root(temp), "B");
                SetMaxBytes(temp, s1);

                var result = engine.Capture(Load(temp));

                TestRunner.True(!result.WasNoOp);
                TestRunner.Equal(2L, result.PublishedId);
                TestRunner.Equal(2L, HighestId(temp));
                TestRunner.True(!File.Exists(ManifestPath(temp, 1)));
                TestRunner.True(File.Exists(ManifestPath(temp, 2)));
                TestRunner.True(result.Cleanup.BudgetOk);
                TestRunner.Equal(s1, RetainedBytes(temp));
            });
        });

        TestRunner.Check("a checkpoint one byte over budget fails and leaves prior state usable", () =>
        {
            WithRepo(temp =>
            {
                var engine = NewEngine(temp);
                engine.Capture(Load(temp));

                var s1 = RetainedBytes(temp);
                File.WriteAllText(Root(temp), "AB");
                SetMaxBytes(temp, s1);

                var ex = TestRunner.Capture<HistoryStoreException>(() => engine.Capture(Load(temp)));

                TestRunner.True(ex.Message.Contains("exceeds", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal(1L, HighestId(temp));
                TestRunner.True(File.Exists(ManifestPath(temp, 1)));
                TestRunner.Equal("AB", File.ReadAllText(Root(temp)));
            });
        });

        TestRunner.Check("shrinking the budget evicts older checkpoints on the next capture", () =>
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

                TestRunner.Equal(3L, result.PublishedId);
                TestRunner.True(!File.Exists(ManifestPath(temp, 1)));
                TestRunner.True(!File.Exists(ManifestPath(temp, 2)));
                TestRunner.True(File.Exists(ManifestPath(temp, 3)));
                TestRunner.True(result.Cleanup.BudgetOk);
                TestRunner.True(RetainedBytes(temp) <= total - (mb1 + 1));
            });
        });
    }

    private static void WithRepo(Action<string> action) =>
        TestSupport.WithTemp(temp =>
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
