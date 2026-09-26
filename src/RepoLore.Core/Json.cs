using System.Globalization;
using System.Text;

namespace RepoLore.Core;

public sealed class JsonParseException : Exception
{
    public int Position { get; }

    public JsonParseException(string message, int position) : base(message) { Position = position; }
}

public abstract class JsonValue { }

public sealed class JsonObject : JsonValue
{
    public List<JsonMember> Members { get; } = new();
}

public sealed class JsonMember
{
    public JsonMember(string name, JsonValue value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public JsonValue Value { get; }
}

public sealed class JsonArray : JsonValue
{
    public List<JsonValue> Items { get; } = new();
}

public sealed class JsonStringValue : JsonValue
{
    public JsonStringValue(string value) { Value = value; }
    public string Value { get; }
}

public sealed class JsonNumberValue : JsonValue
{
    public JsonNumberValue(string raw) { Raw = raw; }
    public string Raw { get; }
}

public sealed class JsonBooleanValue : JsonValue
{
    public JsonBooleanValue(bool value) { Value = value; }
    public bool Value { get; }
}

public sealed class JsonNullValue : JsonValue { }

public static class JsonParser
{
    public static JsonValue Parse(string text)
    {
        var parser = new Parser(text);
        var value = parser.ReadValue();
        parser.SkipWhitespace();
        if (!parser.AtEnd)
            throw parser.Fail("unexpected trailing content after the JSON value");
        return value;
    }

    private sealed class Parser
    {
        private readonly string _text;
        private int _index;

        public Parser(string text) { _text = text; }

        public bool AtEnd => _index >= _text.Length;

        public JsonParseException Fail(string message) => new($"{message} at position {_index}", _index);

        public void SkipWhitespace()
        {
            while (!AtEnd && IsWhitespace(_text[_index]))
                _index++;
        }

        public JsonValue ReadValue()
        {
            SkipWhitespace();
            if (AtEnd)
                throw Fail("expected a JSON value");
            return _text[_index] switch
            {
                '{' => ReadObject(),
                '[' => ReadArray(),
                '"' => new JsonStringValue(ReadString()),
                't' => ReadLiteral("true", new JsonBooleanValue(true)),
                'f' => ReadLiteral("false", new JsonBooleanValue(false)),
                'n' => ReadLiteral("null", new JsonNullValue()),
                _ => ReadNumber()
            };
        }

        private JsonValue ReadLiteral(string literal, JsonValue value)
        {
            if (_index + literal.Length > _text.Length
                || string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                throw Fail("invalid literal");
            _index += literal.Length;
            return value;
        }

        private JsonObject ReadObject()
        {
            var obj = new JsonObject();
            _index++;
            SkipWhitespace();
            if (!AtEnd && _text[_index] == '}')
            {
                _index++;
                return obj;
            }

            while (true)
            {
                SkipWhitespace();
                if (AtEnd || _text[_index] != '"')
                    throw Fail("expected a string key in an object");
                var name = ReadString();
                SkipWhitespace();
                if (AtEnd || _text[_index] != ':')
                    throw Fail("expected ':' after an object key");
                _index++;
                obj.Members.Add(new JsonMember(name, ReadValue()));
                SkipWhitespace();
                if (AtEnd)
                    throw Fail("unterminated object");
                if (_text[_index] == ',')
                {
                    _index++;
                    continue;
                }
                if (_text[_index] == '}')
                {
                    _index++;
                    return obj;
                }
                throw Fail("expected ',' or '}' in an object");
            }
        }

        private JsonArray ReadArray()
        {
            var array = new JsonArray();
            _index++;
            SkipWhitespace();
            if (!AtEnd && _text[_index] == ']')
            {
                _index++;
                return array;
            }

            while (true)
            {
                array.Items.Add(ReadValue());
                SkipWhitespace();
                if (AtEnd)
                    throw Fail("unterminated array");
                if (_text[_index] == ',')
                {
                    _index++;
                    continue;
                }
                if (_text[_index] == ']')
                {
                    _index++;
                    return array;
                }
                throw Fail("expected ',' or ']' in an array");
            }
        }

        private string ReadString()
        {
            if (AtEnd || _text[_index] != '"')
                throw Fail("expected '\"'");
            _index++;
            var builder = new StringBuilder();
            while (true)
            {
                if (AtEnd)
                    throw Fail("unterminated string");
                var c = _text[_index];
                if (c == '"')
                {
                    _index++;
                    return builder.ToString();
                }
                if (c != '\\')
                {
                    builder.Append(c);
                    _index++;
                    continue;
                }
                _index++;
                if (AtEnd)
                    throw Fail("unterminated escape");
                var escape = _text[_index];
                _index++;
                builder.Append(escape switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    '/' => '/',
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'u' => ReadUnicodeEscape(),
                    _ => throw Fail($"invalid escape '\\{escape}'")
                });
            }
        }

        private char ReadUnicodeEscape()
        {
            if (_index + 4 > _text.Length)
                throw Fail("truncated unicode escape");
            var code = 0;
            for (var k = 0; k < 4; k++)
            {
                var hex = HexValue(_text[_index + k]);
                if (hex < 0)
                    throw Fail("invalid unicode escape");
                code = (code << 4) | hex;
            }
            _index += 4;
            return (char)code;
        }

        private JsonNumberValue ReadNumber()
        {
            var start = _index;
            if (!AtEnd && _text[_index] == '-')
                _index++;
            var digits = 0;
            while (!AtEnd && IsDigit(_text[_index]))
            {
                _index++;
                digits++;
            }
            if (digits == 0)
                throw Fail("expected a number");

            if (!AtEnd && _text[_index] == '.')
            {
                _index++;
                var fractionDigits = 0;
                while (!AtEnd && IsDigit(_text[_index]))
                {
                    _index++;
                    fractionDigits++;
                }
                if (fractionDigits == 0)
                    throw Fail("expected digits after a decimal point");
            }

            if (!AtEnd && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                _index++;
                if (!AtEnd && (_text[_index] == '+' || _text[_index] == '-'))
                    _index++;
                var exponentDigits = 0;
                while (!AtEnd && IsDigit(_text[_index]))
                {
                    _index++;
                    exponentDigits++;
                }
                if (exponentDigits == 0)
                    throw Fail("expected digits in an exponent");
            }

            return new JsonNumberValue(_text[start.._index]);
        }

        private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r';

        private static bool IsDigit(char c) => c is >= '0' and <= '9';

        private static int HexValue(char c) => c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1
        };
    }
}

public static class JsonWriter
{
    public static string Write(JsonValue value)
    {
        var builder = new StringBuilder();
        WriteTo(value, builder);
        return builder.ToString();
    }

    private static void WriteTo(JsonValue value, StringBuilder builder)
    {
        switch (value)
        {
            case JsonObject obj:
                WriteObject(obj, builder);
                break;
            case JsonArray array:
                WriteArray(array, builder);
                break;
            case JsonStringValue text:
                builder.Append('"');
                AppendEscaped(text.Value, builder);
                builder.Append('"');
                break;
            case JsonNumberValue number:
                builder.Append(number.Raw);
                break;
            case JsonBooleanValue boolean:
                builder.Append(boolean.Value ? "true" : "false");
                break;
            case JsonNullValue:
                builder.Append("null");
                break;
            default:
                throw new InvalidOperationException("Unknown JSON value type.");
        }
    }

    private static void WriteObject(JsonObject obj, StringBuilder builder)
    {
        builder.Append('{');
        for (var i = 0; i < obj.Members.Count; i++)
        {
            if (i > 0)
                builder.Append(',');
            var member = obj.Members[i];
            builder.Append('"');
            AppendEscaped(member.Name, builder);
            builder.Append("\":");
            WriteTo(member.Value, builder);
        }
        builder.Append('}');
    }

    private static void WriteArray(JsonArray array, StringBuilder builder)
    {
        builder.Append('[');
        for (var i = 0; i < array.Items.Count; i++)
        {
            if (i > 0)
                builder.Append(',');
            WriteTo(array.Items[i], builder);
        }
        builder.Append(']');
    }

    private static void AppendEscaped(string value, StringBuilder builder)
    {
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < ' ')
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        builder.Append(c);
                    break;
            }
        }
    }
}
