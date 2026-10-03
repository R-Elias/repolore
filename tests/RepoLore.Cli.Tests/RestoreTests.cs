using System.Text;
using FluentAssertions;
using RepoLore.Core.Restore;
using RepoLore.Core.Snapshot;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Cli.Tests;

public class RestoreTests
{
    [Fact]
    public void Restore_to_an_old_snapshot_then_restore_its_pre_operation_id_undoes_it()
    {
        WithRepo(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0); // id 1

            File.WriteAllText(A(temp), "A2");
            File.Delete(B(temp));
            File.WriteAllText(C(temp), "C");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0); // id 2

            TestSupport.Run(temp, TestSupport.Cli, "restore", "1").Code.Should().Be(0);
            File.ReadAllText(A(temp)).Should().Be("A");
            File.ReadAllText(B(temp)).Should().Be("B");
            File.Exists(C(temp)).Should().BeFalse();

            TestSupport.Run(temp, TestSupport.Cli, "restore", "2").Code.Should().Be(0);
            File.ReadAllText(A(temp)).Should().Be("A2");
            File.ReadAllText(C(temp)).Should().Be("C");
            File.Exists(B(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void Dry_run_lists_actions_and_writes_nothing()
    {
        WithRepo(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.WriteAllText(A(temp), "A2");
            File.Delete(B(temp));
            File.WriteAllText(C(temp), "C");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            var preview = TestSupport.Run(temp, TestSupport.Cli, "restore", "1", "--dry-run");
            preview.Code.Should().Be(0);
            var text = TestSupport.Normalize(preview.Out);
            text.Should().Contain("replace _repolore/architecture/a.md");
            text.Should().Contain("add _repolore/architecture/b.md");
            text.Should().Contain("delete _repolore/architecture/c.md");
            text.Should().Contain("unchanged _repolore/root.md");

            File.ReadAllText(A(temp)).Should().Be("A2"); // untouched
            File.ReadAllText(C(temp)).Should().Be("C");
            File.Exists(B(temp)).Should().BeFalse();
            CountManifests(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)).Should().Be(2);
        });
    }

    [Fact]
    public void Unknown_id_exits_3()
    {
        WithRepo(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            var result = TestSupport.Run(temp, TestSupport.Cli, "restore", "99");
            result.Code.Should().Be(3);
            result.Error.Should().Contain("unknown");
        });
    }

    [Fact]
    public void Path_selector_restores_one_file_and_leaves_the_rest_untouched()
    {
        WithRepo(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.WriteAllText(A(temp), "A2");
            File.WriteAllText(B(temp), "B2");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            TestSupport.Run(temp, TestSupport.Cli, "restore", "1", "--path", "_repolore/architecture/a.md").Code.Should().Be(0);
            File.ReadAllText(A(temp)).Should().Be("A");
            File.ReadAllText(B(temp)).Should().Be("B2");
        });
    }

    [Fact]
    public void Checkpoint_and_restore_refuse_while_a_restore_is_pending()
    {
        WithRepo(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.WriteAllText(A(temp), "A2");
            File.WriteAllText(B(temp), "B2");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            WritePending(temp, targetId: 1, preOperationId: 2);

            var checkpoint = TestSupport.Run(temp, TestSupport.Cli, "checkpoint");
            checkpoint.Code.Should().Be(3);
            checkpoint.Error.Should().Contain("restore 2");

            var otherRestore = TestSupport.Run(temp, TestSupport.Cli, "restore", "1");
            otherRestore.Code.Should().Be(3);
            otherRestore.Error.Should().Contain("restore 2");
        });
    }

    [Fact]
    public void Restore_pre_operation_id_recovers_the_before_state_and_clears_pending()
    {
        WithRepo(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0); // id 1: A, B
            File.WriteAllText(A(temp), "A2");
            File.WriteAllText(B(temp), "B2");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0); // id 2: A2, B2

            WritePending(temp, targetId: 1, preOperationId: 2);
            File.WriteAllText(A(temp), "A"); // simulate fully-applied writes before the crash
            File.WriteAllText(B(temp), "B");

            var recovered = TestSupport.Run(temp, TestSupport.Cli, "restore", "2");
            recovered.Code.Should().Be(0);
            TestSupport.Normalize(recovered.Out).Should().Contain("recovered");
            File.ReadAllText(A(temp)).Should().Be("A2");
            File.ReadAllText(B(temp)).Should().Be("B2");
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void A_fresh_edit_during_recovery_is_detected_not_overwritten()
    {
        WithRepo(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);
            File.WriteAllText(A(temp), "A2");
            File.WriteAllText(B(temp), "B2");
            TestSupport.Run(temp, TestSupport.Cli, "checkpoint").Code.Should().Be(0);

            WritePending(temp, targetId: 1, preOperationId: 2);
            File.WriteAllText(A(temp), "A");
            File.WriteAllText(B(temp), "B");
            File.WriteAllText(A(temp), "A_FRESH_EDIT"); // unrelated edit

            var recovered = TestSupport.Run(temp, TestSupport.Cli, "restore", "2");
            recovered.Code.Should().Be(3);
            recovered.Error.Should().Contain("edited");
            File.ReadAllText(A(temp)).Should().Be("A_FRESH_EDIT");
            File.Exists(PendingPath(temp)).Should().BeTrue(); // still pending
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
