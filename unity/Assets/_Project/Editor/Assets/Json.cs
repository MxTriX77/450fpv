using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Fpv450.Assets
{
    /// <summary>
    /// Reads the pipeline's JSON files (the catalog, material recipes, the manifest and <c>.assetmeta.json</c>)
    /// into <c>Dictionary&lt;string, object&gt;</c>, <c>List&lt;object&gt;</c>, <c>double</c>, <c>string</c>,
    /// <c>bool</c> and <c>null</c>, with typed getters that name the file and key in their errors. JsonUtility
    /// reads neither dynamic keys (the catalog) nor nested arrays (gap polygons), and the project adds no JSON
    /// package for this.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var reader = new Reader(text);
            object value = reader.Value();
            reader.End();
            return value;
        }

        public static Dictionary<string, object> Obj(object value, string what) =>
            value as Dictionary<string, object> ?? throw new FormatException($"{what}: expected an object");

        public static List<object> Arr(object value, string what) =>
            value as List<object> ?? throw new FormatException($"{what}: expected an array");

        public static object Get(Dictionary<string, object> obj, string key, string what) =>
            obj.TryGetValue(key, out object value) ? value : throw new FormatException($"{what}: missing \"{key}\"");

        public static Dictionary<string, object> Obj(Dictionary<string, object> obj, string key, string what) =>
            Obj(Get(obj, key, what), $"{what} \"{key}\"");

        public static List<object> Arr(Dictionary<string, object> obj, string key, string what) =>
            Arr(Get(obj, key, what), $"{what} \"{key}\"");

        public static string Str(Dictionary<string, object> obj, string key, string what) =>
            Get(obj, key, what) as string ?? throw new FormatException($"{what}: \"{key}\" must be a string");

        public static float Num(Dictionary<string, object> obj, string key, string what) =>
            Get(obj, key, what) is double d ? (float)d : throw new FormatException($"{what}: \"{key}\" must be a number");

        public static bool Bool(Dictionary<string, object> obj, string key, string what) =>
            Get(obj, key, what) is bool b ? b : throw new FormatException($"{what}: \"{key}\" must be true or false");

        public static Vector2 Vec2(Dictionary<string, object> obj, string key, string what)
        {
            float[] c = Numbers(Get(obj, key, what), 2, $"{what} \"{key}\"");
            return new Vector2(c[0], c[1]);
        }

        public static Vector3 Vec3(object value, string what)
        {
            float[] c = Numbers(value, 3, what);
            return new Vector3(c[0], c[1], c[2]);
        }

        public static Vector3 Vec3(Dictionary<string, object> obj, string key, string what) =>
            Vec3(Get(obj, key, what), $"{what} \"{key}\"");

        /// <summary>A quaternion written as [x, y, z, w].</summary>
        public static Quaternion Quat(Dictionary<string, object> obj, string key, string what)
        {
            float[] c = Numbers(Get(obj, key, what), 4, $"{what} \"{key}\"");
            return new Quaternion(c[0], c[1], c[2], c[3]);
        }

        public static Color Rgba(Dictionary<string, object> obj, string key, string what)
        {
            float[] c = Numbers(Get(obj, key, what), 4, $"{what} \"{key}\"");
            return new Color(c[0], c[1], c[2], c[3]);
        }

        static float[] Numbers(object value, int count, string what)
        {
            List<object> list = Arr(value, what);
            if (list.Count != count || !list.All(x => x is double))
                throw new FormatException($"{what}: expected {count} numbers");
            return list.Select(x => (float)(double)x).ToArray();
        }

        sealed class Reader
        {
            readonly string text;
            int pos;

            public Reader(string text) => this.text = text;

            public object Value()
            {
                SkipWhitespace();
                if (pos >= text.Length)
                    throw Error("unexpected end of text");
                switch (text[pos])
                {
                    case '{': return Object();
                    case '[': return Array();
                    case '"': return String();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return Number();
                }
            }

            public void End()
            {
                SkipWhitespace();
                if (pos != text.Length)
                    throw Error("text after the value");
            }

            Dictionary<string, object> Object()
            {
                var result = new Dictionary<string, object>();
                pos++;
                SkipWhitespace();
                if (Take('}'))
                    return result;
                while (true)
                {
                    SkipWhitespace();
                    if (pos >= text.Length || text[pos] != '"')
                        throw Error("expected a key");
                    string key = String();
                    SkipWhitespace();
                    if (!Take(':'))
                        throw Error("expected ':'");
                    result[key] = Value();
                    SkipWhitespace();
                    if (Take(','))
                        continue;
                    if (Take('}'))
                        return result;
                    throw Error("expected ',' or '}'");
                }
            }

            List<object> Array()
            {
                var result = new List<object>();
                pos++;
                SkipWhitespace();
                if (Take(']'))
                    return result;
                while (true)
                {
                    result.Add(Value());
                    SkipWhitespace();
                    if (Take(','))
                        continue;
                    if (Take(']'))
                        return result;
                    throw Error("expected ',' or ']'");
                }
            }

            string String()
            {
                var result = new StringBuilder();
                pos++;
                while (true)
                {
                    if (pos >= text.Length)
                        throw Error("unterminated string");
                    char c = text[pos++];
                    if (c == '"')
                        return result.ToString();
                    if (c != '\\')
                    {
                        result.Append(c);
                        continue;
                    }
                    if (pos >= text.Length)
                        throw Error("unterminated escape");
                    char escaped = text[pos++];
                    switch (escaped)
                    {
                        case '"': case '\\': case '/': result.Append(escaped); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > text.Length)
                                throw Error("short \\u escape");
                            result.Append((char)int.Parse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 4;
                            break;
                        default: throw Error($"unknown escape \\{escaped}");
                    }
                }
            }

            double Number()
            {
                int start = pos;
                while (pos < text.Length && "+-0123456789.eE".IndexOf(text[pos]) >= 0)
                    pos++;
                if (pos == start || !double.TryParse(text.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw Error("expected a value");
                return value;
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(text, pos, word, 0, word.Length) != 0)
                    throw Error($"expected {word}");
                pos += word.Length;
            }

            bool Take(char c)
            {
                if (pos >= text.Length || text[pos] != c)
                    return false;
                pos++;
                return true;
            }

            void SkipWhitespace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                    pos++;
            }

            FormatException Error(string what) => new FormatException($"JSON: {what} at character {pos}");
        }
    }
}
