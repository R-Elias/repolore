using System.Diagnostics;
using RepoLore.Infrastructure.History;
using RepoLore.Infrastructure.Tests;

namespace RepoLore.Infrastructure.Tests.History;

public static class WriterLockCrashTests
{
    public static void Run()
    {
        TestRunner.Check("a killed lock owner does not block later writes", () =>
        {
            TestSupport.WithTemp(temp =>
            {
                var history = Path.Combine(temp, ".history");
                var ready = Path.Combine(temp, "ready");

                var start = new ProcessStartInfo(Dotnet)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
                start.ArgumentList.Add(TestHostDll);
                start.ArgumentList.Add("--hold-lock");
                start.ArgumentList.Add(history);
                start.ArgumentList.Add(ready);

                using var holder = Process.Start(start) ?? throw new InvalidOperationException("could not start the lock holder");
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!File.Exists(ready) && DateTime.UtcNow < deadline)
                    Thread.Sleep(50);

                TestRunner.True(File.Exists(ready), "the lock holder did not become ready");
                TestRunner.Throws<WriterLockException>(() => WriterLock.Acquire(history));

                holder.Kill(entireProcessTree: true);
                holder.WaitForExit();

                using (WriterLock.Acquire(history))
                    TestRunner.True(true);
            });
        });
    }

    private static string Dotnet =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
        ?? Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
            "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));

    private static string TestHostDll => Path.Combine(AppContext.BaseDirectory, "RepoLore.Infrastructure.Tests.dll");
}
