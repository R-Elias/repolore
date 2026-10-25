using System.Text;

namespace RepoLore.Core.Health;

public static class MarkdownLinks
{
    public static List<string> ExtractLocalTargets(string text)
    {
        var targets = new List<string>();
        var inFence = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence)
                continue;

            ScanLine(RemoveInlineCode(line), targets);
        }

        return targets;
    }

    private static string RemoveInlineCode(string line)
    {
        var kept = new System.Text.StringBuilder(line.Length);
        var inCode = false;
        foreach (var c in line)
        {
            if (c == '`')
            {
                inCode = !inCode;
                continue;
            }
            if (!inCode)
                kept.Append(c);
        }
        return kept.ToString();
    }

    private static void ScanLine(string line, List<string> targets)
    {
        var from = 0;
        while (true)
        {
            var open = line.IndexOf("](", from, StringComparison.Ordinal);
            if (open < 0)
                return;
            from = open + 2;

            if (line.LastIndexOf('[', open) < 0)
                continue;

            var target = ReadTarget(line, from);
            var local = ToLocalFileTarget(target);
            if (local is not null && !targets.Contains(local))
                targets.Add(local);
        }
    }

    private static string ReadTarget(string line, int start)
    {
        if (start < line.Length && line[start] == '<')
        {
            var close = line.IndexOf('>', start + 1);
            return close < 0 ? string.Empty : line.Substring(start + 1, close - start - 1);
        }

        var end = start;
        while (end < line.Length && line[end] != ')' && !char.IsWhiteSpace(line[end]))
            end++;
        return line.Substring(start, end - start);
    }

    private static string? ToLocalFileTarget(string target)
    {
        if (target.Length == 0 || target[0] == '#' || target[0] == '/')
            return null;
        if (HasScheme(target))
            return null;

        var fragment = target.IndexOf('#');
        var file = fragment < 0 ? target : target[..fragment];
        return file.Length == 0 ? null : PercentDecode(file);
    }

    // Markdown renderers resolve 'Design%20Notes.md' to 'Design Notes.md', so the check does too. Decoding happens
    // before the path is resolved, so an encoded '..' is still caught as an escape. A target whose escapes are not
    // valid UTF-8 is kept as written.
    private static string PercentDecode(string target)
    {
        if (!target.Contains('%'))
            return target;

        var source = Encoding.UTF8.GetBytes(target);
        var decoded = new List<byte>(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == (byte)'%' && i + 2 < source.Length && IsHex(source[i + 1]) && IsHex(source[i + 2]))
            {
                decoded.Add((byte)(HexValue(source[i + 1]) * 16 + HexValue(source[i + 2])));
                i += 2;
                continue;
            }
            decoded.Add(source[i]);
        }

        try
        {
            return StrictUtf8.GetString(decoded.ToArray());
        }
        catch (DecoderFallbackException)
        {
            return target;
        }
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static bool IsHex(byte b) => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'a' and <= (byte)'f' or >= (byte)'A' and <= (byte)'F';

    private static int HexValue(byte b) => b <= (byte)'9' ? b - '0' : (b | 0x20) - 'a' + 10;

    private static bool HasScheme(string target)
    {
        var colon = target.IndexOf(':');
        if (colon <= 0)
            return false;
        for (var i = 0; i < colon; i++)
            if (!char.IsAsciiLetterOrDigit(target[i]) && target[i] != '+' && target[i] != '-' && target[i] != '.')
                return false;
        return char.IsAsciiLetter(target[0]);
    }
}
