namespace RepoLore.Cli.Failures;

public sealed class CliFailureException : Exception
{
    public CliFailureException(string id, string message, long? recoveryId = null) : base(message)
    {
        Id = id;
        RecoveryId = recoveryId;
    }

    public string Id { get; }
    public long? RecoveryId { get; }
}
