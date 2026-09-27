namespace RepoLore.Core.Context;

public sealed class ContextBlock
{
    public ContextBlock(string path, string scope, string text, long charge)
    {
        Path = path;
        Scope = scope;
        Text = text;
        Charge = charge;
    }

    public string Path { get; }
    public string Scope { get; }
    public string Text { get; }
    public long Charge { get; }

    public string Render() => "---\n## " + Path + " [" + Scope + "]\n\n" + Text + "\n";
}

public sealed class ContextOmission
{
    public ContextOmission(string path, string scope, string reason)
    {
        Path = path;
        Scope = scope;
        Reason = reason;
    }

    public string Path { get; }
    public string Scope { get; }
    public string Reason { get; }
}

public sealed class ContextEstimate
{
    public List<ContextBlock> Included { get; } = new();
    public List<ContextOmission> Omissions { get; } = new();
    public long TotalCharge { get; set; }
}

public sealed class ContextNote
{
    public ContextNote(string path, string scope, string text)
    {
        Path = path;
        Scope = scope;
        Text = text;
    }

    public string Path { get; }
    public string Scope { get; }
    public string Text { get; }
}

public static class BlockEstimator
{
    public static long ChargeFor(string path, string scope, string text)
    {
        var length = ("---\n## " + path + " [" + scope + "]\n\n" + text + "\n").Length;
        return (length + 3) / 4;
    }

    public static ContextEstimate Estimate(IReadOnlyList<ContextNote> notes, long budget)
    {
        var result = new ContextEstimate();
        var used = 0L;

        foreach (var note in notes)
        {
            var charge = ChargeFor(note.Path, note.Scope, note.Text);
            if (used + charge > budget)
            {
                result.Omissions.Add(new ContextOmission(note.Path, note.Scope, "budget"));
                continue;
            }

            used += charge;
            result.Included.Add(new ContextBlock(note.Path, note.Scope, note.Text, charge));
        }

        result.TotalCharge = used;
        return result;
    }
}
