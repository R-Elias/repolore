using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests;

public class InitTests
{
    [Fact]
    public void Empty_directory_initializes_with_one_baseline_and_no_source_placeholders()
    {
        TestSupport.WithTemp(temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "init");
            result.Code.Should().Be(0);

            File.ReadAllText(Path.Combine(temp, "_repolore", "repolore.json")).Should().Be("{\"formatVersion\":1}");
            var method = File.ReadAllText(Path.Combine(temp, "_repolore", "method.md"));
            method.Should().StartWith("# RepoLore Method");
            File.Exists(Path.Combine(temp, "_repolore", "root.md")).Should().BeTrue();
            var sparse = Path.Combine(temp, "_repolore", "sparse-tree");
            Directory.Exists(sparse).Should().BeTrue();
            Directory.EnumerateFileSystemEntries(sparse).Count().Should().Be(0);

            var history = TestSupport.Run(temp, TestSupport.Cli, "history");
            CountManifestLines(TestSupport.Normalize(history.Out)).Should().Be(1);
        });
    }

    [Fact]
    public void Init_writes_lf_templates_whatever_line_endings_the_source_was_checked_out_with()
    {
        // A Windows checkout gives EmbeddedTemplates.cs CRLF line endings; found by the init-reproduces-the-adoption
        // scenario on Windows (2026-10-24).
        TestSupport.WithTemp(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "init").Code.Should().Be(0);

            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().NotContain("\r");
            File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")).Should().NotContain("\r");
        });
    }

    [Fact]
    public void Second_init_changes_nothing()
    {
        TestSupport.WithTemp(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "init").Code.Should().Be(0);
            var second = TestSupport.Run(temp, TestSupport.Cli, "init");
            second.Code.Should().Be(0);
            TestSupport.Normalize(second.Out).Should().Contain("nothing to create");
            CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)).Should().Be(1);
        });
    }

    [Fact]
    public void Preexisting_root_config_custom_notes_survive_exactly()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "MY_ROOT_SENTINEL");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md"), "MY_CUSTOM_SENTINEL");

            TestSupport.Run(temp, TestSupport.Cli, "init").Code.Should().Be(0);

            File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")).Should().Be("MY_ROOT_SENTINEL");
            File.ReadAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md")).Should().Be("MY_CUSTOM_SENTINEL");
        });
    }

    [Fact]
    public void A_nonempty_alpha_sparse_only_clone_is_never_emptied()
    {
        TestSupport.WithFixture("alpha-sparse-only", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "init");
            result.Code.Should().Be(3);
            result.Error.Should().Contain("migrate");

            TestSupport.Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md")))
                .Should().Be("ALPHA_SPARSE_ONLY_SENTINEL\n");
            File.Exists(Path.Combine(temp, "_repolore", "root.md")).Should().BeTrue();
            File.Exists(Path.Combine(temp, "_repolore", "repolore.json")).Should().BeFalse();
        });
    }

    [Fact]
    public void A_session_folder_created_before_init_makes_init_refuse_and_change_nothing()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sessions", "early-session"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "sessions", "early-session", "root.md"), "EARLY_SESSION_SENTINEL");
            var before = TestSupport.Snapshot(temp);

            var result = TestSupport.Run(temp, TestSupport.Cli, "init");

            result.Code.Should().Be(3);
            result.Error.Should().Contain("no format marker").And.Contain("move _repolore/sessions/ aside").And.Contain("migrate --check");
            TestSupport.Snapshot(temp).Should().Be(before);
        });
    }

    [Fact]
    public void Moving_the_early_session_aside_lets_init_succeed_and_the_session_returns_unchanged()
    {
        TestSupport.WithTemp(temp =>
        {
            var sessions = Path.Combine(temp, "_repolore", "sessions");
            Directory.CreateDirectory(Path.Combine(sessions, "early-session"));
            File.WriteAllText(Path.Combine(sessions, "early-session", "root.md"), "EARLY_SESSION_SENTINEL");
            var aside = Path.Combine(temp, "sessions-aside");
            Directory.Move(sessions, aside);

            TestSupport.Run(temp, TestSupport.Cli, "init").Code.Should().Be(0);
            Directory.Move(aside, sessions);

            TestSupport.Run(temp, TestSupport.Cli, "context", "--session", "early-session").Out.Should().Contain("EARLY_SESSION_SENTINEL");
            TestSupport.Run(temp, TestSupport.Cli, "health-check").Code.Should().Be(0);
        });
    }

    [Fact]
    public void A_user_edited_method_is_never_replaced_during_ordinary_init()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "USER_METHOD_SENTINEL");

            TestSupport.Run(temp, TestSupport.Cli, "init").Code.Should().Be(0);
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be("USER_METHOD_SENTINEL");
        });
    }

    [Fact]
    public void Method_update_can_be_undone_to_exact_old_bytes()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
            File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD_SENTINEL");
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

            var update = TestSupport.Run(temp, TestSupport.Cli, "init", "--update-method");
            update.Code.Should().Be(0);
            update.Out.Should().Contain("updated _repolore/method.md; undo with 'repolore restore 1'");
            var method = File.ReadAllText(Path.Combine(temp, "_repolore", "method.md"));
            method.Should().StartWith("# RepoLore Method");
            method.Should().NotContain("OLD_METHOD_SENTINEL");

            var undo = TestSupport.Run(temp, TestSupport.Cli, "restore", "1");
            undo.Code.Should().Be(0);
            File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")).Should().Be("OLD_METHOD_SENTINEL");
        });
    }

    [Fact]
    public void Init_below_an_existing_knowledge_base_refuses_unless_the_root_is_explicit()
    {
        TestSupport.WithTemp(temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "init").Code.Should().Be(0);
            var nested = Path.Combine(temp, "services", "orders");
            Directory.CreateDirectory(nested);

            var refused = TestSupport.Run(nested, TestSupport.Cli, "init", "--json");
            refused.Code.Should().Be(2);
            refused.Out.Should().Contain("\"usage\"").And.Contain("a knowledge base already exists in");
            Directory.Exists(Path.Combine(nested, "_repolore")).Should().BeFalse();

            TestSupport.Run(nested, TestSupport.Cli, "init", "--repo-root", ".").Code.Should().Be(0);
            File.Exists(Path.Combine(nested, "_repolore", "repolore.json")).Should().BeTrue();
            TestSupport.Run(nested, TestSupport.Cli, "init").Code.Should().Be(0, "init in the directory that holds the knowledge base stays idempotent");
        });
    }

    [Fact]
    public void Init_with_disabled_history_creates_files_and_states_protection_disabled()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");

            var result = TestSupport.Run(temp, TestSupport.Cli, "init");
            result.Code.Should().Be(0);
            TestSupport.Normalize(result.Out).Should().Contain("disabled");
            File.Exists(Path.Combine(temp, "_repolore", "method.md")).Should().BeTrue();
            CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)).Should().Be(0);
        });
    }

    private static int CountManifestLines(string text)
    {
        var count = 0;
        foreach (var line in text.Split('\n'))
            if (line.StartsWith("000000000000000", StringComparison.Ordinal))
                count++;
        return count;
    }
}
