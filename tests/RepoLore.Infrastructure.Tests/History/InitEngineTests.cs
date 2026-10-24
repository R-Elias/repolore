using FluentAssertions;
using RepoLore.Infrastructure.History;
using Xunit;

namespace RepoLore.Infrastructure.Tests.History;

public class InitEngineTests
{
    private const string MethodTemplate = "METHOD_V1_TEMPLATE";
    private const string RootTemplate = "ROOT_TEMPLATE";

    [Fact]
    public void Empty_directory_initializes_with_marker_templates_tree_and_one_baseline()
    {
        TempDir.WithTemp(temp =>
        {
            var result = Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: false);
            result.WasNoOp.Should().BeFalse();
            File.ReadAllText(Path.Combine(temp, "_repolore", "repolore.json")).Should().Be("{\"formatVersion\":1}");
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be(MethodTemplate);
            File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")).Should().Be(RootTemplate);
            Directory.Exists(Path.Combine(temp, "_repolore", "sparse-tree")).Should().BeTrue();
            Directory.EnumerateFileSystemEntries(Path.Combine(temp, "_repolore", "sparse-tree")).Count().Should().Be(0);
            HighestId(temp).Should().Be(1L);
        });
    }

    [Fact]
    public void Second_init_changes_nothing()
    {
        TempDir.WithTemp(temp =>
        {
            Engine(temp).Run(MethodTemplate, RootTemplate, false);
            var result = Engine(temp).Run(MethodTemplate, RootTemplate, false);
            result.WasNoOp.Should().BeTrue();
            result.Created.Should().HaveCount(0);
            HighestId(temp).Should().Be(1L);
        });
    }

    [Fact]
    public void Preexisting_root_config_custom_notes_survive_exactly()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "MY_ROOT");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md"), "MY_CUSTOM");

            Engine(temp).Run(MethodTemplate, RootTemplate, false);

            File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")).Should().Be("MY_ROOT");
            File.ReadAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md")).Should().Be("MY_CUSTOM");
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be(MethodTemplate);
            Directory.Exists(Path.Combine(temp, "_repolore", "sparse-tree")).Should().BeTrue();
        });
    }

    [Fact]
    public void Failure_creating_the_first_checkpoint_is_visible_and_retryable()
    {
        TempDir.WithTemp(temp =>
        {
            var ex = new Action(() =>
                Engine(temp).Run(MethodTemplate, RootTemplate, false,
                    beforeFirstCapture: () => throw new InvalidOperationException("injected")))
                .Should().Throw<InitIncompleteException>().Which;

            ex.Created.Contains("_repolore/repolore.json").Should().BeTrue();
            ex.Created.Contains("_repolore/method.md").Should().BeTrue();
            ex.Created.Contains("_repolore/root.md").Should().BeTrue();
            HighestId(temp).Should().Be(0L);

            Engine(temp).Run(MethodTemplate, RootTemplate, false);
            HighestId(temp).Should().Be(1L);
        });
    }

    [Fact]
    public void Alpha_content_without_a_marker_is_refused_and_never_emptied()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sparse-tree", "src"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ALPHA_ROOT");
            File.WriteAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md"), "ALPHA_NOTE");

            Engine(temp).Classify().Should().Be(InitDirKind.Alpha);
            var ex = new Action(() => Engine(temp).Run(MethodTemplate, RootTemplate, false)).Should().Throw<InitException>().Which;
            ex.Message.Should().Contain("migrate");

            File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")).Should().Be("ALPHA_ROOT");
            File.ReadAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md")).Should().Be("ALPHA_NOTE");
        });
    }

    [Fact]
    public void A_user_edited_method_is_never_replaced_during_ordinary_init()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "USER_METHOD");

            Engine(temp).Run(MethodTemplate, RootTemplate, false);
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be("USER_METHOD");
        });
    }

    [Fact]
    public void Method_update_rewrites_the_method_and_can_be_undone_to_exact_old_bytes()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

            var result = Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true);
            result.MethodUpdated.Should().BeTrue();
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be(MethodTemplate);
            File.Exists(PendingPath(temp)).Should().BeFalse();

            var restored = new RestoreEngine(temp, HistoryDir(temp), Clock()).Apply(HistoryConfigLoader.Load(temp), 1);
            restored.WasNoOp.Should().BeFalse();
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be("OLD_METHOD");
        });
    }

    [Fact]
    public void Unchanged_embedded_method_bytes_are_a_no_op()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sparse-tree"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), MethodTemplate);
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), RootTemplate);

            var result = Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true);
            result.MethodUpdated.Should().BeFalse();
            result.WasNoOp.Should().BeTrue();
            File.Exists(PendingPath(temp)).Should().BeFalse();
        });
    }

    [Fact]
    public void Method_update_with_disabled_history_refuses()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD");

            var ex = new Action(() => Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true)).Should().Throw<InitException>().Which;
            ex.Message.Should().Contain("disabled");
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be("OLD_METHOD");
        });
    }

    [Fact]
    public void Method_update_interrupted_before_write_leaves_pending_and_recovers()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

            var ex = new Action(() =>
                Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true,
                    beforeFirstReplacement: () => throw new InvalidOperationException("injected")))
                .Should().Throw<InitException>().Which;

            ex.RecoveryId.Should().NotBeNull("expected a recovery id");
            File.Exists(PendingPath(temp)).Should().BeTrue();
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be("OLD_METHOD");

            var recovered = new RestoreEngine(temp, HistoryDir(temp), Clock()).Recover(ex.RecoveryId!.Value);
            recovered.WasRecovery.Should().BeTrue();
            File.Exists(PendingPath(temp)).Should().BeFalse();
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be("OLD_METHOD");
        });
    }

    [Fact]
    public void History_disabled_init_reports_protection_disabled_and_skips_checkpoint()
    {
        TempDir.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");

            var result = Engine(temp).Run(MethodTemplate, RootTemplate, false);
            result.HistoryDisabled.Should().BeTrue();
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be(MethodTemplate);
            HighestId(temp).Should().Be(0L);
        });
    }

    private static InitEngine Engine(string temp) => new(temp, HistoryDir(temp), Clock());

    private static string HistoryDir(string temp) => Path.Combine(temp, "_repolore", ".history");

    private static string PendingPath(string temp) => Path.Combine(HistoryDir(temp), "pending.json");

    private static long HighestId(string temp) => new CheckpointStore(HistoryDir(temp)).ReadHighestCompleted()?.Id ?? 0;

    private static Clock Clock() => new(() => new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
}
