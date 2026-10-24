namespace RepoLore.Cli.Arguments;

public sealed class UsageException : Exception
{
    public UsageException(string message) : base(message) { }
}
