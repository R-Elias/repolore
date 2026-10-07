using FluentAssertions;
using RepoLore.Core.Migration;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Infrastructure.Tests.History;

public class MigrationEngineTests
{
    [Fact]
    public void Sparse_only_clone_migrates_preserving_every_note_and_marks_v1()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/root.md", "ROOT");
            Write(temp, "_repolore/sparse-tree/src/src.md", "SPARSE_ONLY");

            var result = Apply(temp);

            Read(temp, "_repolore/repolore.json").Should().Be("{\"formatVersion\":1}");
            Read(temp, "_repolore/root.md").Should().Be("ROOT");
            Read(temp, "_repolore/sparse-tree/src/src.md").Should().Be("SPARSE_ONLY");
            Directory.Exists(Path.Combine(temp, "_repolore", "tree")).Should().BeFalse();
            Engine(temp).Classify().Should().Be(InitDirKind.V1);
        });
    }

    [Fact]
    public void Local_only_tree_note_is_copied_and_the_legacy_tree_removed()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/root.md", "ROOT");
            Write(temp, "_repolore/tree/src/src.md", "TREE_ONLY");

            Apply(temp);

            Read(temp, "_repolore/sparse-tree/src/src.md").Should().Be("TREE_ONLY");
            Directory.Exists(Path.Combine(temp, "_repolore", "tree")).Should().BeFalse();
            Read(temp, "_repolore/root.md").Should().Be("ROOT");
        });
    }

    [Fact]
    public void Identical_copies_keep_the_sparse_copy_and_drop_the_tree_copy()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/root.md", "ROOT");
            Write(temp, "_repolore/tree/src/src.md", "SAME");
            Write(temp, "_repolore/sparse-tree/src/src.md", "SAME");

            Apply(temp);

            Read(temp, "_repolore/sparse-tree/src/src.md").Should().Be("SAME");
            Directory.Exists(Path.Combine(temp, "_repolore", "tree")).Should().BeFalse();
        });
    }

    [Fact]
    public void Differing_copies_conflict_and_write_nothing()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/root.md", "ROOT");
            Write(temp, "_repolore/tree/src/src.md", "TREE_VALUE");
            Write(temp, "_repolore/sparse-tree/src/src.md", "SPARSE_VALUE");

            var ex = new Action(() => Apply(temp)).Should().Throw<MigrationException>().Which;

            ex.Message.Should().Contain("conflict");
            File.Exists(Path.Combine(temp, "_repolore", "repolore.json")).Should().BeFalse();
            Read(temp, "_repolore/tree/src/src.md").Should().Be("TREE_VALUE");
            Read(temp, "_repolore/sparse-tree/src/src.md").Should().Be("SPARSE_VALUE");
        });
    }

    [Fact]
    public void Non_markdown_legacy_content_is_left_in_place_and_reported()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/root.md", "ROOT");
            Write(temp, "_repolore/tree/src/src.md", "TREE_ONLY");
            Write(temp, "_repolore/tree/notes.bin", "BINARY");

            var inventory = Engine(temp).Inventory();
            inventory.UnknownFiles.Should().Contain("_repolore/tree/notes.bin");

            var result = Apply(temp);

            File.Exists(Path.Combine(temp, "_repolore", "tree", "notes.bin")).Should().BeTrue();
            result.LeftBehind.Should().Contain("_repolore/tree/notes.bin");
            File.Exists(Path.Combine(temp, "_repolore", "tree", "src", "src.md")).Should().BeFalse();
        });
    }

    [Fact]
    public void Restoring_the_migration_baseline_restores_old_bytes_and_marker_absence()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/root.md", "ROOT");
            Write(temp, "_repolore/tree/src/src.md", "TREE_ONLY");

            Apply(temp); // baseline id 1, post id 2

            var restored = new RestoreEngine(temp, HistoryDir(temp), Clock()).Apply(HistoryConfigLoader.Load(temp), 1);
            restored.WasNoOp.Should().BeFalse();

            Read(temp, "_repolore/tree/src/src.md").Should().Be("TREE_ONLY");
            File.Exists(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "_repolore", "repolore.json")).Should().BeFalse();
            Read(temp, "_repolore/root.md").Should().Be("ROOT");
        });
    }

    [Fact]
    public void Mid_migration_failure_reports_the_baseline_id_and_recovers()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/root.md", "ROOT");
            Write(temp, "_repolore/tree/src/src.md", "TREE_ONLY");

            var ex = new Action(() =>
                Apply(temp, afterFirstReplacement: () => throw new InvalidOperationException("injected")))
                .Should().Throw<MigrationException>().Which;

            ex.RecoveryId.Should().NotBeNull();
            File.Exists(PendingPath(temp)).Should().BeTrue();

            var recovered = new RestoreEngine(temp, HistoryDir(temp), Clock()).Recover(ex.RecoveryId!.Value);
            recovered.WasRecovery.Should().BeTrue();

            Read(temp, "_repolore/tree/src/src.md").Should().Be("TREE_ONLY");
            File.Exists(Path.Combine(temp, "_repolore", "repolore.json")).Should().BeFalse();
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void Tree_root_note_copies_to_the_top_level_root_destination()
    {
        TempDir.WithTemp(temp =>
        {
            Write(temp, "_repolore/tree/root.md", "TREE_ROOT");

            Apply(temp);

            Read(temp, "_repolore/root.md").Should().Be("TREE_ROOT");
            File.Exists(Path.Combine(temp, "_repolore", "tree", "root.md")).Should().BeFalse();
        });
    }

    private static MigrationResult Apply(
        string temp,
        Action? beforeFirstReplacement = null,
        Action? afterFirstReplacement = null,
        Action? beforePostCheckpoint = null)
    {
        var inventory = Engine(temp).Inventory();
        var mappings = MigrationPlanner.Classify(inventory.Notes);
        return Engine(temp).Apply(inventory, mappings, beforeFirstReplacement, afterFirstReplacement, beforePostCheckpoint);
    }

    private static MigrationEngine Engine(string temp) => new(temp, HistoryDir(temp), Clock());

    private static string HistoryDir(string temp) => Path.Combine(temp, "_repolore", ".history");

    private static string PendingPath(string temp) => Path.Combine(HistoryDir(temp), "pending.json");

    private static Clock Clock() => new(() => new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));

    private static void Write(string temp, string relative, string content)
    {
        var path = Path.Combine(temp, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Read(string temp, string relative) => File.ReadAllText(Path.Combine(temp, relative));
}
