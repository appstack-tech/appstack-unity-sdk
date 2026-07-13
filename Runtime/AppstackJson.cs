using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Appstack
{
    internal static class AppstackJson
    {
        public static string SerializeObject(IDictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append('{');
            var isFirst = true;
            foreach (var pair in values)
            {
                if (!isFirst)
                {
                    builder.Append(',');
                }

                isFirst = false;
                AppendString(builder, pair.Key);
                builder.Append(':');
                AppendValue(builder, pair.Value);
            }

            builder.Append('}');
            return builder.ToString();
        }

        public static Dictionary<string, object> ParseObject(string json)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            var cursor = 0;
            SkipWhitespace(json, ref cursor);
            if (!Consume(json, ref cursor, '{'))
            {
                return result;
            }

            while (cursor < json.Length)
            {
                SkipWhitespace(json, ref cursor);
                if (Consume(json, ref cursor, '}'))
                {
                    return result;
                }

                var key = ReadString(json, ref cursor);
                SkipWhitespace(json, ref cursor);
                if (key == null || !Consume(json, ref cursor, ':'))
                {
                    return new Dictionary<string, object>();
                }

                SkipWhitespace(json, ref cursor);
                result[key] = ReadValue(json, ref cursor);
                SkipWhitespace(json, ref cursor);

                if (Consume(json, ref cursor, ','))
                {
                    continue;
                }

                if (Consume(json, ref cursor, '}'))
                {
                    return result;
                }

                return new Dictionary<string, object>();
            }

            return new Dictionary<string, object>();
        }

        private static object ReadValue(string json, ref int cursor)
        {
            if (cursor >= json.Length)
            {
                return null;
            }

            if (json[cursor] == '"')
            {
                return ReadString(json, ref cursor);
            }

            var start = cursor;
            while (cursor < json.Length && json[cursor] != ',' && json[cursor] != '}')
            {
                cursor++;
            }

            var value = json.Substring(start, cursor - start).Trim();
            if (value == "true") return true;
            if (value == "false") return false;
            if (value == "null") return null;
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                return integer;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return number;
            return value;
        }

        private static void AppendValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string stringValue)
            {
                AppendString(builder, stringValue);
                return;
            }

            if (value is bool boolValue)
            {
                builder.Append(boolValue ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is float || value is double)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            AppendString(builder, value.ToString());
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                builder.Append(
                    value
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r"));
            }

            builder.Append('"');
        }

        private static string ReadString(string json, ref int cursor)
        {
            if (!Consume(json, ref cursor, '"'))
            {
                return null;
            }

            var builder = new StringBuilder();
            while (cursor < json.Length)
            {
                var character = json[cursor++];
                if (character == '"')
                {
                    return builder.ToString();
                }

                if (character != '\\' || cursor >= json.Length)
                {
                    builder.Append(character);
                    continue;
                }

                var escaped = json[cursor++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (cursor + 4 <= json.Length &&
                            int.TryParse(
                                json.Substring(cursor, 4),
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out var codePoint))
                        {
                            builder.Append((char)codePoint);
                            cursor += 4;
                        }
                        break;
                }
            }

            return null;
        }

        private static bool Consume(string json, ref int cursor, char expected)
        {
            if (cursor >= json.Length || json[cursor] != expected)
            {
                return false;
            }

            cursor++;
            return true;
        }

        private static void SkipWhitespace(string json, ref int cursor)
        {
            while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
            {
                cursor++;
            }
        }
    }
}
