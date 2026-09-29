using RepoLore.Core.Configuration;
using RepoLore.Infrastructure.History;
using RepoLore.Infrastructure.Tests;

namespace RepoLore.Infrastructure.Tests.History;

public static class RestoreEngineTests
{
    private const string Root = "_repolore/root.md";
    private const string A = "_repolore/architecture/a.md";
    private const string B = "_repolore/architecture/b.md";
    private const string C = "_repolore/architecture/c.md";

    public static void Run()
    {
        TestRunner.Check("restore to an old snapshot then restore its pre-operation id undoes it byte-for-byte", () =>
        {
            WithRepo(temp =>
            {
                Capture(temp).Capture(Load(temp)); // id 1: ROOT, A, B

                Write(temp, A, "A_EDITED");
                Write(temp, C, "C");
                Delete(temp, B);
                Capture(temp).Capture(Load(temp)); // id 2: edited state

                var restore = Restore(temp).Apply(Load(temp), 1);
                TestRunner.True(!restore.WasNoOp);
                TestRunner.Equal("A", Read(temp, A));
                TestRunner.Equal("B", Read(temp, B));
                TestRunner.True(!Exists(temp, C));
                TestRunner.Equal("ROOT", Read(temp, Root));

                var undo = Restore(temp).Apply(Load(temp), 2);
                TestRunner.True(!undo.WasNoOp);
                TestRunner.Equal("A_EDITED", Read(temp, A));
                TestRunner.Equal("C", Read(temp, C));
                TestRunner.True(!Exists(temp, B));
            });
        });

        TestRunner.Check("an excluded file stays untouched", () =>
        {
            WithRepo(temp =>
            {
                Capture(temp).Capture(Load(temp)); // id 1

                Write(temp, A, "A_EDITED");
                Write(temp, B, "B_EDITED");
                Capture(temp).Capture(Load(temp)); // id 2

                SetExclude(temp, new[] { B });
                Restore(temp).Apply(Load(temp), 1);

                TestRunner.Equal("A", Read(temp, A)); // restored
                TestRunner.Equal("B_EDITED", Read(temp, B)); // excluded, untouched
            });
        });

        TestRunner.Check("unknown id fails before any mutation", () =>
        {
            WithRepo(temp =>
            {
                Capture(temp).Capture(Load(temp));
                var ex = TestRunner.Capture<HistoryStoreException>(() => Restore(temp).Apply(Load(temp), 99));
                TestRunner.True(ex.Message.Contains("unknown", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal("ROOT", Read(temp, Root));
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });

        TestRunner.Check("a corrupt target object fails before any mutation", () =>
        {
            WithRepo(temp =>
            {
                Capture(temp).Capture(Load(temp)); // id 1
                Write(temp, A, "A_EDITED");
                Capture(temp).Capture(Load(temp)); // id 2

                CorruptObject(temp, HashOf("A")); // corrupt the original A object

                var ex = TestRunner.Capture<HistoryStoreException>(() => Restore(temp).Apply(Load(temp), 1));
                TestRunner.True(ex.Message.Contains("corrupt", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal("A_EDITED", Read(temp, A));
            });
        });

        TestRunner.Check("disabled history refuses restore", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
                var ex = TestRunner.Capture<HistoryStoreException>(() => Restore(temp).Apply(Load(temp), 1));
                TestRunner.True(ex.Message.Contains("disabled", StringComparison.Ordinal), ex.Message);
            });
        });

        TestRunner.Check("a no-op restore creates no pending and no new checkpoint", () =>
        {
            WithRepo(temp =>
            {
                Capture(temp).Capture(Load(temp)); // id 1
                var result = Restore(temp).Apply(Load(temp), 1);
                TestRunner.True(result.WasNoOp);
                TestRunner.True(!File.Exists(PendingPath(temp)));
                TestRunner.Equal(1L, HighestId(temp));
            });
        });

        TestRunner.Check("failure before the first replacement reports the pre-operation id and recovers", () =>
        {
            WithTempWithTwoCaptures(temp =>
            {
                var ex = TestRunner.Capture<RestoreException>(() =>
                    Restore(temp).Apply(Load(temp), 1, beforeFirstReplacement: () => throw new InvalidOperationException("injected")));

                TestRunner.Equal(2L, ex.RecoveryId);
                TestRunner.True(File.Exists(PendingPath(temp)));
                TestRunner.Equal("A2", Read(temp, A));
                TestRunner.Equal("B2", Read(temp, B));

                Recover(temp, 2L);
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });

        TestRunner.Check("failure between two replacements reports the same recovery id and recovers", () =>
        {
            WithTempWithTwoCaptures(temp =>
            {
                var ex = TestRunner.Capture<RestoreException>(() =>
                    Restore(temp).Apply(Load(temp), 1, afterFirstReplacement: () => throw new InvalidOperationException("injected")));

                TestRunner.Equal(2L, ex.RecoveryId);
                TestRunner.True(File.Exists(PendingPath(temp)));

                Recover(temp, 2L);
                TestRunner.Equal("A2", Read(temp, A));
                TestRunner.Equal("B2", Read(temp, B));
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });

        TestRunner.Check("failure after writes but before the final checkpoint reports the same recovery id and recovers", () =>
        {
            WithTempWithTwoCaptures(temp =>
            {
                var ex = TestRunner.Capture<RestoreException>(() =>
                    Restore(temp).Apply(Load(temp), 1, beforePostCheckpoint: () => throw new InvalidOperationException("injected")));

                TestRunner.Equal(2L, ex.RecoveryId);
                TestRunner.True(File.Exists(PendingPath(temp)));
                TestRunner.Equal("A", Read(temp, A)); // writes already applied
                TestRunner.Equal("B", Read(temp, B));

                Recover(temp, 2L);
                TestRunner.Equal("A2", Read(temp, A));
                TestRunner.Equal("B2", Read(temp, B));
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });

        TestRunner.Check("restart and recover restores the exact before-state", () =>
        {
            WithTempWithTwoCaptures(temp =>
            {
                var ex = TestRunner.Capture<RestoreException>(() =>
                    Restore(temp).Apply(Load(temp), 1, beforePostCheckpoint: () => throw new InvalidOperationException("injected")));

                TestRunner.Equal(2L, ex.RecoveryId);

                // fresh engine simulates a restart; recover via the recorded pre-operation id
                var recovered = Restore(temp).Recover(2L);
                TestRunner.True(recovered.WasRecovery);
                TestRunner.Equal("A2", Read(temp, A));
                TestRunner.Equal("B2", Read(temp, B));
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });

        TestRunner.Check("a fresh unrelated edit during recovery is detected, not overwritten", () =>
        {
            WithTempWithTwoCaptures(temp =>
            {
                TestRunner.Capture<RestoreException>(() =>
                    Restore(temp).Apply(Load(temp), 1, beforePostCheckpoint: () => throw new InvalidOperationException("injected")));

                Write(temp, A, "A_FRESH_EDIT");

                var ex = TestRunner.Capture<HistoryStoreException>(() => Restore(temp).Recover(2L));
                TestRunner.True(ex.Message.Contains("edited", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal("A_FRESH_EDIT", Read(temp, A)); // untouched
                TestRunner.True(File.Exists(PendingPath(temp))); // still pending, retryable
            });
        });

        TestRunner.Check("--path restores one file and leaves the rest untouched", () =>
        {
            WithTempWithTwoCaptures(temp =>
            {
                Restore(temp).Apply(Load(temp), 1, A);
                TestRunner.Equal("A", Read(temp, A));
                TestRunner.Equal("B2", Read(temp, B));
            });
        });

        TestRunner.Check("restore preflights the protected set against the budget before mutating", () =>
        {
            WithRepo(temp =>
            {
                Capture(temp).Capture(Load(temp)); // id 1
                Write(temp, A, "A2");
                Write(temp, B, "B2");
                Capture(temp).Capture(Load(temp)); // id 2

                SetMaxBytes(temp, 100);
                var ex = TestRunner.Capture<HistoryStoreException>(() => Restore(temp).Apply(Load(temp), 1));
                TestRunner.True(ex.Message.Contains("exceeds", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal("A2", Read(temp, A));
                TestRunner.Equal("B2", Read(temp, B));
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });
    }

    private static void Recover(string temp, long id)
    {
        var result = Restore(temp).Recover(id);
        TestRunner.True(result.WasRecovery);
    }

    private static void WithTempWithTwoCaptures(Action<string> action)
    {
        WithRepo(temp =>
        {
            Capture(temp).Capture(Load(temp)); // id 1: A, B
            Write(temp, A, "A2");
            Write(temp, B, "B2");
            Capture(temp).Capture(Load(temp)); // id 2: A2, B2
            action(temp);
        });
    }

    private static void CorruptObject(string temp, string hash)
    {
        var path = Path.Combine(HistoryDir(temp), "objects", hash);
        if (File.Exists(path))
            File.WriteAllText(path, "CORRUPTED");
    }

    private static string HashOf(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private static void SetExclude(string temp, string[] exclude)
    {
        File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
            "{\"formatVersion\":1,\"history\":{\"exclude\":[" + string.Join(',', exclude.Select(r => "\"" + r + "\"")) + "]}}");
    }

    private static void SetMaxBytes(string temp, long maxBytes) =>
        File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"maxBytes\":" + maxBytes + "}}");

    private static string PendingPath(string temp) => Path.Combine(HistoryDir(temp), "pending.json");

    private static long HighestId(string temp) =>
        new CheckpointStore(HistoryDir(temp)).ReadHighestCompleted()?.Id ?? 0;

    private static CheckpointEngine Capture(string temp) => new(temp, HistoryDir(temp), Clock());

    private static RestoreEngine Restore(string temp) => new(temp, HistoryDir(temp), Clock());

    private static Clock Clock() => new(() => new DateTimeOffset(2026, 9, 27, 11, 0, 0, TimeSpan.Zero));

    private static RepoLoreConfig Load(string temp) => HistoryConfigLoader.Load(temp);

    private static string HistoryDir(string temp) => Path.Combine(temp, "_repolore", ".history");

    private static void WithRepo(Action<string> action) =>
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "a.md"), "A");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "b.md"), "B");
            action(temp);
        });

    private static void Write(string temp, string relative, string content) =>
        File.WriteAllText(Path.Combine(temp, relative), content);

    private static void Delete(string temp, string relative)
    {
        var path = Path.Combine(temp, relative);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string Read(string temp, string relative) => File.ReadAllText(Path.Combine(temp, relative));

    private static bool Exists(string temp, string relative) => File.Exists(Path.Combine(temp, relative));
}
