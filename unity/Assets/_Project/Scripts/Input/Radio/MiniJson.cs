using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// A small strict JSON reader and an indenting writer for the app's own settings files (calibration profiles, and
    /// the rates in 1.3), engine-free so the tests run outside Unity. Not for the sim core, which reads its files with
    /// DetJson. An object reads as a Dictionary of string to value, an array as a List, a number as a double.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = Value(text, ref i);
            Space(text, ref i);
            if (i != text.Length) throw Error(text, i, "text after the value");
            return value;
        }

        static object Value(string s, ref int i)
        {
            Space(s, ref i);
            if (i >= s.Length) throw Error(s, i, "a value");
            char c = s[i];
            if (c == '{')
            {
                var o = new Dictionary<string, object>();
                i++;
                Space(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return o; }
                while (true)
                {
                    Space(s, ref i);
                    string key = String(s, ref i);
                    Space(s, ref i);
                    Expect(s, ref i, ':');
                    o[key] = Value(s, ref i);
                    Space(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    Expect(s, ref i, '}');
                    return o;
                }
            }
            if (c == '[')
            {
                var a = new List<object>();
                i++;
                Space(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return a; }
                while (true)
                {
                    a.Add(Value(s, ref i));
                    Space(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    Expect(s, ref i, ']');
                    return a;
                }
            }
            if (c == '"') return String(s, ref i);
            if (Word(s, ref i, "true")) return true;
            if (Word(s, ref i, "false")) return false;
            if (Word(s, ref i, "null")) return null;
            int from = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == from || !double.TryParse(s.Substring(from, i - from), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                throw Error(s, from, "a value");
            return d;
        }

        static string String(string s, ref int i)
        {
            Expect(s, ref i, '"');
            var b = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Error(s, i, "the end of a string");
                char c = s[i++];
                if (c == '"') return b.ToString();
                if (c != '\\')
                {
                    b.Append(c);
                    continue;
                }
                if (i >= s.Length) throw Error(s, i, "an escape");
                char e = s[i++];
                switch (e)
                {
                    case '"': case '\\': case '/': b.Append(e); break;
                    case 'b': b.Append('\b'); break;
                    case 'f': b.Append('\f'); break;
                    case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break;
                    case 't': b.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(s, i, "four hex digits");
                        b.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Error(s, i - 1, "an escape");
                }
            }
        }

        static bool Word(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static void Space(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        static void Expect(string s, ref int i, char c)
        {
            if (i >= s.Length || s[i] != c) throw Error(s, i, "'" + c + "'");
            i++;
        }

        static FormatException Error(string s, int i, string expected)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++)
                if (s[k] == '\n') line++;
            return new FormatException("JSON line " + line + ": expected " + expected);
        }

        /// <summary>A JSON string literal of text.</summary>
        public static string Quote(string text)
        {
            var b = new StringBuilder("\"");
            foreach (char c in text ?? "")
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c < ' ') b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }
    }

    /// <summary>Reads typed members out of a parsed object, failing plainly on a missing or mistyped one.</summary>
    public static class JsonRead
    {
        public static Dictionary<string, object> Object(object o, string what) =>
            o as Dictionary<string, object> ?? throw new FormatException(what + " is not an object");

        public static object Member(Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out object v) ? v : throw new FormatException("missing '" + key + "'");

        public static string Text(Dictionary<string, object> o, string key) =>
            Member(o, key) as string ?? throw new FormatException("'" + key + "' is not a string");

        public static bool Bool(Dictionary<string, object> o, string key) =>
            Member(o, key) is bool b ? b : throw new FormatException("'" + key + "' is not true or false");

        public static int Int(Dictionary<string, object> o, string key)
        {
            if (Member(o, key) is double d && d == Math.Floor(d) && Math.Abs(d) < int.MaxValue) return (int)d;
            throw new FormatException("'" + key + "' is not an integer");
        }
    }
}
