using CliWrap;
using CliWrap.Buffered;
using System.Security.Cryptography;

namespace RepoLore.Cli.Tests;

public static class TestSupport
{
    public static string Repo { get; } = FindRepo();
    public static string Configuration { get; } = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
    public static string Cli { get; } = Path.Combine(Repo, "src", "RepoLore.Cli", "bin", Configuration, "net10.0", "RepoLore.Cli.dll");
    public static string Dotnet { get; } = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
        ?? Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
            "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));

    public static (int Code, string Out, string Error) Run(string cwd, params string[] arguments)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var result = CliWrap.Cli.Wrap(Dotnet)
                .WithArguments(arguments)
                .WithWorkingDirectory(cwd)
                .WithEnvironmentVariables(env => env
                    .Set("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
                    .Set("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1"))
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(timeout.Token)
                .GetAwaiter()
                .GetResult();
            return (result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("dotnet exceeded two minutes.");
        }
    }

    public static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

    public static string FindRepo()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "RepoLore.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Run the suite from a repository build.");
    }

    public static string Snapshot(string root) => string.Join('\n',
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => Directory.Exists(path)
                ? $"D {Path.GetRelativePath(root, path)} {Directory.GetLastWriteTimeUtc(path).Ticks}"
                : $"F {Path.GetRelativePath(root, path)} {File.GetLastWriteTimeUtc(path).Ticks} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}"));

    public static void WithTemp(Action<string> action)
    {
        var temp = Path.Combine(Path.GetTempPath(), "repolore-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try { action(temp); }
        finally { Directory.Delete(temp, recursive: true); }
    }

    public static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file) == "gitignore.template" ? ".gitignore" : Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source))
            if (Path.GetFileName(directory) is not ("bin" or "obj"))
                CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    public static void WithFixture(string name, Action<string> action)
    {
        WithTemp(temp =>
        {
            CopyTree(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), temp);
            action(temp);
        });
    }
}
