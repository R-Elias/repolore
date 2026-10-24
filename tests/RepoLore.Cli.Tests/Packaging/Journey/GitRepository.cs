using CliWrap;
using CliWrap.Buffered;
using FluentAssertions;

namespace RepoLore.Cli.Tests.Packaging.Journey;

public sealed class GitRepository
{
    private static readonly string EmptyGlobalConfig = CreateEmptyGlobalConfig();

    public string Path { get; }

    private GitRepository(string path) => Path = path;

    public static GitRepository Initialize(string path)
    {
        var repository = new GitRepository(path);
        repository.Run("init", "--quiet", "--initial-branch", "main");
        return repository;
    }

    public static GitRepository Clone(GitRepository source, string destination)
    {
        var parent = System.IO.Path.GetDirectoryName(destination)!;
        new GitRepository(parent).Run("clone", "--quiet", source.Path, destination);
        return new GitRepository(destination);
    }

    public static GitRepository Open(string path) => new(path);

    public static GitRepository CloneWithoutCheckout(string sourcePath, string destination)
    {
        var parent = System.IO.Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        new GitRepository(parent).Run("clone", "--quiet", "--no-checkout", sourcePath, destination);
        return new GitRepository(destination);
    }

    public void CheckoutStage(string tag)
    {
        Run("checkout", "--quiet", "--force", tag);
        Run("clean", "-fd", "--quiet");
    }

    public bool HasCommit(string revision) => Execute("rev-parse", "--verify", "--quiet", revision + "^{commit}").Code == 0;

    public string ResolveCommit(string revision) => Run("rev-parse", "--verify", "--quiet", revision + "^{commit}").Trim();

    public byte[] FileAt(string revision, string relativePath)
    {
        using var buffer = new MemoryStream();
        var result = CliWrap.Cli.Wrap("git")
            .WithArguments(["-c", "core.autocrlf=false", "cat-file", "blob", revision + ":" + relativePath])
            .WithWorkingDirectory(Path)
            .WithEnvironmentVariables(env => env.Set("GIT_CONFIG_GLOBAL", EmptyGlobalConfig).Set("GIT_CONFIG_NOSYSTEM", "1"))
            .WithStandardOutputPipe(PipeTarget.ToStream(buffer))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync()
            .GetAwaiter()
            .GetResult();
        result.ExitCode.Should().Be(0, $"'{relativePath}' must exist at '{revision}'");
        return buffer.ToArray();
    }

    public void CommitAll(string message)
    {
        Run("add", "--all");
        Run("commit", "--quiet", "--message", message);
    }

    public bool IsIgnored(string relativePath) => Execute("check-ignore", "--quiet", relativePath).Code == 0;

    public IReadOnlyList<string> StatusLines() => Lines(Run("status", "--porcelain", "--untracked-files=all"));

    public IReadOnlyList<string> TrackedFiles() => Lines(Run("ls-files"));

    public IReadOnlyList<string> StagedFiles() => Lines(Run("diff", "--cached", "--name-only"));

    public IReadOnlyList<string> ChangedFiles() => Lines(Run("diff", "--name-only"));

    public string Diff() => Run("diff");

    public IReadOnlyList<string> PathsInAllHistory() => Lines(Run("log", "--all", "--name-only", "--pretty=format:"));

    public string Run(params string[] arguments)
    {
        var (code, output, error) = Execute(arguments);
        code.Should().Be(0, $"git {string.Join(' ', arguments)} must succeed:\n{output}\n{error}");
        return output;
    }

    private (int Code, string Out, string Error) Execute(params string[] arguments)
    {
        string[] isolatedArguments =
        [
            "-c", "user.name=Journey", "-c", "user.email=journey@example.invalid",
            "-c", "core.autocrlf=false", "-c", "core.longpaths=true", "-c", "commit.gpgsign=false", "-c", "tag.gpgsign=false", .. arguments,
        ];
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var result = CliWrap.Cli.Wrap("git")
            .WithArguments(isolatedArguments)
            .WithWorkingDirectory(Path)
            .WithEnvironmentVariables(env => env
                .Set("GIT_CONFIG_GLOBAL", EmptyGlobalConfig)
                .Set("GIT_CONFIG_NOSYSTEM", "1")
                .Set("GIT_TERMINAL_PROMPT", "0"))
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(timeout.Token)
            .GetAwaiter()
            .GetResult();
        return (result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private static IReadOnlyList<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();

    private static string CreateEmptyGlobalConfig()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "repolore-journey-empty-gitconfig");
        if (!File.Exists(path)) File.WriteAllText(path, string.Empty);
        return path;
    }
}
