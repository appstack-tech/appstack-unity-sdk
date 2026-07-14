using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Appstack
{
    internal static class AppstackJson
    {
        private const int MaximumDepth = 64;

        public static string SerializeObject(IDictionary<string, object> values)
        {
            if (values == null || values.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            var ancestors = new HashSet<object>(ReferenceComparer.Instance);
            AppendStringObjectDictionary(builder, values, ancestors, 0);
            return builder.ToString();
        }

        public static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, object>();
            }

            var parser = new Parser(json);
            return parser.TryParseObject(out var result)
                ? result
                : new Dictionary<string, object>();
        }

        private static void AppendValue(
            StringBuilder builder,
            object value,
            HashSet<object> ancestors,
            int depth)
        {
            if (depth > MaximumDepth)
            {
                throw new ArgumentException(
                    $"JSON values cannot be nested more than {MaximumDepth} levels.");
            }

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

            if (value is char character)
            {
                AppendString(builder, character.ToString());
                return;
            }

            if (value is bool boolean)
            {
                builder.Append(boolean ? "true" : "false");
                return;
            }

            if (value is float single)
            {
                if (float.IsNaN(single) || float.IsInfinity(single))
                {
                    throw new ArgumentException("JSON numbers must be finite.");
                }

                builder.Append(single.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is double number)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    throw new ArgumentException("JSON numbers must be finite.");
                }

                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (IsIntegralNumber(value) || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dictionary)
            {
                AppendDictionary(builder, dictionary, ancestors, depth + 1);
                return;
            }

            if (value is IEnumerable enumerable)
            {
                AppendArray(builder, enumerable, ancestors, depth + 1);
                return;
            }

            throw new ArgumentException(
                $"Values of type {value.GetType().FullName} are not JSON-compatible.");
        }

        private static void AppendDictionary(
            StringBuilder builder,
            IDictionary dictionary,
            HashSet<object> ancestors,
            int depth)
        {
            EnterContainer(dictionary, ancestors);
            try
            {
                builder.Append('{');
                var isFirst = true;
                foreach (DictionaryEntry pair in dictionary)
                {
                    if (!(pair.Key is string key))
                    {
                        throw new ArgumentException("JSON object keys must be non-null strings.");
                    }

                    if (!isFirst)
                    {
                        builder.Append(',');
                    }

                    isFirst = false;
                    AppendString(builder, key);
                    builder.Append(':');
                    AppendValue(builder, pair.Value, ancestors, depth);
                }

                builder.Append('}');
            }
            finally
            {
                ancestors.Remove(dictionary);
            }
        }

        private static void AppendStringObjectDictionary(
            StringBuilder builder,
            IDictionary<string, object> dictionary,
            HashSet<object> ancestors,
            int depth)
        {
            EnterContainer(dictionary, ancestors);
            try
            {
                builder.Append('{');
                var isFirst = true;
                foreach (var pair in dictionary)
                {
                    if (pair.Key == null)
                    {
                        throw new ArgumentException("JSON object keys must be non-null strings.");
                    }

                    if (!isFirst)
                    {
                        builder.Append(',');
                    }

                    isFirst = false;
                    AppendString(builder, pair.Key);
                    builder.Append(':');
                    AppendValue(builder, pair.Value, ancestors, depth);
                }

                builder.Append('}');
            }
            finally
            {
                ancestors.Remove(dictionary);
            }
        }

        private static void AppendArray(
            StringBuilder builder,
            IEnumerable values,
            HashSet<object> ancestors,
            int depth)
        {
            EnterContainer(values, ancestors);
            try
            {
                builder.Append('[');
                var isFirst = true;
                foreach (var value in values)
                {
                    if (!isFirst)
                    {
                        builder.Append(',');
                    }

                    isFirst = false;
                    AppendValue(builder, value, ancestors, depth);
                }

                builder.Append(']');
            }
            finally
            {
                ancestors.Remove(values);
            }
        }

        private static void EnterContainer(object container, HashSet<object> ancestors)
        {
            if (!ancestors.Add(container))
            {
                throw new ArgumentException("JSON values cannot contain reference cycles.");
            }
        }

        private static bool IsIntegralNumber(object value)
        {
            return value is sbyte || value is byte ||
                   value is short || value is ushort ||
                   value is int || value is uint ||
                   value is long || value is ulong;
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (value != null)
            {
                foreach (var character in value)
                {
                    switch (character)
                    {
                        case '"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (character < 0x20)
                            {
                                builder.Append("\\u");
                                builder.Append(((int)character).ToString("x4"));
                            }
                            else
                            {
                                builder.Append(character);
                            }
                            break;
                    }
                }
            }

            builder.Append('"');
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }

        private sealed class Parser
        {
            private readonly string json;
            private int cursor;

            public Parser(string jsonValue)
            {
                json = jsonValue;
            }

            public bool TryParseObject(out Dictionary<string, object> result)
            {
                SkipWhitespace();
                if (!TryReadObject(out result, 0))
                {
                    return false;
                }

                SkipWhitespace();
                return cursor == json.Length;
            }

            private bool TryReadValue(out object result, int depth)
            {
                result = null;
                if (depth > MaximumDepth)
                {
                    return false;
                }

                SkipWhitespace();
                if (cursor >= json.Length)
                {
                    return false;
                }

                switch (json[cursor])
                {
                    case '"':
                        if (TryReadString(out var stringValue))
                        {
                            result = stringValue;
                            return true;
                        }
                        return false;
                    case '{':
                        if (TryReadObject(out var objectValue, depth + 1))
                        {
                            result = objectValue;
                            return true;
                        }
                        return false;
                    case '[':
                        if (TryReadArray(out var arrayValue, depth + 1))
                        {
                            result = arrayValue;
                            return true;
                        }
                        return false;
                    case 't':
                        if (ConsumeLiteral("true"))
                        {
                            result = true;
                            return true;
                        }
                        return false;
                    case 'f':
                        if (ConsumeLiteral("false"))
                        {
                            result = false;
                            return true;
                        }
                        return false;
                    case 'n':
                        return ConsumeLiteral("null");
                    default:
                        return TryReadNumber(out result);
                }
            }

            private bool TryReadObject(
                out Dictionary<string, object> result,
                int depth)
            {
                result = new Dictionary<string, object>();
                if (depth > MaximumDepth || !Consume('{'))
                {
                    return false;
                }

                SkipWhitespace();
                if (Consume('}'))
                {
                    return true;
                }

                while (cursor < json.Length)
                {
                    if (!TryReadString(out var key))
                    {
                        return false;
                    }

                    SkipWhitespace();
                    if (!Consume(':') || !TryReadValue(out var value, depth))
                    {
                        return false;
                    }

                    result[key] = value;
                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        return true;
                    }

                    if (!Consume(','))
                    {
                        return false;
                    }

                    SkipWhitespace();
                }

                return false;
            }

            private bool TryReadArray(out List<object> result, int depth)
            {
                result = new List<object>();
                if (depth > MaximumDepth || !Consume('['))
                {
                    return false;
                }

                SkipWhitespace();
                if (Consume(']'))
                {
                    return true;
                }

                while (cursor < json.Length)
                {
                    if (!TryReadValue(out var value, depth))
                    {
                        return false;
                    }

                    result.Add(value);
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return true;
                    }

                    if (!Consume(','))
                    {
                        return false;
                    }

                    SkipWhitespace();
                }

                return false;
            }

            private bool TryReadString(out string result)
            {
                result = null;
                if (!Consume('"'))
                {
                    return false;
                }

                var builder = new StringBuilder();
                while (cursor < json.Length)
                {
                    var character = json[cursor++];
                    if (character == '"')
                    {
                        result = builder.ToString();
                        return true;
                    }

                    if (character < 0x20)
                    {
                        return false;
                    }

                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (cursor >= json.Length)
                    {
                        return false;
                    }

                    switch (json[cursor++])
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (!TryReadUnicodeEscape(out var escaped))
                            {
                                return false;
                            }
                            builder.Append(escaped);
                            break;
                        default:
                            return false;
                    }
                }

                return false;
            }

            private bool TryReadUnicodeEscape(out char result)
            {
                result = default;
                if (cursor + 4 > json.Length ||
                    !ushort.TryParse(
                        json.Substring(cursor, 4),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var codePoint))
                {
                    return false;
                }

                cursor += 4;
                result = (char)codePoint;
                return true;
            }

            private bool TryReadNumber(out object result)
            {
                result = null;
                var start = cursor;
                Consume('-');

                if (Consume('0'))
                {
                    if (cursor < json.Length && char.IsDigit(json[cursor]))
                    {
                        return false;
                    }
                }
                else if (!ConsumeDigits())
                {
                    return false;
                }

                var isFloatingPoint = false;
                if (Consume('.'))
                {
                    isFloatingPoint = true;
                    if (!ConsumeDigits())
                    {
                        return false;
                    }
                }

                if (cursor < json.Length &&
                    (json[cursor] == 'e' || json[cursor] == 'E'))
                {
                    isFloatingPoint = true;
                    cursor++;
                    if (cursor < json.Length &&
                        (json[cursor] == '+' || json[cursor] == '-'))
                    {
                        cursor++;
                    }

                    if (!ConsumeDigits())
                    {
                        return false;
                    }
                }

                var token = json.Substring(start, cursor - start);
                if (!isFloatingPoint &&
                    long.TryParse(
                        token,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var integer))
                {
                    result = integer;
                    return true;
                }

                if (double.TryParse(
                        token,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var number) &&
                    !double.IsNaN(number) &&
                    !double.IsInfinity(number))
                {
                    result = number;
                    return true;
                }

                return false;
            }

            private bool ConsumeDigits()
            {
                var start = cursor;
                while (cursor < json.Length && char.IsDigit(json[cursor]))
                {
                    cursor++;
                }

                return cursor > start;
            }

            private bool ConsumeLiteral(string literal)
            {
                if (cursor + literal.Length > json.Length ||
                    string.CompareOrdinal(json, cursor, literal, 0, literal.Length) != 0)
                {
                    return false;
                }

                cursor += literal.Length;
                return true;
            }

            private bool Consume(char expected)
            {
                if (cursor >= json.Length || json[cursor] != expected)
                {
                    return false;
                }

                cursor++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (cursor < json.Length && char.IsWhiteSpace(json[cursor]))
                {
                    cursor++;
                }
            }
        }
    }
}
