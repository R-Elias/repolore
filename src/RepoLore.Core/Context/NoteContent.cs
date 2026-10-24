using System.Text;

namespace RepoLore.Core.Context;

public static class NoteContent
{
    public const string EmptyMarker = "<!-- repolore:empty -->";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static bool TryNormalize(byte[] bytes, out string? text, out string? finding)
    {
        text = null;
        finding = null;

        var start = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            start = 3;

        var content = start == 0 ? bytes : bytes[start..];

        string decoded;
        try
        {
            decoded = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            finding = "note is not valid UTF-8";
            return false;
        }

        var normalized = decoded.Replace("\r\n", "\n").Replace('\r', '\n');
        text = normalized.Trim();
        return true;
    }

    public static bool IsEmpty(string text) => text.Length == 0 || text == EmptyMarker;
}
