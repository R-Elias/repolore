using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace RepoLore.Cli.Tests.Packaging;

[Collection(PackagingCollection.Name)]
public sealed class InstalledToolTests(PackedToolFixture package)
{
    private const string FirstSentinel = "# src\nINSTALLED-TOOL-SENTINEL-FIRST\n";
    private const string SecondSentinel = "# src\nINSTALLED-TOOL-SENTINEL-SECOND\n";

    [Fact]
    public void Pinned_manifest_restores_from_a_local_only_feed_into_an_empty_cache_and_runs_the_whole_workflow()
    {
        using var environment = new IsolatedToolEnvironment(package.FeedDirectory);
        environment.UseEmptyPackageCache();
        environment.PinTool(package.PackageId, package.PackageVersion);

        var restore = environment.RestoreTools();
        restore.Code.Should().Be(0, restore.Out + restore.Error);
        var installedAssembly = Path.Combine(environment.PackageCache, package.PackageId.ToLowerInvariant(), package.PackageVersion,
            "tools", "net10.0", "any", "RepoLore.Cli.dll");
        File.Exists(installedAssembly).Should().BeTrue("the tool must run from the restored package, not from build outputs");

        using var version = JsonDocument.Parse(environment.RunTool("version", "--json").Out);
        version.RootElement.GetProperty("version").GetString().Should().Be(package.SourceVersion);

        var init = environment.RunTool("init");
        init.Code.Should().Be(0, init.Out + init.Error);
        init.Out.Should().Contain("checkpoint 0000000000000001");

        var note = Path.Combine(environment.ConsumerRepository, "_repolore", "sparse-tree", "src", "src.md");
        Directory.CreateDirectory(Path.Combine(environment.ConsumerRepository, "src"));
        Directory.CreateDirectory(Path.GetDirectoryName(note)!);
        File.WriteAllText(note, FirstSentinel);

        var context = environment.RunTool("context", "src/", "--json");
        context.Code.Should().Be(0, context.Out + context.Error);
        context.Out.Should().Contain("INSTALLED-TOOL-SENTINEL-FIRST");

        environment.RunTool("checkpoint").Out.Should().Contain("checkpoint 0000000000000002");
        File.WriteAllText(note, SecondSentinel);
        environment.RunTool("checkpoint").Out.Should().Contain("checkpoint 0000000000000003");

        using var history = JsonDocument.Parse(environment.RunTool("history", "--json").Out);
        history.RootElement.GetProperty("checkpoints").EnumerateArray().Select(item => item.GetProperty("id").GetInt32())
            .Should().Equal(3, 2, 1);

        var restore2 = environment.RunTool("restore", "2");
        restore2.Code.Should().Be(0, restore2.Out + restore2.Error);
        File.ReadAllText(note).Should().Be(FirstSentinel);

        var health = environment.RunTool("health-check", "--json");
        health.Code.Should().Be(0, health.Out + health.Error);
        health.Out.Should().Contain("\"healthy\":true");
    }

    [Theory]
    [InlineData("RepoLore.Cli", null)]
    [InlineData("dotnetsay", "2.1.7")]
    public void Empty_local_feed_with_an_empty_cache_fails_instead_of_finding_a_cached_or_public_copy(string packageId, string? version)
    {
        var emptyFeed = Path.Combine(Path.GetTempPath(), "repolore-empty-feed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyFeed);
        try
        {
            using var environment = new IsolatedToolEnvironment(emptyFeed);
            environment.UseEmptyPackageCache();
            environment.PinTool(packageId, version ?? package.PackageVersion);

            var restore = environment.RestoreTools();

            restore.Code.Should().NotBe(0, restore.Out + restore.Error);
            Directory.Exists(Path.Combine(environment.PackageCache, packageId.ToLowerInvariant())).Should().BeFalse();
            environment.RunTool("version").Code.Should().NotBe(0);
        }
        finally
        {
            Directory.Delete(emptyFeed, recursive: true);
        }
    }
}
