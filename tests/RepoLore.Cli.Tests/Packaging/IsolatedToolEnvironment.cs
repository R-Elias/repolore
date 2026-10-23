namespace RepoLore.Cli.Tests.Packaging;

public sealed class IsolatedToolEnvironment : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "repolore-install-" + Guid.NewGuid().ToString("N"));
    private int cacheGeneration;

    public string FeedDirectory { get; }
    public string ConsumerRepository { get; }
    public string NuGetConfigPath => Path.Combine(root, "nuget.config");
    public string PackageCache => Path.Combine(root, "packages-" + cacheGeneration);

    public IsolatedToolEnvironment(string feedDirectory)
    {
        FeedDirectory = feedDirectory;
        ConsumerRepository = Path.Combine(root, "consumer");
        Directory.CreateDirectory(ConsumerRepository);
        File.WriteAllText(NuGetConfigPath,
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local-only" value="{feedDirectory}" />
              </packageSources>
            </configuration>
            """);
    }

    public void UseEmptyPackageCache()
    {
        cacheGeneration++;
        Directory.CreateDirectory(PackageCache);
    }

    public void PinTool(string packageId, string version)
    {
        Directory.CreateDirectory(Path.Combine(ConsumerRepository, ".config"));
        File.WriteAllText(Path.Combine(ConsumerRepository, ".config", "dotnet-tools.json"),
            $$"""
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "{{packageId.ToLowerInvariant()}}": { "version": "{{version}}", "commands": [ "repolore" ], "rollForward": false }
              }
            }
            """);
    }

    public (int Code, string Out, string Error) RestoreTools() => Dotnet("tool", "restore", "--configfile", NuGetConfigPath);

    public (int Code, string Out, string Error) RunTool(params string[] arguments) =>
        Dotnet(["tool", "run", "repolore", "--", .. arguments]);

    public void Dispose() => Directory.Delete(root, recursive: true);

    private (int Code, string Out, string Error) Dotnet(params string[] arguments) =>
        TestSupport.RunWithEnvironment(ConsumerRepository, IsolationVariables(), arguments);

    private Dictionary<string, string> IsolationVariables() => new()
    {
        ["NUGET_PACKAGES"] = PackageCache,
        ["NUGET_HTTP_CACHE_PATH"] = Path.Combine(root, "http-cache"),
        ["NUGET_PLUGINS_CACHE_PATH"] = Path.Combine(root, "plugins-cache"),
        ["DOTNET_CLI_HOME"] = Path.Combine(root, "dotnet-home"),
        ["DOTNET_NOLOGO"] = "1",
    };
}
