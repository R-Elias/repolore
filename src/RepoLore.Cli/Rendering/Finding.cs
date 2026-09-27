namespace RepoLore.Cli.Rendering;

public sealed class Finding
{
    public Finding(string id, string path, string message)
    {
        Id = id;
        Path = path;
        Message = message;
    }

    public string Id { get; }
    public string Path { get; }
    public string Message { get; }
}
