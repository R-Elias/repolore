using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests;

public class MigrateTests
{
    [Fact]
    public void Migrate_applies_on_a_sparse_only_clone_and_marks_v1()
    {
        TestSupport.WithFixture("alpha-sparse-only", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "migrate");
            result.Code.Should().Be(0);

            File.ReadAllText(Path.Combine(temp, "_repolore", "repolore.json")).Should().Be("{\"formatVersion\":1}");
            TestSupport.Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md")))
                .Should().Be("ALPHA_SPARSE_ONLY_SENTINEL\n");
            File.Exists(Path.Combine(temp, "_repolore", "root.md")).Should().BeTrue();
            Directory.Exists(Path.Combine(temp, "_repolore", "tree")).Should().BeFalse();
        });
    }

    [Fact]
    public void Rerunning_migrate_is_a_no_op()
    {
        TestSupport.WithFixture("alpha-sparse-only", temp =>
        {
            TestSupport.Run(temp, TestSupport.Cli, "migrate").Code.Should().Be(0);
            var second = TestSupport.Run(temp, TestSupport.Cli, "migrate");
            second.Code.Should().Be(0);
            second.Error.Should().Contain("already");
        });
    }

    [Fact]
    public void Migrate_on_a_conflict_writes_nothing()
    {
        TestSupport.WithFixture("alpha-conflict", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "migrate");
            result.Code.Should().Be(3);

            File.Exists(Path.Combine(temp, "_repolore", "repolore.json")).Should().BeFalse();
            TestSupport.Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "tree", "src", "src.md")))
                .Should().Be("ALPHA_TREE_VARIANT_SENTINEL\n");
            TestSupport.Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md")))
                .Should().Be("ALPHA_SPARSE_VARIANT_SENTINEL\n");
        });
    }

    [Fact]
    public void Migrate_check_reports_a_conflict_with_exit_3()
    {
        TestSupport.WithFixture("alpha-conflict", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "migrate", "--check");
            result.Code.Should().Be(3);
            result.Out.Should().Contain("conflict");
            File.Exists(Path.Combine(temp, "_repolore", "repolore.json")).Should().BeFalse();
        });
    }

    [Fact]
    public void Migrate_dry_run_writes_nothing()
    {
        TestSupport.WithFixture("alpha-local-only", temp =>
        {
            var result = TestSupport.Run(temp, TestSupport.Cli, "migrate", "--dry-run");
            result.Code.Should().Be(0);
            result.Out.Should().Contain("copy");

            File.Exists(Path.Combine(temp, "_repolore", "repolore.json")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "_repolore", "tree", "src", "src.md")).Should().BeTrue();
            File.Exists(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md")).Should().BeFalse();
        });
    }

    [Fact]
    public void Custom_and_session_areas_survive_migration_unchanged()
    {
        TestSupport.WithTemp(temp =>
        {
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sparse-tree", "src"));
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
            Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sessions", "session-a"));
            File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");
            File.WriteAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md"), "NOTE");
            File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md"), "CUSTOM");
            File.WriteAllText(Path.Combine(temp, "_repolore", "sessions", "session-a", "root.md"), "SESSION");

            var result = TestSupport.Run(temp, TestSupport.Cli, "migrate");
            result.Code.Should().Be(0);

            File.ReadAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md")).Should().Be("CUSTOM");
            File.ReadAllText(Path.Combine(temp, "_repolore", "sessions", "session-a", "root.md")).Should().Be("SESSION");
        });
    }
}
