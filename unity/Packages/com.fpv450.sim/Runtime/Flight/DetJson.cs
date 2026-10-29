using System;
using System.Collections.Generic;
using System.Text;

namespace Fpv450.Sim.Flight
{
    internal enum JsonKind { Null, Bool, Number, String, Array, Object }

    /// <summary>
    /// One JSON value. An object keeps its members in file order (Keys[i] names Items[i]), so nothing depends on a
    /// hash order; lookups are linear, which suits the small setup files the core reads at load time.
    /// </summary>
    internal sealed class JsonNode
    {
        public JsonKind Kind;
        public bool Bool;
        public double Number;
        public string Text;
        public int Line;
        public List<JsonNode> Items;
        public List<string> Keys;

        /// <summary>The member named key, or null.</summary>
        public JsonNode Member(string key)
        {
            if (Kind != JsonKind.Object) return null;
            for (int i = 0; i < Keys.Count; i++)
                if (string.Equals(Keys[i], key, StringComparison.Ordinal))
                    return Items[i];
            return null;
        }
    }

    /// <summary>
    /// DetJson: a strict JSON reader (RFC 8259) for the core's setup files, such as drone.json. Load time only: it
    /// allocates.
    ///
    /// Numbers are read deterministically, without double.Parse: the decimal digits become an exact integer m ≤ 2^53
    /// and a power of ten 10^k with |k| ≤ 22, both exact doubles, and the value is the one correctly rounded IEEE
    /// product or quotient m·10^k (Clinger's fast path). That is the correctly rounded value of the decimal, the same
    /// bits on Mono, IL2CPP and CoreCLR. A number outside that range (more than 15 significant digits, or an exponent
    /// past ±22 after the digits are placed) is refused rather than read approximately.
    /// </summary>
    public sealed class DetJson
    {
        const ulong MaxExactInteger = 1UL << 53;

        // 10^0 … 10^22, each exactly representable in a double.
        static readonly double[] Pow10 =
        {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
            1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
        };

        readonly string _s;
        int _i;
        int _line = 1;

        DetJson(string text) => _s = text;

        /// <summary>The value the whole text holds. Throws FormatException, with the line, on anything but JSON.</summary>
        internal static JsonNode Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var r = new DetJson(text);
            JsonNode root = r.Value();
            r.SkipSpace();
            if (r._i < r._s.Length) throw r.Error("text after the end of the value");
            return root;
        }

        /// <summary>A number in JSON's grammar, read as Parse reads it (for tests and tools).</summary>
        public static double ParseNumber(string text)
        {
            JsonNode n = Parse(text);
            if (n.Kind != JsonKind.Number) throw new FormatException("not a number: " + text);
            return n.Number;
        }

        FormatException Error(string what) => new FormatException("line " + _line + ": " + what);

        void SkipSpace()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (c == '\n') _line++;
                else if (c != ' ' && c != '\t' && c != '\r') return;
                _i++;
            }
        }

        JsonNode Value()
        {
            SkipSpace();
            if (_i >= _s.Length) throw Error("a value was expected");
            char c = _s[_i];
            var node = new JsonNode { Line = _line };
            switch (c)
            {
                case '{': Object(node); break;
                case '[': Array(node); break;
                case '"': node.Kind = JsonKind.String; node.Text = String(); break;
                case 't': Literal("true"); node.Kind = JsonKind.Bool; node.Bool = true; break;
                case 'f': Literal("false"); node.Kind = JsonKind.Bool; break;
                case 'n': Literal("null"); node.Kind = JsonKind.Null; break;
                default:
                    if (c == '-' || (c >= '0' && c <= '9')) { node.Kind = JsonKind.Number; node.Number = Number(); }
                    else throw Error("unexpected '" + c + "'");
                    break;
            }
            return node;
        }

        void Object(JsonNode node)
        {
            node.Kind = JsonKind.Object;
            node.Keys = new List<string>();
            node.Items = new List<JsonNode>();
            _i++;
            SkipSpace();
            if (_i < _s.Length && _s[_i] == '}') { _i++; return; }
            while (true)
            {
                SkipSpace();
                if (_i >= _s.Length || _s[_i] != '"') throw Error("a member name was expected");
                string key = String();
                if (node.Keys.Contains(key)) throw Error("the member '" + key + "' appears twice");
                SkipSpace();
                if (_i >= _s.Length || _s[_i] != ':') throw Error("':' was expected after '" + key + "'");
                _i++;
                node.Keys.Add(key);
                node.Items.Add(Value());
                SkipSpace();
                if (_i >= _s.Length) throw Error("the object is not closed");
                if (_s[_i] == ',') { _i++; continue; }
                if (_s[_i] == '}') { _i++; return; }
                throw Error("',' or '}' was expected");
            }
        }

        void Array(JsonNode node)
        {
            node.Kind = JsonKind.Array;
            node.Items = new List<JsonNode>();
            _i++;
            SkipSpace();
            if (_i < _s.Length && _s[_i] == ']') { _i++; return; }
            while (true)
            {
                node.Items.Add(Value());
                SkipSpace();
                if (_i >= _s.Length) throw Error("the array is not closed");
                if (_s[_i] == ',') { _i++; continue; }
                if (_s[_i] == ']') { _i++; return; }
                throw Error("',' or ']' was expected");
            }
        }

        void Literal(string word)
        {
            if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("'" + word + "' was expected");
            _i += word.Length;
        }

        string String()
        {
            _i++; // the opening quote
            var b = new StringBuilder();
            while (true)
            {
                if (_i >= _s.Length) throw Error("the string is not closed");
                char c = _s[_i++];
                if (c == '"') return b.ToString();
                if (c < ' ') throw Error("a control character inside a string");
                if (c != '\\') { b.Append(c); continue; }
                if (_i >= _s.Length) throw Error("the string is not closed");
                char e = _s[_i++];
                switch (e)
                {
                    case '"': b.Append('"'); break;
                    case '\\': b.Append('\\'); break;
                    case '/': b.Append('/'); break;
                    case 'b': b.Append('\b'); break;
                    case 'f': b.Append('\f'); break;
                    case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break;
                    case 't': b.Append('\t'); break;
                    case 'u':
                        if (_i + 4 > _s.Length) throw Error("a short \\u escape");
                        int code = 0;
                        for (int k = 0; k < 4; k++)
                        {
                            char h = _s[_i++];
                            int v = h >= '0' && h <= '9' ? h - '0' : h >= 'a' && h <= 'f' ? h - 'a' + 10 : h >= 'A' && h <= 'F' ? h - 'A' + 10 : -1;
                            if (v < 0) throw Error("a bad \\u escape");
                            code = code * 16 + v;
                        }
                        b.Append((char)code);
                        break;
                    default: throw Error("a bad escape '\\" + e + "'");
                }
            }
        }

        // -?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?, read exactly (see the class summary).
        double Number()
        {
            bool negative = false;
            if (_s[_i] == '-') { negative = true; _i++; }

            ulong m = 0;
            int digits = 0;   // significant digits held in m
            long exp10 = 0;   // the value is m·10^exp10
            bool lost = false; // a nonzero digit past what m holds

            if (_i >= _s.Length || !IsDigit(_s[_i])) throw Error("a digit was expected");
            if (_s[_i] == '0')
            {
                _i++;
                if (_i < _s.Length && IsDigit(_s[_i])) throw Error("a number with a leading zero");
            }
            else
            {
                while (_i < _s.Length && IsDigit(_s[_i])) Digit(_s[_i++] - '0', false, ref m, ref digits, ref exp10, ref lost);
            }

            if (_i < _s.Length && _s[_i] == '.')
            {
                _i++;
                if (_i >= _s.Length || !IsDigit(_s[_i])) throw Error("a digit was expected after '.'");
                while (_i < _s.Length && IsDigit(_s[_i])) Digit(_s[_i++] - '0', true, ref m, ref digits, ref exp10, ref lost);
            }

            if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
            {
                _i++;
                bool expNegative = false;
                if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) { expNegative = _s[_i] == '-'; _i++; }
                if (_i >= _s.Length || !IsDigit(_s[_i])) throw Error("a digit was expected in the exponent");
                long e = 0;
                while (_i < _s.Length && IsDigit(_s[_i]))
                {
                    e = e * 10 + (_s[_i++] - '0');
                    if (e > 100000) throw Error("an exponent out of range");
                }
                exp10 += expNegative ? -e : e;
            }

            if (m == 0) return negative ? -0.0 : 0.0;
            while (m % 10 == 0) { m /= 10; exp10++; }
            if (lost || m > MaxExactInteger)
                throw Error("a number with more significant digits than an exact read allows (write at most 15)");

            double x = m;
            if (exp10 > 0)
            {
                if (exp10 > 22) throw Error("a number too large for an exact read");
                x *= Pow10[exp10];
            }
            else if (exp10 < 0)
            {
                if (exp10 < -22) throw Error("a number too small for an exact read (write it with fewer leading zeros)");
                x /= Pow10[-exp10];
            }
            return negative ? -x : x;
        }

        static bool IsDigit(char c) => c >= '0' && c <= '9';

        // One decimal digit into m·10^exp10. Leading zeros are skipped; past 19 digits a digit only moves the exponent,
        // and a nonzero one is remembered as lost.
        static void Digit(int d, bool fraction, ref ulong m, ref int digits, ref long exp10, ref bool lost)
        {
            if (m == 0 && d == 0)
            {
                if (fraction) exp10--;
                return;
            }
            if (digits < 19)
            {
                m = m * 10 + (ulong)d;
                digits++;
                if (fraction) exp10--;
            }
            else
            {
                if (d != 0) lost = true;
                if (!fraction) exp10++;
            }
        }
    }
}
