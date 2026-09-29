using System.Text;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;

namespace RepoLore.Cli.Tests;

public static class RestoreTests
{
    public static void Run()
    {
        TestRunner.Check("restore to an old snapshot then restore its pre-operation id undoes it", () =>
        {
            WithRepo(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code); // id 1

                File.WriteAllText(A(temp), "A2");
                File.Delete(B(temp));
                File.WriteAllText(C(temp), "C");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code); // id 2

                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "restore", "1").Code);
                TestRunner.Equal("A", File.ReadAllText(A(temp)));
                TestRunner.Equal("B", File.ReadAllText(B(temp)));
                TestRunner.True(!File.Exists(C(temp)));

                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "restore", "2").Code);
                TestRunner.Equal("A2", File.ReadAllText(A(temp)));
                TestRunner.Equal("C", File.ReadAllText(C(temp)));
                TestRunner.True(!File.Exists(B(temp)));
            });
        });

        TestRunner.Check("--dry-run lists actions and writes nothing", () =>
        {
            WithRepo(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                File.WriteAllText(A(temp), "A2");
                File.Delete(B(temp));
                File.WriteAllText(C(temp), "C");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                var preview = TestSupport.Run(temp, TestSupport.Cli, "restore", "1", "--dry-run");
                TestRunner.Equal(0, preview.Code, preview.Error);
                var text = TestSupport.Normalize(preview.Out);
                TestRunner.True(text.Contains("replace _repolore/architecture/a.md", StringComparison.Ordinal), text);
                TestRunner.True(text.Contains("add _repolore/architecture/b.md", StringComparison.Ordinal), text);
                TestRunner.True(text.Contains("delete _repolore/architecture/c.md", StringComparison.Ordinal), text);
                TestRunner.True(text.Contains("unchanged _repolore/root.md", StringComparison.Ordinal), text);

                TestRunner.Equal("A2", File.ReadAllText(A(temp))); // untouched
                TestRunner.Equal("C", File.ReadAllText(C(temp)));
                TestRunner.True(!File.Exists(B(temp)));
                TestRunner.Equal(2, CountManifests(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)));
            });
        });

        TestRunner.Check("unknown id exits 3", () =>
        {
            WithRepo(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                var result = TestSupport.Run(temp, TestSupport.Cli, "restore", "99");
                TestRunner.Equal(3, result.Code);
                TestRunner.True(result.Error.Contains("unknown", StringComparison.Ordinal), result.Error);
            });
        });

        TestRunner.Check("--path restores one file and leaves the rest untouched", () =>
        {
            WithRepo(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                File.WriteAllText(A(temp), "A2");
                File.WriteAllText(B(temp), "B2");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "restore", "1", "--path", "_repolore/architecture/a.md").Code);
                TestRunner.Equal("A", File.ReadAllText(A(temp)));
                TestRunner.Equal("B2", File.ReadAllText(B(temp)));
            });
        });

        TestRunner.Check("checkpoint and restore refuse while a restore is pending", () =>
        {
            WithRepo(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                File.WriteAllText(A(temp), "A2");
                File.WriteAllText(B(temp), "B2");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                WritePending(temp, targetId: 1, preOperationId: 2);

                var checkpoint = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(3, checkpoint.Code);
                TestRunner.True(checkpoint.Error.Contains("restore 2", StringComparison.Ordinal), checkpoint.Error);

                var otherRestore = TestSupport.Run(temp, TestSupport.Cli, "restore", "1");
                TestRunner.Equal(3, otherRestore.Code);
                TestRunner.True(otherRestore.Error.Contains("restore 2", StringComparison.Ordinal), otherRestore.Error);
            });
        });

        TestRunner.Check("restore <pre-operation-id> recovers the before-state and clears pending", () =>
        {
            WithRepo(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code); // id 1: A, B
                File.WriteAllText(A(temp), "A2");
                File.WriteAllText(B(temp), "B2");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code); // id 2: A2, B2

                WritePending(temp, targetId: 1, preOperationId: 2);
                File.WriteAllText(A(temp), "A"); // simulate fully-applied writes before the crash
                File.WriteAllText(B(temp), "B");

                var recovered = TestSupport.Run(temp, TestSupport.Cli, "restore", "2");
                TestRunner.Equal(0, recovered.Code, recovered.Error);
                TestRunner.True(TestSupport.Normalize(recovered.Out).Contains("recovered", StringComparison.Ordinal), recovered.Out);
                TestRunner.Equal("A2", File.ReadAllText(A(temp)));
                TestRunner.Equal("B2", File.ReadAllText(B(temp)));
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });

        TestRunner.Check("a fresh edit during recovery is detected, not overwritten", () =>
        {
            WithRepo(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                File.WriteAllText(A(temp), "A2");
                File.WriteAllText(B(temp), "B2");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                WritePending(temp, targetId: 1, preOperationId: 2);
                File.WriteAllText(A(temp), "A");
                File.WriteAllText(B(temp), "B");
                File.WriteAllText(A(temp), "A_FRESH_EDIT"); // unrelated edit

                var recovered = TestSupport.Run(temp, TestSupport.Cli, "restore", "2");
                TestRunner.Equal(3, recovered.Code);
                TestRunner.True(recovered.Error.Contains("edited", StringComparison.Ordinal), recovered.Error);
                TestRunner.Equal("A_FRESH_EDIT", File.ReadAllText(A(temp)));
                TestRunner.True(File.Exists(PendingPath(temp))); // still pending
            });
        });
    }

    private static void WritePending(string temp, long targetId, long preOperationId)
    {
        var plan = new List<PendingPlanEntry>
        {
            new("_repolore/architecture/a.md", HashOf("A2"), HashOf("A")),
            new("_repolore/architecture/b.md", HashOf("B2"), HashOf("B"))
        };
        var transaction = new PendingTransaction(HistoryVersion.Current, targetId, preOperationId, 1, true, 209715200, new List<string>(), plan);
        new PendingStore(HistoryDir(temp)).Write(transaction);
    }

    private static string HashOf(string content) => ObjectStore.Hash(Encoding.UTF8.GetBytes(content));

    private static string PendingPath(string temp) => Path.Combine(HistoryDir(temp), "pending.json");

    private static string HistoryDir(string temp) => Path.Combine(temp, "_repolore", ".history");

    private static string A(string temp) => Path.Combine(temp, "_repolore", "architecture", "a.md");

    private static string B(string temp) => Path.Combine(temp, "_repolore", "architecture", "b.md");

    private static string C(string temp) => Path.Combine(temp, "_repolore", "architecture", "c.md");

    private static int CountManifests(string text)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
            if (line.StartsWith("000000000000000", StringComparison.Ordinal))
                count++;
        return count;
    }

    private static void WithRepo(Action<string> action) =>
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
            File.WriteAllText(A(temp), "A");
            File.WriteAllText(B(temp), "B");
            action(temp);
        });
}
