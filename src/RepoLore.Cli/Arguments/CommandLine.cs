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
    public bool SawOption { get; set; }

    public static CommandLine Parse(string[] args)
    {
        var cl = new CommandLine();
        string? command = null;
        string? positional = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                    cl.Help = true;
                    cl.SawOption = true;
                    break;
                case "--json":
                    cl.Json = true;
                    cl.SawOption = true;
                    break;
                case "--quiet":
                    cl.Quiet = true;
                    cl.SawOption = true;
                    break;
                case "--include-method":
                    cl.IncludeMethod = true;
                    cl.SawOption = true;
                    break;
                case "--strict":
                    cl.Strict = true;
                    cl.SawOption = true;
                    break;
                case "--session":
                    cl.Session = Value(args, ref i, "--session");
                    cl.SawOption = true;
                    break;
                case "--node":
                    cl.Nodes.Add(Value(args, ref i, "--node"));
                    cl.SawOption = true;
                    break;
                case "--budget-tokens":
                    cl.Budget = ParseBudget(Value(args, ref i, "--budget-tokens"));
                    cl.SawOption = true;
                    break;
                case "--repo-root":
                    cl.RepoRoot = Value(args, ref i, "--repo-root");
                    cl.SawOption = true;
                    break;
                case "--start":
                    cl.TreeStart = Value(args, ref i, "--start");
                    cl.SawOption = true;
                    break;
                case "--path":
                    cl.RestorePath = Value(args, ref i, "--path");
                    cl.SawOption = true;
                    break;
                case "--dry-run":
                    cl.DryRun = true;
                    cl.SawOption = true;
                    break;
                case "--update-method":
                    cl.UpdateMethod = true;
                    cl.SawOption = true;
                    break;
                case "--check":
                    cl.Check = true;
                    cl.SawOption = true;
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

    public void RejectKnowledgeArguments(string commandName)
    {
        if (Target is not null || Session is not null || Nodes.Count > 0 || IncludeMethod || Strict || TreeStart is not null)
            throw new UsageException(commandName + " takes no arguments");
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
