namespace RepoLore.Cli.Arguments;

public sealed class CommandLine
{
    public string? Command { get; set; }
    public string? Target { get; set; }
    public bool IncludeMethod { get; set; }
    public string? Session { get; set; }
    public List<string> Nodes { get; } = new();
    public long Budget { get; set; } = 8000;
    public bool Strict { get; set; }
    public string? RepoRoot { get; set; }
    public string? TreeStart { get; set; }
    public string? RestorePath { get; set; }
    public bool DryRun { get; set; }
    public bool UpdateMethod { get; set; }
    public bool Check { get; set; }
    public bool Json { get; set; }
    public bool Quiet { get; set; }
    public bool Help { get; set; }
    public string? ExplainPath { get; set; }
    public HashSet<string> SeenOptions { get; } = new(StringComparer.Ordinal);

    public static CommandLine Parse(string[] args)
    {
        var cl = new CommandLine();
        string? command = null;
        string? positional = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal) && !cl.SeenOptions.Add(arg) && arg != "--node")
                throw new UsageException("duplicate option: " + arg);
            switch (arg)
            {
                case "--help":
                    cl.Help = true;
                    break;
                case "--json":
                    cl.Json = true;
                    break;
                case "--quiet":
                    cl.Quiet = true;
                    break;
                case "--include-method":
                    cl.IncludeMethod = true;
                    break;
                case "--strict":
                    cl.Strict = true;
                    break;
                case "--session":
                    cl.Session = Value(args, ref i, "--session");
                    break;
                case "--node":
                    cl.Nodes.Add(Value(args, ref i, "--node"));
                    break;
                case "--budget-tokens":
                    cl.Budget = ParseBudget(Value(args, ref i, "--budget-tokens"));
                    break;
                case "--repo-root":
                    cl.RepoRoot = Value(args, ref i, "--repo-root");
                    break;
                case "--start":
                    cl.TreeStart = Value(args, ref i, "--start");
                    break;
                case "--path":
                    cl.RestorePath = Value(args, ref i, "--path");
                    break;
                case "--dry-run":
                    cl.DryRun = true;
                    break;
                case "--update-method":
                    cl.UpdateMethod = true;
                    break;
                case "--explain":
                    cl.ExplainPath = Value(args, ref i, "--explain");
                    break;
                case "--check":
                    cl.Check = true;
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                        throw new UsageException("unknown option: " + arg);
                    if (command is null)
                        command = arg;
                    else if (positional is null)
                        positional = arg;
                    else
                        throw new UsageException("unexpected argument: " + arg);
                    break;
            }
        }

        cl.Command = command;
        cl.Target = positional;
        return cl;
    }

    private static string Value(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
            throw new UsageException(option + " requires a value");
        return args[++i];
    }

    private static long ParseBudget(string raw)
    {
        if (!long.TryParse(raw, out var value))
            throw new UsageException("--budget-tokens must be an integer");
        return value;
    }
}
