using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StickmanBeatEmUp.CorePure.Rules.Persistence
{
    /// <summary>
    /// Bộ phân tích và xuất JSON gọn nhẹ thuần C# (0 phụ thuộc UnityEngine/Newtonsoft),
    /// đảm bảo kiểm tra cú pháp chặt chẽ cho cả Unit Test CoreUnit lẫn Runtime.
    /// </summary>
    public static class SimpleJsonCodec
    {
        public static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new FormatException("JSON content is empty or whitespace.");
            }

            Parser parser = new Parser(json);
            object root = parser.ParseRoot();
            Dictionary<string, object> obj = root as Dictionary<string, object>;
            if (obj == null)
            {
                throw new FormatException("Expected JSON root to be an object '{...}'.");
            }

            return obj;
        }

        public static List<object> ParseArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new FormatException("JSON content is empty or whitespace.");
            }

            Parser parser = new Parser(json);
            object root = parser.ParseRoot();
            List<object> arr = root as List<object>;
            if (arr == null)
            {
                throw new FormatException("Expected JSON root to be an array '[...]'.");
            }

            return arr;
        }

        public static string EscapeString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static string FormatFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        public static string ReadString(Dictionary<string, object> map, string key, string defaultValue = "")
        {
            if (map == null || !map.TryGetValue(key, out object raw) || raw == null)
            {
                return defaultValue;
            }
            if (raw is string str)
            {
                return str;
            }
            throw new FormatException($"Field '{key}' must be a string.");
        }

        public static int ReadInt(Dictionary<string, object> map, string key, int defaultValue = 0)
        {
            if (map == null || !map.TryGetValue(key, out object raw) || raw == null)
            {
                return defaultValue;
            }
            if (raw is double num)
            {
                return (int)Math.Round(num);
            }
            throw new FormatException($"Field '{key}' must be an integer.");
        }

        public static float ReadFloat(Dictionary<string, object> map, string key, float defaultValue = 0f)
        {
            if (map == null || !map.TryGetValue(key, out object raw) || raw == null)
            {
                return defaultValue;
            }
            if (raw is double num)
            {
                return (float)num;
            }
            throw new FormatException($"Field '{key}' must be a number.");
        }

        public static bool ReadBool(Dictionary<string, object> map, string key, bool defaultValue = false)
        {
            if (map == null || !map.TryGetValue(key, out object raw) || raw == null)
            {
                return defaultValue;
            }
            if (raw is bool b)
            {
                return b;
            }
            throw new FormatException($"Field '{key}' must be a boolean.");
        }

        public static List<object> ReadArray(Dictionary<string, object> map, string key)
        {
            if (map == null || !map.TryGetValue(key, out object raw) || raw == null)
            {
                return new List<object>();
            }
            if (raw is List<object> list)
            {
                return list;
            }
            throw new FormatException($"Field '{key}' must be an array.");
        }

        private sealed class Parser
        {
            private readonly string text;
            private int pos;

            public Parser(string json)
            {
                text = json;
                pos = 0;
            }

            public object ParseRoot()
            {
                SkipWhitespace();
                object value = ParseValue();
                SkipWhitespace();
                if (pos != text.Length)
                {
                    throw new FormatException($"Unexpected trailing character at index {pos}.");
                }
                return value;
            }

            private object ParseValue()
            {
                SkipWhitespace();
                if (pos >= text.Length)
                {
                    throw new FormatException("Unexpected end of JSON input.");
                }

                char c = text[pos];
                if (c == '{') return ParseObjectInternal();
                if (c == '[') return ParseArrayInternal();
                if (c == '"') return ParseStringInternal();
                if (c == 't') return ParseLiteral("true", true);
                if (c == 'f') return ParseLiteral("false", false);
                if (c == 'n') return ParseLiteral("null", null);
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumberInternal();

                throw new FormatException($"Unexpected token '{c}' at index {pos}.");
            }

            private Dictionary<string, object> ParseObjectInternal()
            {
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                pos++; // consume '{'
                SkipWhitespace();

                if (pos < text.Length && text[pos] == '}')
                {
                    pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (pos >= text.Length || text[pos] != '"')
                    {
                        throw new FormatException($"Expected property name string at index {pos}.");
                    }

                    string key = ParseStringInternal();
                    SkipWhitespace();
                    if (pos >= text.Length || text[pos] != ':')
                    {
                        throw new FormatException($"Expected ':' after property '{key}' at index {pos}.");
                    }

                    pos++; // consume ':'
                    object value = ParseValue();
                    result[key] = value;

                    SkipWhitespace();
                    if (pos >= text.Length)
                    {
                        throw new FormatException("Unterminated JSON object.");
                    }

                    if (text[pos] == '}')
                    {
                        pos++;
                        return result;
                    }

                    if (text[pos] != ',')
                    {
                        throw new FormatException($"Expected ',' or '}}' at index {pos}.");
                    }
                    pos++; // consume ','
                }
            }

            private List<object> ParseArrayInternal()
            {
                List<object> list = new List<object>();
                pos++; // consume '['
                SkipWhitespace();

                if (pos < text.Length && text[pos] == ']')
                {
                    pos++;
                    return list;
                }

                while (true)
                {
                    object item = ParseValue();
                    list.Add(item);

                    SkipWhitespace();
                    if (pos >= text.Length)
                    {
                        throw new FormatException("Unterminated JSON array.");
                    }

                    if (text[pos] == ']')
                    {
                        pos++;
                        return list;
                    }

                    if (text[pos] != ',')
                    {
                        throw new FormatException($"Expected ',' or ']' at index {pos}.");
                    }
                    pos++; // consume ','
                }
            }

            private string ParseStringInternal()
            {
                if (pos >= text.Length || text[pos] != '"')
                {
                    throw new FormatException($"Expected '\"' at index {pos}.");
                }
                pos++; // consume opening '"'

                StringBuilder sb = new StringBuilder();
                while (pos < text.Length)
                {
                    char c = text[pos++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }

                    if (c == '\\')
                    {
                        if (pos >= text.Length)
                        {
                            throw new FormatException("Unterminated escape sequence in JSON string.");
                        }

                        char esc = text[pos++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (pos + 4 > text.Length)
                                {
                                    throw new FormatException("Invalid unicode escape in JSON string.");
                                }
                                string hex = text.Substring(pos, 4);
                                if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
                                {
                                    throw new FormatException($"Invalid unicode hex '{hex}'.");
                                }
                                sb.Append((char)codePoint);
                                pos += 4;
                                break;
                            default:
                                throw new FormatException($"Unsupported escape '\\{esc}'.");
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private object ParseLiteral(string literal, object value)
            {
                if (pos + literal.Length > text.Length ||
                    string.Compare(text, pos, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
                {
                    throw new FormatException($"Expected '{literal}' at index {pos}.");
                }
                pos += literal.Length;
                return value;
            }

            private double ParseNumberInternal()
            {
                int start = pos;
                if (text[pos] == '-') pos++;
                if (pos >= text.Length || text[pos] < '0' || text[pos] > '9')
                {
                    throw new FormatException($"Invalid number at index {start}.");
                }

                while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++;

                if (pos < text.Length && text[pos] == '.')
                {
                    pos++;
                    if (pos >= text.Length || text[pos] < '0' || text[pos] > '9')
                    {
                        throw new FormatException($"Invalid decimal fraction at index {start}.");
                    }
                    while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++;
                }

                if (pos < text.Length && (text[pos] == 'e' || text[pos] == 'E'))
                {
                    pos++;
                    if (pos < text.Length && (text[pos] == '+' || text[pos] == '-')) pos++;
                    if (pos >= text.Length || text[pos] < '0' || text[pos] > '9')
                    {
                        throw new FormatException($"Invalid exponent at index {start}.");
                    }
                    while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++;
                }

                string raw = text.Substring(start, pos - start);
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    throw new FormatException($"Could not parse number '{raw}'.");
                }
                return value;
            }

            private void SkipWhitespace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                {
                    pos++;
                }
            }
        }
    }
}
