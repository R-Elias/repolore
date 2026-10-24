namespace RepoLore.Cli.Rendering;

public sealed class TreeEntry
{
    public TreeEntry(string path, bool isDirectory)
    {
        Path = path;
        IsDirectory = isDirectory;
    }

    public string Path { get; }
    public bool IsDirectory { get; }
}
