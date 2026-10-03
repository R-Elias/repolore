using System.Security.Cryptography;
using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests;

public class HistoryTests
{
    [Fact]
    public void Checkpoint_captures_add_change_delete_as_three_distinguishable_manifests_with_recoverable_bytes()
    {
        WithFixture("history-failures", temp =>
        {
            var aPath = Path.Combine(temp, "_repolore", "architecture", "a.md");
            var originalBytes = File.ReadAllBytes(aPath);
            var originalHash = Hash(originalBytes);

            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            File.WriteAllText(aPath, File.ReadAllText(Path.Combine(temp, "expected-states", "edited-a.txt")));
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "b.md"),
                File.ReadAllText(Path.Combine(temp, "expected-states", "new-b.txt")));
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            File.Delete(aPath);
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            var history = TestSupport.Run(temp, TestSupport.Cli, "history");
            history.Code.Should().Be(0);
            var historyText = TestSupport.Normalize(history.Out);
            CountManifestLines(historyText).Should().Be(3);
            CountLine(historyText, "  _repolore/architecture/a.md").Should().Be(2);
            CountLine(historyText, "  _repolore/architecture/b.md").Should().Be(2);

            var objectPath = Path.Combine(temp, "_repolore", ".history", "objects", originalHash);
            File.Exists(objectPath).Should().BeTrue();
            Convert.ToHexString(File.ReadAllBytes(objectPath)).Should().Be(Convert.ToHexString(originalBytes));
        });
    }

    [Fact]
    public void Unchanged_bytes_are_a_no_op()
    {
        WithFixture("history-failures", temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            var second = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            second.Code.Should().Be(0);
            TestSupport.Normalize(second.Out).Should().Contain("no changes");
            var history = TestSupport.Run(temp, TestSupport.Cli, "history");
            CountManifestLines(TestSupport.Normalize(history.Out)).Should().Be(1);
        });
    }

    [Fact]
    public void Changing_only_mtime_is_a_no_op()
    {
        TestSupport.WithTemp(temp =>
        {
            WriteFixture(temp);
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.SetLastWriteTimeUtc(Path.Combine(temp, "_repolore", "architecture", "a.md"), DateTime.UtcNow.AddMinutes(5));
            var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            result.Code.Should().Be(0);
            TestSupport.Normalize(result.Out).Should().Contain("no changes");
        });
    }

    [Fact]
    public void Changing_bytes_with_the_same_length_and_mtime_is_captured()
    {
        TestSupport.WithTemp(temp =>
        {
            WriteFixture(temp);
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            var aPath = Path.Combine(temp, "_repolore", "architecture", "a.md");
            var original = File.ReadAllText(aPath);
            var mtime = File.GetLastWriteTimeUtc(aPath);
            var flipped = original.Substring(0, original.Length - 1) + 'X';
            File.WriteAllText(aPath, flipped);
            File.SetLastWriteTimeUtc(aPath, mtime);
            flipped.Length.Should().Be(original.Length);

            var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            result.Code.Should().Be(0);
            TestSupport.Normalize(result.Out).Should().NotContain("no changes");
            var history = TestSupport.Run(temp, TestSupport.Cli, "history");
            CountManifestLines(TestSupport.Normalize(history.Out)).Should().Be(2);
        });
    }

    [Fact]
    public void Distinct_paths_with_identical_bytes_share_one_object()
    {
        TestSupport.WithTemp(temp =>
        {
            WriteFixture(temp);
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "c.md"), "SAME_CONTENT");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "d.md"), "SAME_CONTENT");
            var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            result.Code.Should().Be(0);

            var objects = Path.Combine(temp, "_repolore", ".history", "objects");
            Directory.EnumerateFiles(objects).Count().Should().Be(4);

            var manifestText = File.ReadAllText(Path.Combine(temp, "_repolore", ".history", "checkpoints", "0000000000000001.json"));
            manifestText.Should().Contain("\"path\":\"_repolore/architecture/c.md\"");
            manifestText.Should().Contain("\"path\":\"_repolore/architecture/d.md\"");
        });
    }

    [Fact]
    public void Scope_changes_are_recorded_even_when_captured_hashes_match()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                "{\"formatVersion\":1,\"history\":{\"exclude\":[\"_repolore/repolore.json\"]}}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)).Should().Be(1);

            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                "{\"formatVersion\":1,\"history\":{\"exclude\":[\"_repolore/repolore.json\",\"_repolore/nonexistent.md\"]}}");
            var second = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            second.Code.Should().Be(0);
            TestSupport.Normalize(second.Out).Should().NotContain("no changes");
            CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)).Should().Be(2);
        });
    }

    [Fact]
    public void Explicit_checkpoint_with_disabled_history_exits_3_and_history_stays_readable()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

            var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            result.Code.Should().Be(3);
            result.Error.Should().Contain("disabled");

            var history = TestSupport.Run(temp, TestSupport.Cli, "history");
            history.Code.Should().Be(0);
            CountManifestLines(TestSupport.Normalize(history.Out)).Should().Be(0);
        });
    }

    [Fact]
    public void A_second_writer_is_refused()
    {
        WithFixture("history-failures", temp =>
        {
            var lockPath = Path.Combine(temp, "_repolore", ".history", "write.lock");
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
                result.Code.Should().Be(3);
                result.Error.Should().Contain("another RepoLore writer");
            }
        });
    }

    [Fact]
    public void Alpha_conflict_is_captured_as_separate_paths()
    {
        WithFixture("alpha-conflict", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            result.Code.Should().Be(0);
            var manifestText = File.ReadAllText(Path.Combine(temp, "_repolore", ".history", "checkpoints", "0000000000000001.json"));
            manifestText.Should().Contain("\"path\":\"_repolore/sparse-tree/src/src.md\"");
            manifestText.Should().Contain("\"path\":\"_repolore/tree/src/src.md\"");
        });
    }

    [Fact]
    public void History_ignores_incomplete_files_but_fails_on_a_malformed_completed_manifest()
    {
        TestSupport.WithTemp(temp =>
        {
            WriteFixture(temp);
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            var checkpointsDir = Path.Combine(temp, "_repolore", ".history", "checkpoints");
            File.WriteAllText(Path.Combine(checkpointsDir, "0000000000000002.tmp-abc"), "partial");
            File.WriteAllText(Path.Combine(checkpointsDir, "readme.txt"), "not a manifest");

            var history = TestSupport.Run(temp, TestSupport.Cli, "history");
            history.Code.Should().Be(0);
            CountManifestLines(TestSupport.Normalize(history.Out)).Should().Be(1);

            File.WriteAllText(Path.Combine(checkpointsDir, "0000000000000002.json"), "{broken");
            var corrupt = TestSupport.Run(temp, TestSupport.Cli, "history");
            corrupt.Code.Should().Be(3);
            corrupt.Error.Should().Contain("corrupt manifest");
        });
    }

    [Fact]
    public void An_over_budget_checkpoint_exits_3_and_leaves_the_previous_checkpoint_usable()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "A");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            var retained = HistoryBytes(Path.Combine(temp, "_repolore", ".history"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "B");
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                "{\"formatVersion\":1,\"history\":{\"maxBytes\":" + (retained - 1) + "}}");

            var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            result.Code.Should().Be(3);
            result.Error.Should().Contain("exceeds");

            var history = TestSupport.Run(temp, TestSupport.Cli, "history");
            history.Code.Should().Be(0);
            CountManifestLines(TestSupport.Normalize(history.Out)).Should().Be(1);
            File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")).Should().Be("B");
        });
    }

    [Fact]
    public void Checkpoint_reports_eviction_when_the_budget_shrinks()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "A");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "B");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            var retained = HistoryBytes(Path.Combine(temp, "_repolore", ".history"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"),
                "{\"formatVersion\":1,\"history\":{\"maxBytes\":" + retained + "}}");

            var result = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            result.Code.Should().Be(0);
            TestSupport.Normalize(result.Out).Should().Contain("evicted");
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
