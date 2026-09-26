namespace RepoLore.Infrastructure;

public sealed class PathResolutionException : Exception
{
    public PathResolutionException(string message) : base(message) { }
}

public sealed class RepositoryPathResolver
{
    private const int MaxComponentLength = 255;

    private readonly string _root;

    public RepositoryPathResolver(string repositoryRoot)
    {
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRoot));
        if (_root.Length == 0)
            throw new ArgumentException("Repository root must be a non-empty absolute path.", nameof(repositoryRoot));
    }

    public string Resolve(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new PathResolutionException("Target is empty.");
        if (IsRootedOrUnsafe(target))
            throw new PathResolutionException($"Target must be repository-relative: {target}");

        foreach (var segment in target.Split('/'))
        {
            if (segment.Length == 0)
                throw new PathResolutionException($"Target contains an empty segment: {target}");
            if (segment is "." or "..")
                throw new PathResolutionException($"Target may not contain '{segment}': {target}");
            if (segment.Contains('\\') || segment.Contains(':'))
                throw new PathResolutionException($"Target contains an unsupported character: {segment}");
            if (segment.Length > MaxComponentLength)
                throw new PathResolutionException($"Filename exceeds the supported length: {segment}");
        }

        var full = Path.GetFullPath(Path.Combine(_root, target));
        if (!IsWithinRoot(full))
            throw new PathResolutionException($"Target escapes the repository root: {target}");
        EnsureNoSymlinkTraversal(full);
        return full;
    }

    public string ResolveForWrite(string target)
    {
        var full = Resolve(target);
        CheckWriteAlias(full);
        return full;
    }

    private bool IsWithinRoot(string full)
    {
        if (string.Equals(full, _root, StringComparison.Ordinal)) return true;
        return full.StartsWith(_root, StringComparison.Ordinal)
            && full.Length > _root.Length
            && (full[_root.Length] == Path.DirectorySeparatorChar
                || full[_root.Length] == Path.AltDirectorySeparatorChar);
    }

    private void EnsureNoSymlinkTraversal(string full)
    {
        var relative = full.Substring(_root.Length + 1);
        if (relative.Length == 0) return;
        var current = _root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar))
        {
            if (segment.Length == 0) continue;
            current = Path.Combine(current, segment);
            if (IsReparsePoint(current))
                throw new PathResolutionException($"Symlink/reparse traversal is not allowed: {segment}");
        }
    }

    private void CheckWriteAlias(string full)
    {
        var parent = Path.GetDirectoryName(full);
        var name = Path.GetFileName(full);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name) || !Directory.Exists(parent))
            return;

        var exactExists = false;
        foreach (var entry in Directory.EnumerateFileSystemEntries(parent))
        {
            if (string.Equals(Path.GetFileName(entry), name, StringComparison.Ordinal))
            {
                exactExists = true;
                break;
            }
        }
        if (exactExists) return;

        if (File.Exists(full) || Directory.Exists(full))
            throw new PathResolutionException(
                $"Ambiguous write target: '{name}' aliases an existing entry in '{parent}'.");
    }

    private static bool IsRootedOrUnsafe(string target) =>
        Path.IsPathRooted(target) || target.StartsWith("//", StringComparison.Ordinal) || target.StartsWith("\\\\", StringComparison.Ordinal);

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
