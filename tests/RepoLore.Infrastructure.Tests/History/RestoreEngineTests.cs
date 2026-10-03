using FluentAssertions;
using RepoLore.Core.Configuration;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Infrastructure.Tests.History;

public class RestoreEngineTests
{
    private const string Root = "_repolore/root.md";
    private const string A = "_repolore/architecture/a.md";
    private const string B = "_repolore/architecture/b.md";
    private const string C = "_repolore/architecture/c.md";

    [Fact]
    public void Restore_to_an_old_snapshot_then_restore_its_pre_operation_id_undoes_it_byte_for_byte()
    {
        WithRepo(temp =>
        {
            Capture(temp).Capture(Load(temp)); // id 1: ROOT, A, B

            Write(temp, A, "A_EDITED");
            Write(temp, C, "C");
            Delete(temp, B);
            Capture(temp).Capture(Load(temp)); // id 2: edited state

            var restore = Restore(temp).Apply(Load(temp), 1);
            restore.WasNoOp.Should().BeFalse();
            Read(temp, A).Should().Be("A");
            Read(temp, B).Should().Be("B");
            Exists(temp, C).Should().BeFalse();
            Read(temp, Root).Should().Be("ROOT");

            var undo = Restore(temp).Apply(Load(temp), 2);
            undo.WasNoOp.Should().BeFalse();
            Read(temp, A).Should().Be("A_EDITED");
            Read(temp, C).Should().Be("C");
            Exists(temp, B).Should().BeFalse();
        });
    }

    [Fact]
    public void An_excluded_file_stays_untouched()
    {
        WithRepo(temp =>
        {
            Capture(temp).Capture(Load(temp)); // id 1

            Write(temp, A, "A_EDITED");
            Write(temp, B, "B_EDITED");
            Capture(temp).Capture(Load(temp)); // id 2

            SetExclude(temp, new[] { B });
            Restore(temp).Apply(Load(temp), 1);

            Read(temp, A).Should().Be("A"); // restored
            Read(temp, B).Should().Be("B_EDITED"); // excluded, untouched
        });
    }

    [Fact]
    public void Unknown_id_fails_before_any_mutation()
    {
        WithRepo(temp =>
        {
            Capture(temp).Capture(Load(temp));
            var ex = new Action(() => Restore(temp).Apply(Load(temp), 99)).Should().Throw<HistoryStoreException>().Which;
            ex.Message.Should().Contain("unknown");
            Read(temp, Root).Should().Be("ROOT");
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void A_corrupt_target_object_fails_before_any_mutation()
    {
        WithRepo(temp =>
        {
            Capture(temp).Capture(Load(temp)); // id 1
            Write(temp, A, "A_EDITED");
            Capture(temp).Capture(Load(temp)); // id 2

            CorruptObject(temp, HashOf("A")); // corrupt the original A object

            var ex = new Action(() => Restore(temp).Apply(Load(temp), 1)).Should().Throw<HistoryStoreException>().Which;
            ex.Message.Should().Contain("corrupt");
            Read(temp, A).Should().Be("A_EDITED");
        });
    }

    [Fact]
    public void Disabled_history_refuses_restore()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
            var ex = new Action(() => Restore(temp).Apply(Load(temp), 1)).Should().Throw<HistoryStoreException>().Which;
            ex.Message.Should().Contain("disabled");
        });
    }

    [Fact]
    public void A_no_op_restore_creates_no_pending_and_no_new_checkpoint()
    {
        WithRepo(temp =>
        {
            Capture(temp).Capture(Load(temp)); // id 1
            var result = Restore(temp).Apply(Load(temp), 1);
            result.WasNoOp.Should().BeTrue();
            File.Exists(PendingPath(temp)).Should().BeFalse();
            HighestId(temp).Should().Be(1L);
        });
    }

    [Fact]
    public void Failure_before_the_first_replacement_reports_the_pre_operation_id_and_recovers()
    {
        WithTempWithTwoCaptures(temp =>
        {
            var ex = new Action(() =>
                Restore(temp).Apply(Load(temp), 1, beforeFirstReplacement: () => throw new InvalidOperationException("injected")))
                .Should().Throw<RestoreException>().Which;

            ex.RecoveryId.Should().Be(2L);
            File.Exists(PendingPath(temp)).Should().BeTrue();
            Read(temp, A).Should().Be("A2");
            Read(temp, B).Should().Be("B2");

            Recover(temp, 2L);
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void Failure_between_two_replacements_reports_the_same_recovery_id_and_recovers()
    {
        WithTempWithTwoCaptures(temp =>
        {
            var ex = new Action(() =>
                Restore(temp).Apply(Load(temp), 1, afterFirstReplacement: () => throw new InvalidOperationException("injected")))
                .Should().Throw<RestoreException>().Which;

            ex.RecoveryId.Should().Be(2L);
            File.Exists(PendingPath(temp)).Should().BeTrue();

            Recover(temp, 2L);
            Read(temp, A).Should().Be("A2");
            Read(temp, B).Should().Be("B2");
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void Failure_after_writes_but_before_the_final_checkpoint_reports_the_same_recovery_id_and_recovers()
    {
        WithTempWithTwoCaptures(temp =>
        {
            var ex = new Action(() =>
                Restore(temp).Apply(Load(temp), 1, beforePostCheckpoint: () => throw new InvalidOperationException("injected")))
                .Should().Throw<RestoreException>().Which;

            ex.RecoveryId.Should().Be(2L);
            File.Exists(PendingPath(temp)).Should().BeTrue();
            Read(temp, A).Should().Be("A"); // writes already applied
            Read(temp, B).Should().Be("B");

            Recover(temp, 2L);
            Read(temp, A).Should().Be("A2");
            Read(temp, B).Should().Be("B2");
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void Restart_and_recover_restores_the_exact_before_state()
    {
        WithTempWithTwoCaptures(temp =>
        {
            var ex = new Action(() =>
                Restore(temp).Apply(Load(temp), 1, beforePostCheckpoint: () => throw new InvalidOperationException("injected")))
                .Should().Throw<RestoreException>().Which;

            ex.RecoveryId.Should().Be(2L);

            // fresh engine simulates a restart; recover via the recorded pre-operation id
            var recovered = Restore(temp).Recover(2L);
            recovered.WasRecovery.Should().BeTrue();
            Read(temp, A).Should().Be("A2");
            Read(temp, B).Should().Be("B2");
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void A_fresh_unrelated_edit_during_recovery_is_detected_not_overwritten()
    {
        WithTempWithTwoCaptures(temp =>
        {
            new Action(() =>
                Restore(temp).Apply(Load(temp), 1, beforePostCheckpoint: () => throw new InvalidOperationException("injected")))
                .Should().Throw<RestoreException>();

            Write(temp, A, "A_FRESH_EDIT");

            var ex = new Action(() => Restore(temp).Recover(2L)).Should().Throw<HistoryStoreException>().Which;
            ex.Message.Should().Contain("edited");
            Read(temp, A).Should().Be("A_FRESH_EDIT"); // untouched
            File.Exists(PendingPath(temp)).Should().BeTrue(); // still pending, retryable
        });
    }

    [Fact]
    public void Path_selector_restores_one_file_and_leaves_the_rest_untouched()
    {
        WithTempWithTwoCaptures(temp =>
        {
            Restore(temp).Apply(Load(temp), 1, A);
            Read(temp, A).Should().Be("A");
            Read(temp, B).Should().Be("B2");
        });
    }

    [Fact]
    public void Restore_preflights_the_protected_set_against_the_budget_before_mutating()
    {
        WithRepo(temp =>
        {
            Capture(temp).Capture(Load(temp)); // id 1
            Write(temp, A, "A2");
            Write(temp, B, "B2");
            Capture(temp).Capture(Load(temp)); // id 2

            SetMaxBytes(temp, 100);
            var ex = new Action(() => Restore(temp).Apply(Load(temp), 1)).Should().Throw<HistoryStoreException>().Which;
            ex.Message.Should().Contain("exceeds");
            Read(temp, A).Should().Be("A2");
            Read(temp, B).Should().Be("B2");
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    private static void Recover(string temp, long id)
    {
        var result = Restore(temp).Recover(id);
        result.WasRecovery.Should().BeTrue();
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
        TempDir.WithTemp(temp =>
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
