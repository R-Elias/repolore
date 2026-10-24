using RepoLore.Cli.Arguments;
using RepoLore.Cli.Rendering;
using RepoLore.Cli.Repository;
using RepoLore.Core.Context;
using RepoLore.Core.Health;
using RepoLore.Infrastructure;
using RepoLore.Infrastructure.Health;

namespace RepoLore.Cli.Commands;

public static class HealthCheckCommand
{
    public static int Run(CommandLine cl, string cwd)
    {
        if (cl.Session is not null && !SessionId.IsValid(cl.Session, out var sessionFinding))
            throw new UsageException("invalid --session: " + sessionFinding);

        var root = RepositoryRoot.Discover(cl.RepoRoot, cwd);

        HealthReport report;
        try
        {
            report = new HealthChecker(root).Check(cl.Session, cl.ExplainPath);
        }
        catch (PathResolutionException ex) when (cl.ExplainPath is not null)
        {
            throw new UsageException("invalid --explain: " + ex.Message);
        }

        HealthRenderer.Render(report, cl.ExplainPath is not null, cl.Json, cl.Quiet);

        if (report.HasBlockingFinding)
            return ExitCodes.Failure;
        return report.Findings.Count > 0 ? ExitCodes.Findings : ExitCodes.Success;
    }
}
