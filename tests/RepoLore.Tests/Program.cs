using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

// A single dependency-free executable suite. Nonzero exit means a failed gate.
var repo = FindRepo();
var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
var cli = Path.Combine(repo, "src", "RepoLore.Cli", "bin", configuration, "net10.0", "RepoLore.Cli.dll");
var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
    ?? Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
        "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
var failures = 0;
Check("version outside repository is read-only", () =>
{
    WithTemp(temp =>
    {
        File.WriteAllText(Path.Combine(temp, "sentinel.txt"), "OUTSIDE_REPOSITORY_SENTINEL\r\n");
        var before = Snapshot(temp);
        var result = Run(temp, cli, "version");
        Equal(0, result.Code);
        Equal("RepoLore 0.1.0-preview.1\nSupported knowledge formats: 1\n", Normalize(result.Out));
        Equal("", result.Error);
        Equal(before, Snapshot(temp));
    });
});
Check("named fixtures and rejected commands remain unchanged", () =>
{
    var names = new[] { "minimal-v1", "two-sessions", "mapping-collisions", "alpha-sparse-only", "alpha-local-only", "alpha-conflict", "history-failures" };
    foreach (var name in names)
    {
        WithTemp(temp =>
        {
            CopyTree(Path.Combine(repo, "tests", "RepoLore.Tests", "Fixtures", name), temp);
            True(File.ReadAllText(Path.Combine(temp, "unrelated", "sentinel.txt")).Contains("SENTINEL", StringComparison.Ordinal));
            if (name == "two-sessions")
            {
                foreach (var session in new[] { "a", "b" })
                    Equal($"SESSION_{session.ToUpperInvariant()}_ROOT_SENTINEL\n",
                        Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "sessions", "session-" + session, "root.md"))));
                True(File.Exists(Path.Combine(temp, ".gitignore")));
            }
            var before = Snapshot(temp);
            Equal(0, Run(temp, cli, "version").Code);
            Equal(0, Run(temp, cli, "--help").Code);
            foreach (var arguments in new[] { Array.Empty<string>(), new[] { "init" }, new[] { "checkpoint" }, new[] { "version", "--unknown" }, new[] { "version", "version" } })
            {
                var result = Run(temp, new[] { cli }.Concat(arguments).ToArray());
                Equal(2, result.Code);
                Equal("", result.Out);
                True(result.Error.StartsWith("Usage:", StringComparison.Ordinal));
            }
            Equal(before, Snapshot(temp));
        });
    }
});
Check("runtime dependency list contains exactly the three shipped projects", () =>
{
    using var json = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(cli, ".deps.json")));
    var libraries = json.RootElement.GetProperty("libraries").EnumerateObject().ToArray();
    Equal("RepoLore.Cli,RepoLore.Core,RepoLore.Infrastructure", string.Join(',', libraries.Select(x => x.Name.Split('/')[0]).Order(StringComparer.Ordinal)));
    True(libraries.All(x => x.Value.GetProperty("type").GetString() == "project"));
});
Check("build guards reject forbidden capabilities and recover after removal", () =>
{
    WithTemp(temp =>
    {
        foreach (var file in new[] { "global.json", "Directory.Build.props", "Directory.Build.targets", "NuGet.Config" })
            File.Copy(Path.Combine(repo, file), Path.Combine(temp, file));
        CopyTree(Path.Combine(repo, "build"), Path.Combine(temp, "build"));
        CopyTree(Path.Combine(repo, "src"), Path.Combine(temp, "src"));
        var project = Path.Combine(temp, "src", "RepoLore.Cli", "RepoLore.Cli.csproj");
        var baseline = Run(temp, "build", project, "--configuration", "Release", "--nologo");
        Equal(0, baseline.Code, baseline.Out + baseline.Error);
        var probes = new (string Project, string Code)[]
        {
            ("Core", "public static object Run() => new System.Net.Http.HttpClient();"),
            ("Infrastructure", "public static object Run() => System.Net.Dns.GetHostAddresses(\"localhost\");"),
            ("Cli", "public static object Run() => new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);"),
            ("Core", "public static object? Run() => System.Diagnostics.Process.Start(\"echo\");"),
            ("Infrastructure", "public static object Run() => System.Reflection.Assembly.Load(\"example\");"),
            ("Cli", "public static object Run() => System.Runtime.Loader.AssemblyLoadContext.Default;"),
            ("Core", "[System.Runtime.InteropServices.DllImport(\"example\")] public static extern void Run();"),
            ("Infrastructure", "public static nint Run() => System.Runtime.InteropServices.NativeLibrary.Load(\"example\");"),
            ("Cli", "public static object? Run() => System.Type.GetType(\"example\");")
        };
        foreach (var probe in probes)
        {
            var path = Path.Combine(temp, "src", "RepoLore." + probe.Project, "ForbiddenProbe.cs");
            File.WriteAllText(path, "public static class ForbiddenProbe { " + probe.Code + " }");
            try
            {
                var result = Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
                True(result.Code != 0 && result.Out.Contains("RLBOUNDARY:", StringComparison.Ordinal), result.Out + result.Error);
            }
            finally { File.Delete(path); }
        }
        var originalProject = File.ReadAllText(project);
        var dependencyProbe = XDocument.Parse(originalProject);
        dependencyProbe.Root!.Add(new XElement("ItemGroup", new XElement("Reference",
            new XAttribute("Include", "ForbiddenDependency"),
            new XElement("HintPath", typeof(Program).Assembly.Location))));
        try
        {
            dependencyProbe.Save(project);
            var result = Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
            True(result.Code != 0 && result.Out.Contains("RLBOUNDARY: non-BCL runtime references:", StringComparison.Ordinal), result.Out + result.Error);
        }
        finally { File.WriteAllText(project, originalProject); }
        var recovered = Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
        Equal(0, recovered.Code, recovered.Out + recovered.Error);
    });
});
Console.WriteLine($"{4 - failures}/4 checks passed.");
return failures == 0 ? 0 : 1;

void Check(string name, Action check)
{
    try { check(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
}
void Equal<T>(T expected, T actual, string detail = "")
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}; got {actual}. {detail}");
}
void True(bool value, string detail = "")
{
    if (!value) throw new InvalidOperationException("Assertion failed. " + detail);
}
(int Code, string Out, string Error) Run(string cwd, params string[] arguments)
{
    var start = new ProcessStartInfo(dotnet) { WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
    start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(120_000))
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException("dotnet exceeded two minutes.");
    }
    return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
}
static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);
static string FindRepo()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "RepoLore.slnx"))) return directory.FullName;
    throw new InvalidOperationException("Run the suite from a repository build.");
}
static string Snapshot(string root) => string.Join('\n', Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
    .Order(StringComparer.Ordinal).Select(path => Directory.Exists(path)
        ? $"D {Path.GetRelativePath(root, path)} {Directory.GetLastWriteTimeUtc(path).Ticks}"
        : $"F {Path.GetRelativePath(root, path)} {File.GetLastWriteTimeUtc(path).Ticks} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}"));
static void WithTemp(Action<string> action)
{
    var temp = Path.Combine(Path.GetTempPath(), "repolore-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temp);
    try { action(temp); }
    finally { Directory.Delete(temp, recursive: true); }
}
static void CopyTree(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var file in Directory.EnumerateFiles(source))
        File.Copy(file, Path.Combine(destination, Path.GetFileName(file) == "gitignore.template" ? ".gitignore" : Path.GetFileName(file)));
    foreach (var directory in Directory.EnumerateDirectories(source))
        if (Path.GetFileName(directory) is not ("bin" or "obj")) CopyTree(directory, Path.Combine(destination, Path.GetFileName(directory)));
}
