namespace RepoLore.Core;

public static class KnowledgePathMapper
{
    public const string RootNoteName = "root.md";
    private const string NoteSuffix = ".md";

    public static string MapDirectoryNote(string directoryTarget)
    {
        if (string.IsNullOrEmpty(directoryTarget) || directoryTarget == "." || directoryTarget == "/")
            return RootNoteName;

        var components = SplitSource(directoryTarget);
        if (components.Count == 0)
            return RootNoteName;

        return string.Join('/', components) + "/" + components[^1] + NoteSuffix;
    }

    public static bool TryDecode(string notePath, out string sourcePath, out string? finding)
    {
        sourcePath = string.Empty;
        finding = null;

        if (notePath.Contains('\\'))
            return Fail("note path must use '/' separators", out sourcePath, out finding);

        var components = notePath.Split('/');
        if (components.Any(static c => c.Length == 0))
            return Fail("note path contains an empty segment", out sourcePath, out finding);

        if (components.Length == 1 && components[0] == RootNoteName)
        {
            sourcePath = ".";
            return true;
        }

        var last = components[^1];
        if (!last.EndsWith(NoteSuffix, StringComparison.Ordinal))
            return Fail("note name must end in .md", out sourcePath, out finding);

        var basename = last[..^NoteSuffix.Length];
        if (basename.Length == 0)
            return Fail("note name is empty", out sourcePath, out finding);
        if (components.Length < 2)
            return Fail($"only {RootNoteName} is a top-level note", out sourcePath, out finding);
        if (!string.Equals(components[^2], basename, StringComparison.Ordinal))
            return Fail($"note name must match its directory: {notePath}", out sourcePath, out finding);

        sourcePath = string.Join('/', components[..^1]) + "/";
        return true;
    }

    private static List<string> SplitSource(string target)
    {
        var result = new List<string>();
        foreach (var raw in target.Split('/'))
        {
            if (raw.Length == 0) continue;
            if (raw is "." or "..")
                throw new ArgumentException($"Unsupported path segment: {raw}", nameof(target));
            if (raw.Contains('\\') || raw.Contains(':'))
                throw new ArgumentException($"Unsupported path segment: {raw}", nameof(target));
            result.Add(raw);
        }
        return result;
    }

    private static bool Fail(string? message, out string sourcePath, out string? finding)
    {
        sourcePath = string.Empty;
        finding = message;
        return false;
    }
}
