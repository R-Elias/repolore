using RepoLore.Infrastructure.History;
using RepoLore.Infrastructure.Tests;

namespace RepoLore.Infrastructure.Tests.History;

public static class WriterLockTests
{
    public static void Run()
    {
        TestRunner.Check("the lock is acquired and released", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var history = Path.Combine(temp, ".history");
                using (WriterLock.Acquire(history))
                    TestRunner.True(File.Exists(Path.Combine(history, "write.lock")));
                using (WriterLock.Acquire(history))
                    TestRunner.True(true);
            });
        });

        TestRunner.Check("a second writer is refused", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var history = Path.Combine(temp, ".history");
                using (WriterLock.Acquire(history))
                    TestRunner.Throws<WriterLockException>(() => WriterLock.Acquire(history));
            });
        });
    }
}

public static class CheckpointEngineTests
{
    private const string Root = "_repolore/root.md";
    private const string A = "_repolore/architecture/a.md";

    public static void Run()
    {
        TestRunner.Check("first capture publishes a manifest and content objects", () =>
        {
            WithRepo(temp =>
            {
                var engine = NewEngine(temp);
                var result = engine.Capture(HistoryConfigLoader.Load(temp));

                TestRunner.True(!result.WasNoOp);
                TestRunner.Equal(1L, result.PublishedId);
                TestRunner.True(result.Added.Contains(A));
                TestRunner.True(File.Exists(Path.Combine(temp, "_repolore", ".history", "checkpoints", "0000000000000001.json")));
                TestRunner.Equal(1L, HighestId(temp));
            });
        });

        TestRunner.Check("unchanged state is a no-op", () =>
        {
            WithRepo(temp =>
            {
                var engine = NewEngine(temp);
                engine.Capture(HistoryConfigLoader.Load(temp));

                var second = engine.Capture(HistoryConfigLoader.Load(temp));
                TestRunner.True(second.WasNoOp);
                TestRunner.True(second.PublishedId is null);
                TestRunner.Equal(1L, HighestId(temp));
            });
        });

        TestRunner.Check("add, change, and delete produce distinct manifests", () =>
        {
            WithRepo(temp =>
            {
                var engine = NewEngine(temp);
                engine.Capture(HistoryConfigLoader.Load(temp));

                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "a.md"), "A_EDITED");
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "b.md"), "B_NEW");
                var second = engine.Capture(HistoryConfigLoader.Load(temp));
                TestRunner.Equal(2L, second.PublishedId);
                TestRunner.True(second.Changed.Contains(A));
                TestRunner.True(second.Added.Contains("_repolore/architecture/b.md"));

                File.Delete(Path.Combine(temp, "_repolore", "architecture", "a.md"));
                var third = engine.Capture(HistoryConfigLoader.Load(temp));
                TestRunner.Equal(3L, third.PublishedId);
                TestRunner.True(third.Removed.Contains(A));
            });
        });

        TestRunner.Check("a file changing between the two capture passes fails without publishing", () =>
        {
            WithRepo(temp =>
            {
                var engine = NewEngine(temp);
                var touched = false;

                var ex = TestRunner.Capture<HistoryStoreException>(() => engine.Capture(HistoryConfigLoader.Load(temp), beforeVerification: () =>
                {
                    if (!touched)
                    {
                        touched = true;
                        File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT_CHANGED");
                    }
                }));

                TestRunner.True(ex.Message.Contains("changed while capturing", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal(0L, HighestId(temp));
            });
        });

        TestRunner.Check("failure before manifest publication leaves the previous checkpoint valid", () =>
        {
            WithRepo(temp =>
            {
                var engine = NewEngine(temp);
                engine.Capture(HistoryConfigLoader.Load(temp));

                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT_CHANGED");
                var ex = TestRunner.Capture<HistoryStoreException>(() => engine.Capture(HistoryConfigLoader.Load(temp), beforeManifestPublish: () => throw new HistoryStoreException("injected failure")));

                TestRunner.True(ex.Message.Contains("injected failure", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal(1L, HighestId(temp));
                TestRunner.Equal("ROOT_CHANGED", File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")));
            });
        });

        TestRunner.Check("distinct paths with identical bytes share one object", () =>
        {
            WithRepo(temp =>
            {
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "c.md"), "SAME");
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "d.md"), "SAME");

                var engine = NewEngine(temp);
                engine.Capture(HistoryConfigLoader.Load(temp));

                var objects = Directory.EnumerateFiles(Path.Combine(temp, "_repolore", ".history", "objects")).Count();
                // root.md, repolore.json, a.md, and the shared c.md/d.md -> four distinct contents
                TestRunner.Equal(4, objects);
            });
        });
    }

    private static CheckpointEngine NewEngine(string temp) =>
        new(temp, Path.Combine(temp, "_repolore", ".history"), new Clock(() => new DateTimeOffset(2026, 9, 27, 11, 0, 0, TimeSpan.Zero)));

    private static long HighestId(string temp)
    {
        var store = new CheckpointStore(Path.Combine(temp, "_repolore", ".history"));
        var highest = store.ReadHighestCompleted();
        return highest is null ? 0 : highest.Id;
    }

    private static void WriteRepo(string temp)
    {
        Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
        File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
        File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
        File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "a.md"), "A_ORIGINAL");
    }

    private static void WithRepo(Action<string> action)
    {
        TestSupport.WithTemp(temp =>
        {
            WriteRepo(temp);
            action(temp);
        });
    }
}
