namespace RepoLore.Cli.Failures;

public static class ErrorIds
{
    public const string Usage = "usage";
    public const string HistoryLocked = "history-locked";
    public const string HistoryDisabled = "history-disabled";
    public const string HistoryFailure = "history-failure";
    public const string OperationInterrupted = "operation-interrupted";
    public const string InitIncomplete = "init-incomplete";
    public const string AlphaNeedsMigration = "alpha-needs-migration";
    public const string MigrationBlocked = "migration-blocked";
    public const string RestorePlanInvalid = "restore-plan-invalid";
    public const string IoError = "io-error";
    public const string InternalError = "internal-error";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Usage, HistoryLocked, HistoryDisabled, HistoryFailure, OperationInterrupted, InitIncomplete,
        AlphaNeedsMigration, MigrationBlocked, RestorePlanInvalid, IoError, InternalError
    };
}
