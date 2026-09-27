namespace RepoLore.Core.Context;

public static class SessionId
{
    public const int MaxLength = 100;

    private static readonly string[] ReservedBaseNames = { "CON", "PRN", "AUX", "NUL" };

    public static bool IsValid(string? id, out string? finding)
    {
        finding = null;

        if (string.IsNullOrEmpty(id))
        {
            finding = "session id must not be empty";
            return false;
        }

        if (id.Length > MaxLength)
        {
            finding = $"session id exceeds {MaxLength} characters";
            return false;
        }

        if (!IsAsciiAlphanumeric(id[0]))
        {
            finding = "session id must start with an ASCII letter or digit";
            return false;
        }

        for (var i = 1; i < id.Length; i++)
        {
            var c = id[i];
            if (!IsAsciiAlphanumeric(c) && c != '_' && c != '-')
            {
                finding = $"session id contains an unsupported character '{c}'";
                return false;
            }
        }

        if (IsReservedDeviceName(id))
        {
            finding = $"session id '{id}' is a reserved device name";
            return false;
        }

        return true;
    }

    private static bool IsAsciiAlphanumeric(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool IsReservedDeviceName(string id)
    {
        var upper = id.ToUpperInvariant();
        if (Array.IndexOf(ReservedBaseNames, upper) >= 0)
            return true;

        if (upper.Length == 4
            && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal)))
        {
            var last = upper[3];
            return last is >= '1' and <= '9';
        }

        return false;
    }
}
