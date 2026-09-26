namespace RepoLore.Core.Tests;

public static class TestRunner
{
    private static int _checks;
    private static int _failures;

    public static int RunAll(Action run)
    {
        run();
        Console.WriteLine($"{_checks - _failures}/{_checks} checks passed.");
        return _failures == 0 ? 0 : 1;
    }

    public static void Check(string name, Action check)
    {
        _checks++;
        try { check(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); }
    }

    public static void Throws<T>(Action action, string detail = "") where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}. {detail}");
    }

    public static void Equal<T>(T expected, T actual, string detail = "")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}; got {actual}. {detail}");
    }

    public static void True(bool value, string detail = "")
    {
        if (!value) throw new InvalidOperationException("Assertion failed. " + detail);
    }
}
