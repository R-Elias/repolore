namespace RepoLore.Infrastructure.Tests;

public static class TestSupport
{
    public static void WithTemp(Action<string> action)
    {
        var temp = Path.Combine(Path.GetTempPath(), "repolore-infra-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try { action(temp); }
        finally { Directory.Delete(temp, recursive: true); }
    }
}
