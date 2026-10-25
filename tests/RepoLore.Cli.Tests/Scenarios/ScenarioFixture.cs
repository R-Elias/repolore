using FluentAssertions;
using RepoLore.Cli.Tests.Packaging;
using RepoLore.Cli.Tests.Packaging.Journey;
using Xunit;

namespace RepoLore.Cli.Tests.Scenarios;

[CollectionDefinition(Name)]
public sealed class ScenarioCollection : ICollectionFixture<ScenarioFixture>
{
    public const string Name = "Scenarios";
}

// Skips the scenarios unless REPOLORE_REFERENCE_REPO points at a local clone of the reference repository. CI clones
// the pinned version in a build step, so the tests themselves never touch the network.
public sealed class ScenarioTheoryAttribute : TheoryAttribute
{
    public const string ReferenceVariable = "REPOLORE_REFERENCE_REPO";

    public ScenarioTheoryAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ReferenceVariable)))
            Skip = $"set {ReferenceVariable} to a local clone of the reference repository to run the scenarios";
    }
}

// Packs the shipped projects once, restores the exact package into an isolated cache, and checks that the local
// reference clone holds the pinned tag at the pinned commit. With REPOLORE_SCENARIO_EXECUTABLE set, the scenarios run
// that native executable instead, so each platform build is held to the same contract as the NuGet tool.
public sealed class ScenarioFixture : IDisposable
{
    public const string ArtifactsVariable = "REPOLORE_SCENARIO_ARTIFACTS";
    public const string ExecutableVariable = "REPOLORE_SCENARIO_EXECUTABLE";

    private readonly string? executable = Environment.GetEnvironmentVariable(ExecutableVariable) is { Length: > 0 } path
        ? Path.GetFullPath(path)
        : null;
    private readonly string workRoot = Path.Combine(Path.GetTempPath(), "repolore-scenarios-" + Guid.NewGuid().ToString("N"));
    private readonly PackedToolFixture? package;
    private readonly IsolatedToolEnvironment? environment;

    public string ReferenceRepository { get; }

    public ScenarioFixture()
    {
        ReferenceRepository = Environment.GetEnvironmentVariable(ScenarioTheoryAttribute.ReferenceVariable)!;
        var pin = ReferencePin.Load();
        var resolved = GitRepository.Open(ReferenceRepository).ResolveCommit(pin.Tag);
        resolved.Should().Be(pin.Commit, $"the reference clone must hold tag {pin.Tag} at the pinned commit; fetch its tags or re-clone {pin.Repository}");

        if (executable is not null)
        {
            File.Exists(executable).Should().BeTrue($"{ExecutableVariable} must name an existing executable");
            Directory.CreateDirectory(workRoot);
            return;
        }

        package = new PackedToolFixture();
        environment = new IsolatedToolEnvironment(package.FeedDirectory);
        environment.UseEmptyPackageCache();
        environment.PinTool(package.PackageId, package.PackageVersion);
        var restore = environment.RestoreTools();
        restore.Code.Should().Be(0, restore.Out + restore.Error);
    }

    // A work directory holding the tool manifest; the scenario's clone goes in <work>/repo, below the manifest, so the
    // clone itself stays exactly as the reference repository has it.
    public string NewWorkDirectory(string name)
    {
        if (environment is null)
        {
            var directory = Path.Combine(workRoot, "scenario-" + name);
            Directory.CreateDirectory(directory);
            return directory;
        }

        var work = environment.PathForNewRepository("scenario-" + name);
        environment.PinToolIn(work, package!.PackageId, package.PackageVersion);
        return work;
    }

    public (int Code, string Out, string Error) RunTool(string repository, string[] arguments) =>
        executable is not null
            ? TestSupport.RunProgram(executable, repository, new Dictionary<string, string>(), arguments)
            : environment!.RunToolIn(repository, arguments);

    // On failure in CI, keeps the command log and the clone's files (without .git) for the uploaded artifact.
    public void KeepForInspection(string name, string work)
    {
        var artifacts = Environment.GetEnvironmentVariable(ArtifactsVariable);
        if (string.IsNullOrEmpty(artifacts))
            return;
        CopyWithoutLinks(new DirectoryInfo(work), Path.Combine(artifacts, name));
    }

    public void Dispose()
    {
        environment?.Dispose();
        package?.Dispose();
        if (Directory.Exists(workRoot))
        {
            foreach (var file in Directory.EnumerateFiles(workRoot, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch (FileNotFoundException) { } // a dangling symlink from a scenario stage
            }
            Directory.Delete(workRoot, recursive: true);
        }
    }

    private static void CopyWithoutLinks(DirectoryInfo source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in source.EnumerateFileSystemInfos())
        {
            if (entry.LinkTarget is not null || entry.Name == ".git")
                continue;
            var target = Path.Combine(destination, entry.Name);
            if (entry is DirectoryInfo directory)
                CopyWithoutLinks(directory, target);
            else
                File.Copy(entry.FullName, target);
        }
    }
}
