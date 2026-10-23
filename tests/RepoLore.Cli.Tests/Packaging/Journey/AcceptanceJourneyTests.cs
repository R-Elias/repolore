using System.Text.Json;
using FluentAssertions;
using Xunit;
using static RepoLore.Cli.Tests.Packaging.Journey.JourneyContent;

namespace RepoLore.Cli.Tests.Packaging.Journey;

[Collection(PackagingCollection.Name)]
public sealed class AcceptanceJourneyTests : IDisposable
{
    private static readonly string[] AllSessionSentinels =
        [SessionARootSentinel, SessionADraftSentinel, SessionAVerifiedSentinel, SessionBRootSentinel];

    private readonly PackedToolFixture package;
    private readonly IsolatedToolEnvironment environment;
    private readonly GitRepository git;

    public AcceptanceJourneyTests(PackedToolFixture package)
    {
        this.package = package;
        environment = new IsolatedToolEnvironment(package.FeedDirectory);
        git = GitRepository.Initialize(environment.ConsumerRepository);
    }

    private string Repository => environment.ConsumerRepository;

    [Fact]
    public void Documented_journey_runs_on_the_installed_package_and_leaves_a_portable_durable_result()
    {
        InstallPinnedTool();
        InitializeKnowledgeBase();
        ApplyRecommendedGitIgnoreEntries();
        CommitDurableBaseline();
        CreateSessionsAAndB();
        CheckpointBothSessions();
        ReadDefaultContext();
        ReadSelectedSessionContext();
        EditSessionAAndCheckpoint();
        RefuseAPromotionThatDependsOnTheSession();
        PromoteTheVerifiedFactAndCheckpoint();
        ReviewTheIntendedGitDiffAndCommit();
        RestoreSessionAPreviousFile();
        CleanUpSessionAExplicitly();
        AssertNoSessionOrHistoryPathEverReachedGit();
        FreshCloneWorksWithoutSessionsOrHistory();
    }

    private void InstallPinnedTool()
    {
        environment.UseEmptyPackageCache();
        environment.PinTool(package.PackageId, package.PackageVersion);
        var restore = environment.RestoreTools();
        restore.Code.Should().Be(0, restore.Out + restore.Error);

        using var version = JsonDocument.Parse(ToolSucceeds("version", "--json"));
        version.RootElement.GetProperty("version").GetString().Should().Be(package.SourceVersion);
    }

    private void InitializeKnowledgeBase()
    {
        ToolSucceeds("init").Should().Contain("checkpoint 0000000000000001").And.Contain("recommended .gitignore additions");
        FormatMarker.AssertHashIn(Repository);
        git.StatusLines().Should().Contain(line => line.StartsWith("?? _repolore/.history/", StringComparison.Ordinal),
            "negative control: until the ignore entries are applied, Git sees local history");
    }

    private void ApplyRecommendedGitIgnoreEntries()
    {
        using var second = JsonDocument.Parse(ToolSucceeds("init", "--json"));
        var entries = second.RootElement.GetProperty("recommendedGitExclusions").EnumerateArray().Select(entry => entry.GetString()!);
        File.WriteAllText(GitIgnore.FullPath(Repository), string.Concat(entries.Select(entry => entry + "\n")));
        GitIgnore.AssertHashIn(Repository);

        git.IsIgnored("_repolore/.history/write.lock").Should().BeTrue();
        git.IsIgnored(SessionARootPath).Should().BeTrue();
        git.IsIgnored(DurableNotePath).Should().BeFalse("ignore entries must not swallow durable knowledge");
    }

    private void CommitDurableBaseline()
    {
        RetryPolicy.WriteTo(Repository);
        DurableBaseline.WriteTo(Repository);
        ToolSucceeds("health-check").Should().Contain("healthy");

        git.Run("add", "--all");
        git.StagedFiles().Should().BeEquivalentTo(
        [
            ".config/dotnet-tools.json", ".gitignore", "_repolore/method.md", "_repolore/repolore.json",
            "_repolore/root.md", DurableNotePath, "src/retry.txt",
        ]);
        git.CommitAll("Baseline: tool manifest, ignore entries, durable knowledge");
    }

    private void CreateSessionsAAndB()
    {
        SessionARoot.WriteTo(Repository);
        SessionAInvestigationDraft.WriteTo(Repository);
        SessionBRoot.WriteTo(Repository);

        git.StatusLines().Should().BeEmpty("Gitignored sessions are invisible to Git");
        git.IsIgnored(SessionBRootPath).Should().BeTrue();
    }

    private void CheckpointBothSessions()
    {
        ToolSucceeds("checkpoint").Should().Contain("checkpoint 0000000000000002").And.Contain("4 added");

        using var history = JsonDocument.Parse(ToolSucceeds("history", "--json"));
        var newest = history.RootElement.GetProperty("checkpoints")[0];
        newest.GetProperty("id").GetInt32().Should().Be(2);
        newest.GetProperty("files").EnumerateArray().Select(file => file.GetString()).Should()
            .Contain([SessionARootPath, SessionAInvestigationPath, SessionBRootPath, DurableNotePath]);
    }

    private void ReadDefaultContext()
    {
        var output = ToolSucceeds("context", "src/", "--json");

        IncludedPaths(output).Should().Equal("_repolore/root.md", DurableNotePath);
        output.Should().Contain(DurableBaselineSentinel);
        AssertContainsNone(output, AllSessionSentinels);
    }

    private void ReadSelectedSessionContext()
    {
        var output = ToolSucceeds("context", "src/", "--session", SessionAId, "--json");

        IncludedPaths(output).Should().Equal("_repolore/root.md", DurableNotePath, SessionARootPath);
        output.Should().Contain(DurableBaselineSentinel).And.Contain(SessionARootSentinel).And.Contain($"session:{SessionAId}");
        AssertContainsNone(output, SessionADraftSentinel, SessionBRootSentinel);

        var withNote = ToolSucceeds("context", "src/", "--session", SessionAId, "--node", SessionAInvestigationPath, "--json");
        IncludedPaths(withNote).Should().Equal("_repolore/root.md", DurableNotePath, SessionARootPath, SessionAInvestigationPath);
        withNote.Should().Contain(SessionADraftSentinel);

        var crossSession = environment.RunTool("context", "src/", "--session", SessionAId, "--node", SessionBRootPath);
        crossSession.Code.Should().Be(2, "negative control: session A must not read session B");
        crossSession.Out.Should().NotContain(SessionBRootSentinel);
    }

    private void EditSessionAAndCheckpoint()
    {
        SessionAInvestigationVerified.WriteTo(Repository);

        ToolSucceeds("checkpoint").Should().Contain("checkpoint 0000000000000003").And.Contain("1 changed");
    }

    private void RefuseAPromotionThatDependsOnTheSession()
    {
        File.WriteAllText(DurableBaseline.FullPath(Repository), DurableBaseline.Text + LeakyPromotionSuffix);

        var health = environment.RunTool("health-check", "--json");
        health.Code.Should().Be(1, "negative control: durable guidance must not depend on a local-only session");
        health.Out.Should().Contain("session-link-in-durable-note");
    }

    private void PromoteTheVerifiedFactAndCheckpoint()
    {
        DurablePromoted.WriteTo(Repository);
        DurablePromoted.Text.Should().NotContain("SESSION-").And.NotContain(SessionAId);

        ToolSucceeds("health-check").Should().Contain("healthy");
        ToolSucceeds("checkpoint").Should().Contain("checkpoint 0000000000000004").And.Contain("1 changed");
    }

    private void ReviewTheIntendedGitDiffAndCommit()
    {
        git.StatusLines().Should().Equal($" M {DurableNotePath}");
        git.ChangedFiles().Should().Equal(DurableNotePath);
        var diff = git.Diff();
        diff.Should().Contain(PromotedFactSentinel);
        AssertContainsNone(diff, [SessionAId, SessionBId, .. AllSessionSentinels]);

        git.CommitAll("Promote the verified retry limit");
    }

    private void RestoreSessionAPreviousFile()
    {
        var dryRun = ToolSucceeds("restore", "2", "--path", SessionAInvestigationPath, "--dry-run");
        dryRun.Should().Contain("replace");
        SessionAInvestigationVerified.AssertHashIn(Repository);

        ToolSucceeds("restore", "2", "--path", SessionAInvestigationPath).Should().Contain("checkpoint 0000000000000005");

        SessionAInvestigationDraft.AssertHashIn(Repository);
        SessionARoot.AssertHashIn(Repository);
        SessionBRoot.AssertHashIn(Repository);
        DurablePromoted.AssertHashIn(Repository);
        git.StatusLines().Should().BeEmpty();
        ToolSucceeds("restore", "4", "--path", SessionAInvestigationPath, "--dry-run")
            .Should().Contain("replace", "the overwritten verified version stays recoverable");
    }

    private void CleanUpSessionAExplicitly()
    {
        SessionARootComplete.WriteTo(Repository);
        ToolSucceeds("checkpoint").Should().Contain("checkpoint 0000000000000006");

        Directory.Delete(Path.Combine(Repository, "_repolore", "sessions", SessionAId), recursive: true);
        ToolSucceeds("checkpoint").Should().Contain("checkpoint 0000000000000007").And.Contain("2 removed");

        var sessions = ToolSucceeds("tree", "--start", "_repolore/sessions/");
        sessions.Should().Contain(SessionBId).And.NotContain(SessionAId);
        ToolSucceeds("restore", "6", "--path", SessionAInvestigationPath, "--dry-run")
            .Should().Contain("add", "deleting a session does not erase its retained snapshots");
        SessionBRoot.AssertHashIn(Repository);
        git.StatusLines().Should().BeEmpty();
    }

    private void AssertNoSessionOrHistoryPathEverReachedGit()
    {
        var everything = git.TrackedFiles().Concat(git.PathsInAllHistory());
        everything.Should().NotContain(path => path.Contains("sessions") || path.Contains(".history"));
    }

    private void FreshCloneWorksWithoutSessionsOrHistory()
    {
        var clone = GitRepository.Clone(git, environment.PathForNewRepository("fresh-clone"));
        AssertNoLocalOnlyState(clone.Path);

        environment.UseEmptyPackageCache();
        var restore = environment.RestoreToolsIn(clone.Path);
        restore.Code.Should().Be(0, "a teammate restores the committed manifest from the approved feed: " + restore.Out + restore.Error);
        DurablePromoted.AssertHashIn(clone.Path);
        FormatMarker.AssertHashIn(clone.Path);
        GitIgnore.AssertHashIn(clone.Path);

        var context = ToolSucceedsIn(clone.Path, "context", "src/", "--json");
        IncludedPaths(context).Should().Equal("_repolore/root.md", DurableNotePath);
        context.Should().Contain(PromotedFactSentinel);
        AssertContainsNone(context, AllSessionSentinels);
        ToolSucceedsIn(clone.Path, "health-check").Should().Contain("healthy");
        ToolSucceedsIn(clone.Path, "history").Trim().Should().Be("no checkpoints");

        var missingSession = environment.RunToolIn(clone.Path, "context", "src/", "--session", SessionAId, "--json");
        missingSession.Code.Should().Be(1);
        missingSession.Out.Should().Contain("invalid-session");
        AssertNoLocalOnlyState(clone.Path);

        ToolSucceedsIn(clone.Path, "checkpoint").Should().Contain("checkpoint 0000000000000001");
    }

    private static void AssertNoLocalOnlyState(string repository)
    {
        Directory.Exists(Path.Combine(repository, "_repolore", "sessions")).Should().BeFalse();
        Directory.Exists(Path.Combine(repository, "_repolore", ".history")).Should().BeFalse();
    }

    private string ToolSucceeds(params string[] arguments) => ToolSucceedsIn(Repository, arguments);

    private string ToolSucceedsIn(string repository, params string[] arguments)
    {
        var result = environment.RunToolIn(repository, arguments);
        result.Code.Should().Be(0, $"repolore {string.Join(' ', arguments)}:\n{result.Out}\n{result.Error}");
        return result.Out;
    }

    private static string[] IncludedPaths(string contextJson)
    {
        using var document = JsonDocument.Parse(contextJson);
        return document.RootElement.GetProperty("included").EnumerateArray()
            .Select(note => note.GetProperty("path").GetString()!).ToArray();
    }

    private static void AssertContainsNone(string text, params string[] forbidden)
    {
        foreach (var value in forbidden) text.Should().NotContain(value);
    }

    public void Dispose() => environment.Dispose();
}
