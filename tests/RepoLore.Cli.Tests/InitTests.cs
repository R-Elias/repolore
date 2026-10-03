namespace RepoLore.Cli.Tests;

public static class InitTests
{
    public static void Run()
    {
        TestRunner.Check("empty directory initializes with one baseline and no source placeholders", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "init");
                TestRunner.Equal(0, result.Code, result.Error);

                TestRunner.Equal("{\"formatVersion\":1}", File.ReadAllText(Path.Combine(temp, "_repolore", "repolore.json")));
                var method = File.ReadAllText(Path.Combine(temp, "_repolore", "method.md"));
                TestRunner.True(method.StartsWith("# RepoLore Method", StringComparison.Ordinal), method);
                TestRunner.True(File.Exists(Path.Combine(temp, "_repolore", "root.md")));
                var sparse = Path.Combine(temp, "_repolore", "sparse-tree");
                TestRunner.True(Directory.Exists(sparse));
                TestRunner.Equal(0, Directory.EnumerateFileSystemEntries(sparse).Count());

                var history = TestSupport.Run(temp, TestSupport.Cli, "history");
                TestRunner.Equal(1, CountManifestLines(TestSupport.Normalize(history.Out)));
            });
        });

        TestRunner.Check("second init changes nothing", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "init").Code);
                var second = TestSupport.Run(temp, TestSupport.Cli, "init");
                TestRunner.Equal(0, second.Code, second.Error);
                TestRunner.True(TestSupport.Normalize(second.Out).Contains("nothing to create", StringComparison.Ordinal), second.Out);
                TestRunner.Equal(1, CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)));
            });
        });

        TestRunner.Check("preexisting root/config/custom notes survive exactly", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore", "architecture"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "MY_ROOT_SENTINEL");
                File.WriteAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md"), "MY_CUSTOM_SENTINEL");

                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "init").Code);

                TestRunner.Equal("MY_ROOT_SENTINEL", File.ReadAllText(Path.Combine(temp, "_repolore", "root.md")));
                TestRunner.Equal("MY_CUSTOM_SENTINEL", File.ReadAllText(Path.Combine(temp, "_repolore", "architecture", "custom.md")));
            });
        });

        TestRunner.Check("a nonempty alpha sparse-only clone is never emptied", () =>
        {
            TestSupport.WithFixture("alpha-sparse-only", temp =>
            {
                var result = TestSupport.Run(temp, TestSupport.Cli, "init");
                TestRunner.Equal(3, result.Code);
                TestRunner.True(result.Error.Contains("migrate", StringComparison.Ordinal), result.Error);

                TestRunner.Equal("ALPHA_SPARSE_ONLY_SENTINEL\n",
                    TestSupport.Normalize(File.ReadAllText(Path.Combine(temp, "_repolore", "sparse-tree", "src", "src.md"))));
                TestRunner.True(File.Exists(Path.Combine(temp, "_repolore", "root.md")));
                TestRunner.True(!File.Exists(Path.Combine(temp, "_repolore", "repolore.json")));
            });
        });

        TestRunner.Check("a user-edited method is never replaced during ordinary init", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "USER_METHOD_SENTINEL");

                TestRunner.Equal(0, TestSupport.Run(temp, TestSupport.Cli, "init").Code);
                TestRunner.Equal("USER_METHOD_SENTINEL", File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
            });
        });

        TestRunner.Check("method update can be undone to exact old bytes", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1}");
                File.WriteAllText(Path.Combine(temp, "_repolore", "method.md"), "OLD_METHOD_SENTINEL");
                File.WriteAllText(Path.Combine(temp, "_repolore", "root.md"), "ROOT");

                var update = TestSupport.Run(temp, TestSupport.Cli, "init", "--update-method");
                TestRunner.Equal(0, update.Code, update.Error);
                var method = File.ReadAllText(Path.Combine(temp, "_repolore", "method.md"));
                TestRunner.True(method.StartsWith("# RepoLore Method", StringComparison.Ordinal), method);
                TestRunner.True(!method.Contains("OLD_METHOD_SENTINEL", StringComparison.Ordinal));

                var undo = TestSupport.Run(temp, TestSupport.Cli, "restore", "1");
                TestRunner.Equal(0, undo.Code, undo.Error);
                TestRunner.Equal("OLD_METHOD_SENTINEL", File.ReadAllText(Path.Combine(temp, "_repolore", "method.md")));
            });
        });

        TestRunner.Check("init with disabled history creates files and states protection disabled", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                Directory.CreateDirectory(Path.Combine(temp, "_repolore"));
                File.WriteAllText(Path.Combine(temp, "_repolore", "repolore.json"), "{\"formatVersion\":1,\"history\":{\"enabled\":false}}");

                var result = TestSupport.Run(temp, TestSupport.Cli, "init");
                TestRunner.Equal(0, result.Code, result.Error);
                TestRunner.True(TestSupport.Normalize(result.Out).Contains("disabled", StringComparison.Ordinal), result.Out);
                TestRunner.True(File.Exists(Path.Combine(temp, "_repolore", "method.md")));
                TestRunner.Equal(0, CountManifestLines(TestSupport.Normalize(TestSupport.Run(temp, TestSupport.Cli, "history").Out)));
            });
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
