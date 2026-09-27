using System.Security.Cryptography;
using RepoLore.Cli.Tests;

namespace RepoLore.Cli.Tests;

public static class HistoryTests
{
    public static void Run()
    {
        TestRunner.Check("checkpoint captures add/change/delete as three distinguishable manifests with recoverable bytes", () =>
        {
            WithFixture("history-failures", temp =>
            {
                var aPath = Path.Combine(temp, "_repolore", "architecture", "a.md");
                var originalBytes = File.ReadAllBytes(aPath);
                var originalHash = Hash(originalBytes);

                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                File.WriteAllText(aPath, File.ReadAllText(Path.Combine(temp, "expected-states", "edited-a.txt")));
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "b.md"),
                    File.ReadAllText(Path.Combine(temp, "expected-states", "new-b.txt")));
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                File.Delete(aPath);
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                var history = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(0, history.Code, history.Error);
                var historyText = TestSupport.Normalize(history.Out);
                TestRunner.Equal(3, CountManifestLines(historyText));
                TestRunner.Equal(2, CountLine(historyText, "  _repolore/architecture/a.md"));
                TestRunner.Equal(2, CountLine(historyText, "  _repolore/architecture/b.md"));

                var objectPath = Path.Combine(temp, "_repolore", ".history", "objects", originalHash);
                TestRunner.True(File.Exists(objectPath));
                TestRunner.Equal(Convert.ToHexString(originalBytes), Convert.ToHexString(File.ReadAllBytes(objectPath)));
            });
        });

        TestRunner.Check("unchanged bytes are a no-op", () =>
        {
            WithFixture("history-failures", temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                var second = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(0, second.Code, second.Error);
                TestRunner.True(TestSupport.Normalize(second.Out).Contains("no changes", StringComparison.Ordinal));
                var history = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(1, CountManifestLines(TestSupport.Normalize(history.Out)));
            });
        });

        TestRunner.Check("changing only mtime is a no-op", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                WriteFixture(temp);
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                File.SetLastWriteTimeUtc(Path.Combine(temp, "_repolore", "architecture", "a.md"), DateTime.UtcNow.AddMinutes(5));
                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(0, result.Code, result.Error);
                TestRunner.True(TestSupport.Normalize(result.Out).Contains("no changes", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("changing bytes with the same length and mtime is captured", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                WriteFixture(temp);
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                var aPath = Path.Combine(temp, "_repolore", "architecture", "a.md");
                var original = File.ReadAllText(aPath);
                var mtime = File.GetLastWriteTimeUtc(aPath);
                var flipped = original.Substring(0, original.Length - 1) + 'X';
                File.WriteAllText(aPath, flipped);
                File.SetLastWriteTimeUtc(aPath, mtime);
                TestRunner.Equal(original.Length, flipped.Length);

                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(0, result.Code, result.Error);
                TestRunner.True(!TestSupport.Normalize(result.Out).Contains("no changes", StringComparison.Ordinal));
                var history = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(2, CountManifestLines(TestSupport.Normalize(history.Out)));
            });
        });

        TestRunner.Check("distinct paths with identical bytes share one object", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                WriteFixture(temp);
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "c.md"), "SAME_CONTENT");
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "d.md"), "SAME_CONTENT");
                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(0, result.Code, result.Error);

                var objects = Path.Combine(temp, "_repolore", ".history", "objects");
                TestRunner.Equal(4, Directory.EnumerateFiles(objects).Count());

                var manifestText = File.ReadAllText(Path.Combine(temp, "_repolore", ".history", "checkpoints", "0000000000000001.json"));
                TestRunner.True(manifestText.Contains("\"path\":\"_repolore/architecture/c.md\"", StringComparison.Ordinal));
                TestRunner.True(manifestText.Contains("\"path\":\"_repolore/architecture/d.md\"", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("scope changes are recorded even when captured hashes match", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                    "{\"formatVersion\":1,\"history\":{\"exclude\":[\"_repolore/repolore.json\"]}}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                TestRunner.Equal(1, CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)));

                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                    "{\"formatVersion\":1,\"history\":{\"exclude\":[\"_repolore/repolore.json\",\"_repolore/nonexistent.md\"]}}");
                var second = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(0, second.Code, second.Error);
                TestRunner.True(!TestSupport.Normalize(second.Out).Contains("no changes", StringComparison.Ordinal));
                TestRunner.Equal(2, CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)));
            });
        });

        TestRunner.Check("explicit checkpoint with disabled history exits 3 and history stays readable", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(3, result.Code);
                TestRunner.True(result.Error.Contains("disabled", StringComparison.Ordinal));

                var history = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(0, history.Code, history.Error);
                TestRunner.Equal(0, CountManifestLines(TestSupport.Normalize(history.Out)));
            });
        });

        TestRunner.Check("a second writer is refused", () =>
        {
            WithFixture("history-failures", temp =>
            {
                var lockPath = Path.Combine(temp, "_repolore", ".history", "write.lock");
                Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
                using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                    TestRunner.Equal(3, result.Code);
                    TestRunner.True(result.Error.Contains("another RepoLore writer", StringComparison.Ordinal));
                }
            });
        });

        TestRunner.Check("alpha conflict is captured as separate paths", () =>
        {
            WithFixture("alpha-conflict", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(0, result.Code, result.Error);
                var manifestText = File.ReadAllText(Path.Combine(temp, "_repolore", ".history", "checkpoints", "0000000000000001.json"));
                TestRunner.True(manifestText.Contains("\"path\":\"_repolore/sparse-tree/src/src.md\"", StringComparison.Ordinal), manifestText);
                TestRunner.True(manifestText.Contains("\"path\":\"_repolore/tree/src/src.md\"", StringComparison.Ordinal), manifestText);
            });
        });

        TestRunner.Check("history ignores incomplete files but fails on a malformed completed manifest", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                WriteFixture(temp);
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                var checkpointsDir = Path.Combine(temp, "_repolore", ".history", "checkpoints");
                File.WriteAllText(Path.Combine(checkpointsDir, "0000000000000002.tmp-abc"), "partial");
                File.WriteAllText(Path.Combine(checkpointsDir, "readme.txt"), "not a manifest");

                var history = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(0, history.Code, history.Error);
                TestRunner.Equal(1, CountManifestLines(TestSupport.Normalize(history.Out)));

                File.WriteAllText(Path.Combine(checkpointsDir, "0000000000000002.json"), "{broken");
                var corrupt = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(3, corrupt.Code);
                TestRunner.True(corrupt.Error.Contains("corrupt manifest", StringComparison.Ordinal));
            });
        });

        TestRunner.Check("an over-budget checkpoint exits 3 and leaves the previous checkpoint usable", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "A");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                var retained = HistoryBytes(Path.Combine(temp, "_repolore", ".history"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "B");
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                    "{\"formatVersion\":1,\"history\":{\"maxBytes\":" + (retained - 1) + "}}");

                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(3, result.Code);
                TestRunner.True(result.Error.Contains("exceeds", StringComparison.Ordinal), result.Error);

                var history = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(0, history.Code, history.Error);
                TestRunner.Equal(1, CountManifestLines(TestSupport.Normalize(history.Out)));
                TestRunner.Equal("B", File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")));
            });
        });

        TestRunner.Check("checkpoint reports eviction when the budget shrinks", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "A");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "B");
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code);

                var retained = HistoryBytes(Path.Combine(temp, "_repolore", ".history"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                    "{\"formatVersion\":1,\"history\":{\"maxBytes\":" + retained + "}}");

                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                TestRunner.Equal(0, result.Code, result.Error);
                TestRunner.True(TestSupport.Normalize(result.Out).Contains("evicted", StringComparison.Ordinal), result.Out);
            });
        });
    }

    private static long HistoryBytes(string historyDir)
    {
        long total = 0;
        var checkpoints = Path.Combine(historyDir, "checkpoints");
        if (Directory.Exists(checkpoints))
            foreach (var file in Directory.EnumerateFiles(checkpoints))
                if (file.EndsWith(".json", StringComparison.Ordinal))
                    total += new FileInfo(file).Length;
        var objects = Path.Combine(historyDir, "objects");
        if (Directory.Exists(objects))
            foreach (var file in Directory.EnumerateFiles(objects))
                total += new FileInfo(file).Length;
        return total;
    }

    private static void WriteFixture(string temp)
    {
        Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
        File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
        File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT_SENTINEL");
        File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "a.md"), "A_ORIGINAL_SENTINEL");
    }

    private static void WithFixture(string name, Action<string> action) => TestSupport.WithFixture(name, action);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static int CountManifestLines(string text)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
            if (line.StartsWith("000000000000000", StringComparison.Ordinal))
                count++;
        return count;
    }

    private static int CountLine(string text, string expected)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
            if (line == expected)
                count++;
        return count;
    }
}
