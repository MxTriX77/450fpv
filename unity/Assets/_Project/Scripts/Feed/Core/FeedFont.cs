using System.Collections.Generic;

namespace Fpv450.Feed
{
    /// <summary>
    /// The OSD's character ROM (p4-feed task 1.4, OSD-01, K§11.6), ported from the Godot feed's <c>FeedFont</c>
    /// (<c>docs/legacy/video-feed-godot/src/FeedFont.cs</c>): 5 × 8 glyphs in a 14 × 18 cell, capitals only, icons at
    /// their own size, a one-texel outline by dilation. The glyph rows, their order (a character's code is its index
    /// plus one) and the atlas are Godot's; only the engine plumbing differs: the atlas comes back as bytes, which the
    /// rig uploads (<c>FpvCamera</c>).
    /// </summary>
    /// <remarks>
    /// The geometry is the Godot ROM's, measured off the footage at 1920 × 1080 (its header holds the table): ink
    /// 5/14 × 8/18 of the 67.3 × 71.0 px cell = 24.0 × 31.6 px, strokes 4.8 and 3.9 px, a one-sample outline.
    /// Nothing is scaled on the way into the atlas: one authored sample is one texel, and the analog path softens it.
    /// </remarks>
    public static class FeedFont
    {
        /// The character cell, in atlas texels (FeedFont.cs:33).
        public const int CellW = 14, CellH = 18;

        /// One entry per character: the character, a space, then its rows, `#` ink and `.` blank, centred in the cell.
        /// Verbatim from FeedFont.cs:38-94, in its order; the volts mark is written as its escape, U+0432.
        static readonly string[] Glyphs =
        {
            "0 .###. #...# #...# #...# #...# #...# #...# .###.",
            "1 ..#.. .##.. ..#.. ..#.. ..#.. ..#.. ..#.. .###.",
            "2 .###. #...# ....# ...#. ..#.. .#... #.... #####",
            "3 ##### ...#. ..##. ....# ....# #...# #...# .###.",
            "4 ...#. ..##. .#.#. #..#. ##### ...#. ...#. ...#.",
            "5 ##### #.... #.... ####. ....# ....# #...# .###.",
            "6 ..##. .#... #.... ####. #...# #...# #...# .###.",
            "7 ##### ....# ...#. ...#. ..#.. ..#.. .#... .#...",
            "8 .###. #...# #...# .###. #...# #...# #...# .###.",
            "9 .###. #...# #...# #...# .#### ....# ...#. .##..",
            "A .###. #...# #...# #...# ##### #...# #...# #...#",
            "B ####. #...# #...# ####. #...# #...# #...# ####.",
            "C .###. #...# #.... #.... #.... #.... #...# .###.",
            "D ####. #...# #...# #...# #...# #...# #...# ####.",
            "E ##### #.... #.... ####. #.... #.... #.... #####",
            "F ##### #.... #.... ####. #.... #.... #.... #....",
            "G .###. #...# #.... #.... #.### #...# #...# .####",
            "H #...# #...# #...# ##### #...# #...# #...# #...#",
            // The footage draws I as a plain bar with no serifs: one sample wide over the full height.
            "I ..#.. ..#.. ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
            "J ..### ...#. ...#. ...#. ...#. ...#. #..#. .##..",
            "K #...# #..#. #.#.. ##... ##... #.#.. #..#. #...#",
            "L #.... #.... #.... #.... #.... #.... #.... #####",
            "M #...# ##.## #.#.# #.#.# #...# #...# #...# #...#",
            "N #...# ##..# ##..# #.#.# #.#.# #..## #..## #...#",
            "O .###. #...# #...# #...# #...# #...# #...# .###.",
            "P ####. #...# #...# #...# ####. #.... #.... #....",
            "Q .###. #...# #...# #...# #...# #.#.# #..#. .##.#",
            "R ####. #...# #...# #...# ####. #.#.. #..#. #...#",
            "S .###. #...# #.... .###. ....# ....# #...# .###.",
            "T ##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
            "U #...# #...# #...# #...# #...# #...# #...# .###.",
            "V #...# #...# #...# #...# #...# #...# .#.#. ..#..",
            "W #...# #...# #...# #...# #.#.# #.#.# ##.## #...#",
            "X #...# #...# .#.#. ..#.. ..#.. .#.#. #...# #...#",
            "Y #...# #...# .#.#. ..#.. ..#.. ..#.. ..#.. ..#..",
            "Z ##### ....# ...#. ..#.. ..#.. .#... #.... #####",
            // The unit marks are the only small characters: they sit low in the cell, as in the footage.
            "m ..... ..... ..... ##.#. #.#.# #.#.# #.#.# #.#.#",
            "a ..... ..... ..... .###. ....# .#### #...# .####",
            "в ..... ..... ..... ####. #...# ####. #...# ####.",
            "! ..#.. ..#.. ..#.. ..#.. ..#.. ..#.. ..... ..#..",
            ". ..... ..... ..... ..... ..... ..... ..##. ..##.",
            ": ..... ..##. ..##. ..... ..... ..##. ..##. .....",
            "- ..... ..... ..... ..... ##### ..... ..... .....",
            // The battery mark that leads each voltage readout: 10 x 14 in the cell, measured 45 x 62 px on the footage.
            // A cap, a rim, a hollow upper part and a solid lower one.
            "$ ...####... ########## #........# #........# #........# #........# #........# #........# ########## ########## ########## ########## ########## ########## ########## ##########",
            // The altitude label: three small letters inside one cell, 11 x 5, measured 49 x 18 px.
            "> .#..#...### #.#.#....#. ###.#....#. #.#.#....#. #.#.###..#.",
            // The two-line unit label beside the flight timer, 7 x 9, measured 33 x 37 px; its content is invented.
            "[ ###.#.. #...#.. ##..#.. #...### ....... ###.#.# .#..### .#..#.# .#..#.#",
            // The centre reticle, three cells wide: a stub, the bar with its lozenge, and a stub.
            "{ .............. .............. ...........### ..............",
            "| .............. ....######.... #####.....#### ....######....",
            "} .............. .............. ####.......... ..............",
        };

        static readonly Dictionary<char, float> Codes = Build();

        /// The number of cells in the atlas, blank included (FeedFont.cs:99). The shader needs it to index a cell.
        public static int CellCount => Glyphs.Length + 1;

        /// The atlas's width in texels: one row of <see cref="CellCount"/> cells.
        public static int AtlasWidth => CellCount * CellW;

        /// The atlas cell of a character, 0 (blank) for a space or anything the ROM does not have (FeedFont.cs:102).
        public static float Code(char c) => Codes.TryGetValue(c, out float code) ? code : 0f;

        /// Whether the ROM draws `c`.
        public static bool Has(char c) => Codes.ContainsKey(c);

        static Dictionary<char, float> Build()
        {
            var codes = new Dictionary<char, float>(Glyphs.Length);
            for (int i = 0; i < Glyphs.Length; i++)
                codes[Glyphs[i][0]] = i + 1;
            return codes;
        }

        /// Bakes the atlas (FeedFont.cs:114-152): <see cref="AtlasWidth"/> × <see cref="CellH"/> texels, two bytes
        /// each, R the glyph and G its one-texel outline (0 or 255), row 0 at the top as Godot's <c>Image</c> holds
        /// it. The shader reads it without a flip (Feed.shader's <c>glyph_ink</c>).
        public static byte[] BuildAtlas()
        {
            int w = AtlasWidth;
            var ink = new bool[w, CellH];
            for (int g = 0; g < Glyphs.Length; g++)
            {
                string[] rows = Glyphs[g].Substring(2).Split(' ');
                int gw = rows[0].Length, gh = rows.Length;
                int x0 = (g + 1) * CellW + (CellW - gw) / 2, y0 = (CellH - gh) / 2;
                for (int y = 0; y < gh; y++)
                    for (int x = 0; x < gw; x++)
                        if (rows[y][x] == '#')
                            ink[x0 + x, y0 + y] = true;
            }

            var rg = new byte[w * CellH * 2];
            for (int y = 0; y < CellH; y++)
                for (int x = 0; x < w; x++)
                {
                    bool on = ink[x, y];
                    bool outline = false;
                    if (!on)
                    {
                        // Dilate by one texel, but never across a cell boundary, or a glyph would outline its neighbour.
                        int cellStart = x / CellW * CellW;
                        for (int dy = -1; dy <= 1 && !outline; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (ny < 0 || ny >= CellH || nx < cellStart || nx >= cellStart + CellW || !ink[nx, ny])
                                    continue;
                                outline = true;
                                break;
                            }
                    }
                    int i = (y * w + x) * 2;
                    rg[i] = on ? (byte)255 : (byte)0;
                    rg[i + 1] = outline ? (byte)255 : (byte)0;
                }
            return rg;
        }
    }
}
