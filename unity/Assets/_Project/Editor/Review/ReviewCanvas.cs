using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// A CPU pixel canvas (rows top-down) that composes the review sheet: read-back tiles, label bands, the overlay
    /// lines, the legend, and the PNG. Everything is drawn in pixels so batchmode needs no GUI or font assets.
    /// </summary>
    sealed class ReviewCanvas
    {
        public readonly int Width;
        public readonly int Height;
        readonly Color32[] px;

        public ReviewCanvas(int width, int height, Color32 fill)
        {
            Width = width;
            Height = height;
            px = new Color32[width * height];
            for (int i = 0; i < px.Length; i++)
                px[i] = fill;
        }

        /// <summary>A canvas holding a read-back texture (whose rows are bottom-up).</summary>
        public static ReviewCanvas FromTexture(Texture2D texture)
        {
            var canvas = new ReviewCanvas(texture.width, texture.height, new Color32(0, 0, 0, 255));
            Color32[] source = texture.GetPixels32();
            for (int y = 0; y < canvas.Height; y++)
                Array.Copy(source, (canvas.Height - 1 - y) * canvas.Width, canvas.px, y * canvas.Width, canvas.Width);
            return canvas;
        }

        public Color32 Get(int x, int y) => px[y * Width + x];

        public void Put(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
                px[y * Width + x] = c;
        }

        public void Blit(ReviewCanvas tile, int x0, int y0)
        {
            for (int y = 0; y < tile.Height; y++)
            {
                int ty = y0 + y;
                if (ty < 0 || ty >= Height)
                    continue;
                int from = Mathf.Max(0, -x0), to = Mathf.Min(tile.Width, Width - x0);
                if (to > from)
                    Array.Copy(tile.px, y * tile.Width + from, px, ty * Width + x0 + from, to - from);
            }
        }

        public void Fill(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Put(i, j, c);
        }

        /// <summary>Blends a rectangle towards black by <paramref name="alpha"/> / 255, for label bands.</summary>
        public void Darken(int x, int y, int w, int h, byte alpha)
        {
            float keep = 1f - alpha / 255f;
            for (int j = Mathf.Max(0, y); j < Mathf.Min(Height, y + h); j++)
                for (int i = Mathf.Max(0, x); i < Mathf.Min(Width, x + w); i++)
                {
                    Color32 c = px[j * Width + i];
                    px[j * Width + i] = new Color32((byte)(c.r * keep), (byte)(c.g * keep), (byte)(c.b * keep), 255);
                }
        }

        public void Outline(int x, int y, int w, int h, Color32 c, int thickness)
        {
            Fill(x, y, w, thickness, c);
            Fill(x, y + h - thickness, w, thickness, c);
            Fill(x, y, thickness, h, c);
            Fill(x + w - thickness, y, thickness, h, c);
        }

        /// <summary>A line of the given pixel thickness (a square brush), skipped when it leaves the canvas by far.</summary>
        public void Line(Vector2 a, Vector2 b, Color32 c, int thickness)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)));
            if (steps > 16384)
                return;
            steps = Mathf.Max(steps, 1);
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                int x = Mathf.RoundToInt(a.x + dx * t), y = Mathf.RoundToInt(a.y + dy * t);
                for (int j = 0; j < thickness; j++)
                    for (int i = 0; i < thickness; i++)
                        Put(x + i, y + j, c);
            }
        }

        /// <summary>Draws upper-case text in the 5 × 7 font at an integer scale; the advance is 6 × scale per glyph.</summary>
        public void Text(int x, int y, string text, int scale, Color32 c)
        {
            foreach (char ch in text)
            {
                byte[] rows = ReviewFont.Glyph(ch);
                for (int r = 0; r < ReviewFont.Rows; r++)
                    for (int k = 0; k < ReviewFont.Columns; k++)
                        if ((rows[r] >> (ReviewFont.Columns - 1 - k) & 1) != 0)
                            Fill(x + k * scale, y + r * scale, scale, scale, c);
                x += (ReviewFont.Columns + 1) * scale;
            }
        }

        public static int TextWidth(string text, int scale) => text.Length * (ReviewFont.Columns + 1) * scale;

        public static int LineHeight(int scale) => (ReviewFont.Rows + 2) * scale;

        /// <summary>The mean colour of a rectangle, for the log's render checks.</summary>
        public Color32 Mean(int x, int y, int w, int h)
        {
            long r = 0, g = 0, b = 0, n = 0;
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                {
                    Color32 c = Get(i, j);
                    r += c.r;
                    g += c.g;
                    b += c.b;
                    n++;
                }
            return n == 0 ? new Color32() : new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
        }

        public byte[] ToPng()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
            try
            {
                var flipped = new Color32[px.Length];
                for (int y = 0; y < Height; y++)
                    Array.Copy(px, y * Width, flipped, (Height - 1 - y) * Width, Width);
                texture.SetPixels32(flipped);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>A 5 × 7 bitmap font: upper-case letters, digits and the punctuation the sheet labels use.</summary>
    static class ReviewFont
    {
        public const int Columns = 5;
        public const int Rows = 7;

        static readonly Dictionary<char, byte[]> glyphs = Build();

        public static byte[] Glyph(char c)
        {
            c = char.ToUpperInvariant(c);
            return glyphs.TryGetValue(c, out byte[] rows) ? rows : glyphs['?'];
        }

        static Dictionary<char, byte[]> Build()
        {
            var table = new Dictionary<char, string>
            {
                ['A'] = "01110|10001|10001|11111|10001|10001|10001",
                ['B'] = "11110|10001|10001|11110|10001|10001|11110",
                ['C'] = "01110|10001|10000|10000|10000|10001|01110",
                ['D'] = "11100|10010|10001|10001|10001|10010|11100",
                ['E'] = "11111|10000|10000|11110|10000|10000|11111",
                ['F'] = "11111|10000|10000|11110|10000|10000|10000",
                ['G'] = "01110|10001|10000|10111|10001|10001|01111",
                ['H'] = "10001|10001|10001|11111|10001|10001|10001",
                ['I'] = "01110|00100|00100|00100|00100|00100|01110",
                ['J'] = "00111|00010|00010|00010|00010|10010|01100",
                ['K'] = "10001|10010|10100|11000|10100|10010|10001",
                ['L'] = "10000|10000|10000|10000|10000|10000|11111",
                ['M'] = "10001|11011|10101|10101|10001|10001|10001",
                ['N'] = "10001|10001|11001|10101|10011|10001|10001",
                ['O'] = "01110|10001|10001|10001|10001|10001|01110",
                ['P'] = "11110|10001|10001|11110|10000|10000|10000",
                ['Q'] = "01110|10001|10001|10001|10101|10010|01101",
                ['R'] = "11110|10001|10001|11110|10100|10010|10001",
                ['S'] = "01111|10000|10000|01110|00001|00001|11110",
                ['T'] = "11111|00100|00100|00100|00100|00100|00100",
                ['U'] = "10001|10001|10001|10001|10001|10001|01110",
                ['V'] = "10001|10001|10001|10001|10001|01010|00100",
                ['W'] = "10001|10001|10001|10101|10101|10101|01010",
                ['X'] = "10001|10001|01010|00100|01010|10001|10001",
                ['Y'] = "10001|10001|10001|01010|00100|00100|00100",
                ['Z'] = "11111|00001|00010|00100|01000|10000|11111",
                ['0'] = "01110|10001|10011|10101|11001|10001|01110",
                ['1'] = "00100|01100|00100|00100|00100|00100|01110",
                ['2'] = "01110|10001|00001|00010|00100|01000|11111",
                ['3'] = "11111|00010|00100|00010|00001|10001|01110",
                ['4'] = "00010|00110|01010|10010|11111|00010|00010",
                ['5'] = "11111|10000|11110|00001|00001|10001|01110",
                ['6'] = "00110|01000|10000|11110|10001|10001|01110",
                ['7'] = "11111|00001|00010|00100|01000|01000|01000",
                ['8'] = "01110|10001|10001|01110|10001|10001|01110",
                ['9'] = "01110|10001|10001|01111|00001|00010|01100",
                [' '] = "00000|00000|00000|00000|00000|00000|00000",
                ['.'] = "00000|00000|00000|00000|00000|01100|01100",
                [','] = "00000|00000|00000|00000|01100|00100|01000",
                [':'] = "00000|01100|01100|00000|01100|01100|00000",
                ['-'] = "00000|00000|00000|11111|00000|00000|00000",
                ['_'] = "00000|00000|00000|00000|00000|00000|11111",
                ['('] = "00010|00100|01000|01000|01000|00100|00010",
                [')'] = "01000|00100|00010|00010|00010|00100|01000",
                ['/'] = "00000|00001|00010|00100|01000|10000|00000",
                ['+'] = "00000|00100|00100|11111|00100|00100|00000",
                ['='] = "00000|00000|11111|00000|11111|00000|00000",
                ['%'] = "11001|11010|00010|00100|01000|01011|10011",
                ['>'] = "10000|01000|00100|00010|00100|01000|10000",
                ['@'] = "01110|10001|10111|10101|10110|10000|01111",
                ['?'] = "01110|10001|00001|00010|00100|00000|00100",
                ['°'] = "01100|10010|01100|00000|00000|00000|00000",
            };
            var result = new Dictionary<char, byte[]>();
            foreach (KeyValuePair<char, string> entry in table)
            {
                string[] rows = entry.Value.Split('|');
                var bits = new byte[Rows];
                for (int r = 0; r < Rows; r++)
                    bits[r] = Convert.ToByte(rows[r], 2);
                result[entry.Key] = bits;
            }
            return result;
        }
    }
}
