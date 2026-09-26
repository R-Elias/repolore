namespace RepoLore.Core;

public sealed class RuleSyntaxException : Exception
{
    public int Line { get; }

    public RuleSyntaxException(int line, string message) : base(message) { Line = line; }
}

public sealed class CompiledRule
{
    private readonly Segment[] _segments;

    public int Line { get; }
    public string Source { get; }
    public bool Negated { get; }
    public bool DirectoryOnly { get; }

    private CompiledRule(int line, string source, bool negated, bool directoryOnly, Segment[] segments)
    {
        Line = line;
        Source = source;
        Negated = negated;
        DirectoryOnly = directoryOnly;
        _segments = segments;
    }

    public static CompiledRule? TryParse(int line, string text)
    {
        if (text.Length == 0)
            return null;
        if (text[0] == '#')
            return null;

        var negated = false;
        if (text[0] == '!')
        {
            negated = true;
            text = text[1..];
        }
        else if (text[0] == '\\')
        {
            if (text.Length < 2 || (text[1] != '#' && text[1] != '!'))
                throw new RuleSyntaxException(line, "unsupported escape: '\\' is only valid before '#' or '!' at the start of a rule.");
            text = text[1..];
        }

        if (text.StartsWith("/", StringComparison.Ordinal))
            text = text[1..];

        var directoryOnly = false;
        if (text.Length > 0 && text[^1] == '/')
        {
            directoryOnly = true;
            text = text[..^1];
        }

        if (text.Length == 0)
            throw new RuleSyntaxException(line, "rule has an empty pattern.");

        var segments = CompileSegments(line, text);
        return new CompiledRule(line, text, negated, directoryOnly, segments);
    }

    private static Segment[] CompileSegments(int line, string text)
    {
        var parts = text.Split('/');
        var segments = new List<Segment>(parts.Length);
        foreach (var part in parts)
        {
            if (part.Length == 0)
                throw new RuleSyntaxException(line, "rule contains an empty path segment.");
            if (part == "**")
            {
                segments.Add(Segment.DoubleStar());
                continue;
            }
            if (part.Contains("**", StringComparison.Ordinal))
                throw new RuleSyntaxException(line, "'**' must be a whole path segment (found in '" + part + "').");
            if (part.Contains('[') || part.Contains(']'))
                throw new RuleSyntaxException(line, "character classes ([...]) are not supported.");
            if (part.Contains('\\'))
                throw new RuleSyntaxException(line, "unsupported escape in pattern.");
            segments.Add(Segment.Glob(part));
        }
        return segments.ToArray();
    }

    public bool Matches(IReadOnlyList<string> path, bool isDirectory)
    {
        for (var k = 1; k <= path.Count; k++)
        {
            if (!MatchSegments(0, 0, k, path))
                continue;
            if (!DirectoryOnly || k < path.Count || isDirectory)
                return true;
        }
        return false;
    }

    private bool MatchSegments(int segmentIndex, int pathIndex, int pathEnd, IReadOnlyList<string> path)
    {
        while (segmentIndex < _segments.Length)
        {
            var segment = _segments[segmentIndex];
            if (segment.IsDoubleStar)
            {
                for (var k = pathIndex; k <= pathEnd; k++)
                    if (MatchSegments(segmentIndex + 1, k, pathEnd, path))
                        return true;
                return false;
            }
            if (pathIndex >= pathEnd)
                return false;
            if (!MatchGlob(segment.Pattern, path[pathIndex]))
                return false;
            segmentIndex++;
            pathIndex++;
        }
        return pathIndex == pathEnd;
    }

    private static bool MatchGlob(string pattern, string value) => MatchGlobAt(pattern, value, 0, 0);

    private static bool MatchGlobAt(string pattern, string value, int patternIndex, int valueIndex)
    {
        while (patternIndex < pattern.Length)
        {
            var c = pattern[patternIndex];
            if (c == '*')
            {
                for (var k = valueIndex; k <= value.Length; k++)
                    if (MatchGlobAt(pattern, value, patternIndex + 1, k))
                        return true;
                return false;
            }
            if (c == '?')
            {
                if (valueIndex >= value.Length)
                    return false;
                patternIndex++;
                valueIndex++;
                continue;
            }
            if (valueIndex >= value.Length || value[valueIndex] != c)
                return false;
            patternIndex++;
            valueIndex++;
        }
        return valueIndex == value.Length;
    }

    private readonly struct Segment
    {
        public bool IsDoubleStar { get; }
        public string Pattern { get; }

        private Segment(bool isDoubleStar, string pattern)
        {
            IsDoubleStar = isDoubleStar;
            Pattern = pattern;
        }

        public static Segment DoubleStar() => new(true, string.Empty);

        public static Segment Glob(string pattern) => new(false, pattern);
    }
}

public sealed class RuleSet
{
    private readonly List<CompiledRule> _rules;

    public IReadOnlyList<CompiledRule> Rules => _rules;
    public bool HasNegation { get; }

    private RuleSet(List<CompiledRule> rules)
    {
        _rules = rules;
        HasNegation = rules.Any(static r => r.Negated);
    }

    public static RuleSet Empty { get; } = new(new List<CompiledRule>());

    public static RuleSet Compile(IEnumerable<string> lines)
    {
        var rules = new List<CompiledRule>();
        var line = 0;
        foreach (var text in lines)
        {
            line++;
            var rule = CompiledRule.TryParse(line, text);
            if (rule is not null)
                rules.Add(rule);
        }
        return new RuleSet(rules);
    }

    public RuleSet Append(RuleSet other)
    {
        var rules = new List<CompiledRule>(_rules);
        rules.AddRange(other._rules);
        return new RuleSet(rules);
    }

    public bool IsExcluded(string relativePath, bool isDirectory)
    {
        var match = Evaluate(relativePath, isDirectory);
        return match is not null && !match.Negated;
    }

    public CompiledRule? Evaluate(string relativePath, bool isDirectory)
    {
        if (relativePath.Length == 0)
            return null;
        var segments = relativePath.Split('/');
        CompiledRule? last = null;
        foreach (var rule in _rules)
            if (rule.Matches(segments, isDirectory))
                last = rule;
        return last;
    }
}
