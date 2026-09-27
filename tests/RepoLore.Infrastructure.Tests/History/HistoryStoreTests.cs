using System.Text;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;
using RepoLore.Infrastructure.Tests;

namespace RepoLore.Infrastructure.Tests.History;

public static class ObjectStoreTests
{
    public static void Run()
    {
        TestRunner.Check("store, validate, and reuse share one object", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var store = new ObjectStore(Path.Combine(temp, ".history"));
                store.EnsureDirectories();
                var bytes = Encoding.UTF8.GetBytes("OBJECT_SENTINEL");
                var hash = ObjectStore.Hash(bytes);

                TestRunner.Equal(hash, store.Store(bytes));
                TestRunner.True(store.Exists(hash));
                TestRunner.True(store.Validate(hash));
                TestRunner.Equal("OBJECT_SENTINEL", Encoding.UTF8.GetString(store.Read(hash)));

                store.Store(bytes);
                TestRunner.Equal(1, Directory.EnumerateFiles(store.ObjectsDirectory).Count());
            });
        });

        TestRunner.Check("a corrupt object is never silently replaced", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var store = new ObjectStore(Path.Combine(temp, ".history"));
                store.EnsureDirectories();
                var bytes = Encoding.UTF8.GetBytes("ORIGINAL");
                var hash = ObjectStore.Hash(bytes);
                store.Store(bytes);

                File.WriteAllBytes(Path.Combine(store.ObjectsDirectory, hash), Encoding.UTF8.GetBytes("TAMPERED"));
                TestRunner.True(!store.Validate(hash));
                TestRunner.Throws<HistoryStoreException>(() => store.Store(bytes), "corrupt object must fail, not overwrite");
            });
        });
    }
}

public static class CheckpointStoreTests
{
    public static void Run()
    {
        TestRunner.Check("publish and read the highest completed id", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var store = new CheckpointStore(Path.Combine(temp, ".history"));
                store.EnsureDirectory();
                store.Publish(Manifest(1));
                store.Publish(Manifest(3));
                store.Publish(Manifest(2));

                var all = store.ListCompleted();
                TestRunner.Equal(3, all.Count);
                TestRunner.Equal(1L, all[0].Id);
                TestRunner.Equal(2L, all[1].Id);
                TestRunner.Equal(3L, all[2].Id);
                TestRunner.Equal(3L, store.ReadHighestCompleted()!.Id);
            });
        });

        TestRunner.Check("incomplete and non-manifest files are ignored", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var store = new CheckpointStore(Path.Combine(temp, ".history"));
                store.EnsureDirectory();
                store.Publish(Manifest(1));

                File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "0000000000000002.tmp-x"), "partial");
                File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "1.json"), "{}");
                File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "notes.txt"), "ignore me");

                var all = store.ListCompleted();
                TestRunner.Equal(1, all.Count);
                TestRunner.Equal(1L, all[0].Id);
            });
        });

        TestRunner.Check("a malformed completed manifest is an error, not silently skipped", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var store = new CheckpointStore(Path.Combine(temp, ".history"));
                store.EnsureDirectory();
                File.WriteAllText(Path.Combine(store.CheckpointsDirectory, "0000000000000001.json"), "{not json");
                TestRunner.Throws<HistoryStoreException>(() => store.ListCompleted());
            });
        });

        TestRunner.Check("an empty history directory yields no manifests", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var store = new CheckpointStore(Path.Combine(temp, ".history"));
                TestRunner.Equal(0, store.ListCompleted().Count);
                TestRunner.True(store.ReadHighestCompleted() is null);
            });
        });
    }

    private static CheckpointManifest Manifest(long id) =>
        new(id, "t", HistoryVersion.Current, 1,
            new CaptureScope(true, 100, new List<string>(), CaptureScope.RepoLoreJsonPresent),
            new List<FileEntry>());
}
