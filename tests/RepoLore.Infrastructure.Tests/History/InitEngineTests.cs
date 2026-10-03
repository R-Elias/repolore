using RepoLore.Infrastructure.History;
using RepoLore.Infrastructure.Tests;

namespace RepoLore.Infrastructure.Tests.History;

public static class InitEngineTests
{
    private const string MethodTemplate = "METHOD_V1_TEMPLATE";
    private const string RootTemplate = "ROOT_TEMPLATE";

    public static void Run()
    {
        TestRunner.Check("empty directory initializes with marker, templates, tree, and one baseline", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var result = Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: false);
                TestRunner.True(!result.WasNoOp);
                TestRunner.Equal("{\"formatVersion\":1}", File.ReadAllText(Path.Combine(temp, "_repolore", "repolore.json")));
                TestRunner.Equal(MethodTemplate, File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
                TestRunner.Equal(RootTemplate, File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")));
                TestRunner.True(Directory.Exists(Path.Combine(temp, "_repolore", "sparse-tree")));
                TestRunner.Equal(0, Directory.EnumerateFileSystemEntries(Path.Combine(temp, "_repolore", "sparse-tree")).Count());
                TestRunner.Equal(1L, HighestId(temp));
            });
        });

        TestRunner.Check("second init changes nothing", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Engine(temp).Run(MethodTemplate, RootTemplate, false);
                var result = Engine(temp).Run(MethodTemplate, RootTemplate, false);
                TestRunner.True(result.WasNoOp);
                TestRunner.Equal(0, result.Created.Count);
                TestRunner.Equal(1L, HighestId(temp));
            });
        });

        TestRunner.Check("preexisting root/config/custom notes survive exactly", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "MY_ROOT");
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md"), "MY_CUSTOM");

                Engine(temp).Run(MethodTemplate, RootTemplate, false);

                TestRunner.Equal("MY_ROOT", File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")));
                TestRunner.Equal("MY_CUSTOM", File.ReadAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md")));
                TestRunner.Equal(MethodTemplate, File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
                TestRunner.True(Directory.Exists(Path.Combine(temp, "_repolore", "sparse-tree")));
            });
        });

        TestRunner.Check("failure creating the first checkpoint is visible and retryable", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var ex = TestRunner.Capture<InitIncompleteException>(() =>
                    Engine(temp).Run(MethodTemplate, RootTemplate, false,
                        beforeFirstCapture: () => throw new InvalidOperationException("injected")));

                TestRunner.True(ex.Created.Contains("_repolore/repolore.json"));
                TestRunner.True(ex.Created.Contains("_repolore/method.md"));
                TestRunner.True(ex.Created.Contains("_repolore/root.md"));
                TestRunner.Equal(0L, HighestId(temp));

                Engine(temp).Run(MethodTemplate, RootTemplate, false);
                TestRunner.Equal(1L, HighestId(temp));
            });
        });

        TestRunner.Check("alpha content without a marker is refused and never emptied", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sparse-tree", "src"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ALPHA_ROOT");
                File.WriteAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md"), "ALPHA_NOTE");

                TestRunner.Equal(InitDirKind.Alpha, Engine(temp).Classify());
                var ex = TestRunner.Capture<InitException>(() => Engine(temp).Run(MethodTemplate, RootTemplate, false));
                TestRunner.True(ex.Message.Contains("migrate", StringComparison.Ordinal), ex.Message);

                TestRunner.Equal("ALPHA_ROOT", File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")));
                TestRunner.Equal("ALPHA_NOTE", File.ReadAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md")));
            });
        });

        TestRunner.Check("a user-edited method is never replaced during ordinary init", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "USER_METHOD");

                Engine(temp).Run(MethodTemplate, RootTemplate, false);
                TestRunner.Equal("USER_METHOD", File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
            });
        });

        TestRunner.Check("method update rewrites the method and can be undone to exact old bytes", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

                var result = Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true);
                TestRunner.True(result.MethodUpdated);
                TestRunner.Equal(MethodTemplate, File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
                TestRunner.True(!File.Exists(PendingPath(temp)));

                var restored = new RestoreEngine(temp, HistoryDir(temp), Clock()).Apply(HistoryConfigLoader.Load(temp), 1);
                TestRunner.True(!restored.WasNoOp);
                TestRunner.Equal("OLD_METHOD", File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
            });
        });

        TestRunner.Check("unchanged embedded method bytes are a no-op", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore", "sparse-tree"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), MethodTemplate);
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), RootTemplate);

                var result = Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true);
                TestRunner.True(!result.MethodUpdated);
                TestRunner.True(result.WasNoOp);
                TestRunner.True(!File.Exists(PendingPath(temp)));
            });
        });

        TestRunner.Check("method update with disabled history refuses", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD");

                var ex = TestRunner.Capture<InitException>(() => Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true));
                TestRunner.True(ex.Message.Contains("disabled", StringComparison.Ordinal), ex.Message);
                TestRunner.Equal("OLD_METHOD", File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
            });
        });

        TestRunner.Check("method update interrupted before write leaves pending and recovers", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

                var ex = TestRunner.Capture<InitException>(() =>
                    Engine(temp).Run(MethodTemplate, RootTemplate, updateMethod: true,
                        beforeFirstReplacement: () => throw new InvalidOperationException("injected")));

                TestRunner.True(ex.RecoveryId is long, "expected a recovery id");
                TestRunner.True(File.Exists(PendingPath(temp)));
                TestRunner.Equal("OLD_METHOD", File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));

                var recovered = new RestoreEngine(temp, HistoryDir(temp), Clock()).Recover(ex.RecoveryId!.Value);
                TestRunner.True(recovered.WasRecovery);
                TestRunner.True(!File.Exists(PendingPath(temp)));
                TestRunner.Equal("OLD_METHOD", File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
            });
        });

        TestRunner.Check("history disabled init reports protection disabled and skips checkpoint", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");

                var result = Engine(temp).Run(MethodTemplate, RootTemplate, false);
                TestRunner.True(result.HistoryDisabled);
                TestRunner.Equal(MethodTemplate, File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
                TestRunner.Equal(0L, HighestId(temp));
            });
        });
    }

    private static InitEngine Engine(string temp) => new(temp, HistoryDir(temp), Clock());

    private static string HistoryDir(string temp) => Path.Combine(temp, "_repolore", ".history");

    private static string PendingPath(string temp) => Path.Combine(HistoryDir(temp), "pending.json");

    private static long HighestId(string temp) => new CheckpointStore(HistoryDir(temp)).ReadHighestCompleted()?.Id ?? 0;

    private static Clock Clock() => new(() => new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
}
