using System.Text.Json;
using System.Xml.Linq;

namespace RepoLore.Cli.Tests;

public static class ExecutableFoundationTests
{
    public static void Run()
    {
        TestRunner.Check("version outside repository is read-only", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                File.WriteAllText(Path.Combine(temp, "sentinel.txt"), "OUTSIDE_REPOSITORY_SENTINEL\r\n");
                var before = TestSupport.Snapshot(temp);
                var result = TestSupport.Run(temp, TestSupport.Cli, "version");
                TestRunner.Equal(0, result.Code);
                TestRunner.Equal("RepoLore 0.1.0-preview.1\nSupported knowledge formats: 1\n", TestSupport.Normalize(result.Out));
                TestRunner.Equal("", result.Error);
                TestRunner.Equal(before, TestSupport.Snapshot(temp));
            });
        });

        TestRunner.Check("named fixtures and rejected commands remain unchanged", () =>
        {
            var names = new[] { "minimal-v1", "two-sessions", "alpha-sparse-only", "alpha-local-only", "alpha-conflict", "history-failures" };
            foreach (var name in names)
            {
                TestSupport.WithTemp(temp =>
                {
                    TestSupport.CopyTree(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), temp);
                    TestRunner.True(File.ReadAllText(Path.Combine(temp, "unrelated", "sentinel.txt")).Contains("SENTINEL", StringComparison.Ordinal));
                    if (name == "two-sessions")
                    {
                        foreach (var session in new[] { "a", "b" })
                            TestRunner.Equal($"SESSION_{session.ToUpperInvariant()}_ROOT_SENTINEL\n",
                                TestSupport.Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "sessions", "session-" + session, "root.md"))));
                        TestRunner.True(File.Exists(Path.Combine(temp, ".gitignore")));
                    }
                    var before = TestSupport.Snapshot(temp);
                    TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "version").Code);
                    TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "--help").Code);
                    foreach (var arguments in new[] { Array.Empty<string>(), new[] { "init" }, new[] { "checkpoint" }, new[] { "version", "--unknown" }, new[] { "version", "version" } })
                    {
                        var result = TestSupport.Run(temp, new[] { TestSupport.Cli }.Concat(arguments).ToArray());
                        TestRunner.Equal(2, result.Code);
                        TestRunner.Equal("", result.Out);
                        TestRunner.True(result.Error.StartsWith("Usage:", StringComparison.Ordinal));
                    }
                    TestRunner.Equal(before, TestSupport.Snapshot(temp));
                });
            }
        });

        TestRunner.Check("runtime dependency list contains exactly the three shipped projects", () =>
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(TestSupport.Cli, ".deps.json")));
            var libraries = json.RootElement.GetProperty("libraries").EnumerateObject().ToArray();
            TestRunner.Equal("RepoLore.Cli,RepoLore.Core,RepoLore.Infrastructure", string.Join(',', libraries.Select(x => x.Name.Split('/')[0]).Order(StringComparer.Ordinal)));
            TestRunner.True(libraries.All(x => x.Value.GetProperty("type").GetString() == "project"));
        });

        TestRunner.Check("build guards reject forbidden capabilities and recover after removal", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                foreach (var file in new[] { "global.json", "Directory.Build.props", "Directory.Build.targets", "NuGet.Config" })
                    File.Copy(Path.Combine(TestSupport.Repo, file), Path.Combine(temp, file));
                TestSupport.CopyTree(Path.Combine(TestSupport.Repo, "build"), Path.Combine(temp, "build"));
                TestSupport.CopyTree(Path.Combine(TestSupport.Repo, "src"), Path.Combine(temp, "src"));
                var project = Path.Combine(temp, "src", "RepoLore.Cli", "RepoLore.Cli.csproj");
                var baseline = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--nologo");
                TestRunner.Equal(0, baseline.Code, baseline.Out + baseline.Error);
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
                        var result = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
                        TestRunner.True(result.Code != 0 && result.Out.Contains("RLBOUNDARY:", StringComparison.Ordinal), result.Out + result.Error);
                    }
                    finally { File.Delete(path); }
                }
                var originalProject = File.ReadAllText(project);
                var dependencyProbe = XDocument.Parse(originalProject);
                dependencyProbe.Root!.Add(new XElement("ItemGroup", new XElement("Reference",
                    new XAttribute("Include", "ForbiddenDependency"),
                    new XElement("HintPath", typeof(ExecutableFoundationTests).Assembly.Location))));
                try
                {
                    dependencyProbe.Save(project);
                    var result = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
                    TestRunner.True(result.Code != 0 && result.Out.Contains("RLBOUNDARY: non-BCL runtime references:", StringComparison.Ordinal), result.Out + result.Error);
                }
                finally { File.WriteAllText(project, originalProject); }
                var recovered = TestSupport.Run(temp, "build", project, "--configuration", "Release", "--no-restore", "--nologo");
                TestRunner.Equal(0, recovered.Code, recovered.Out + recovered.Error);
            });
        });
    }
}
