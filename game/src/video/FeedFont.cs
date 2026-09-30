using System.Collections.Generic;
using Godot;

/// The character ROM the OSD and the receiver's own text are drawn from.
///
/// `docs/reference-notes/video-feed.md` §5 measures the OSD as a 30 × 16 character grid of white fixed-width glyphs
/// with a black outline. On the recording one cell is ≈64 × 67.5 px, but the feed only carries ≈450 luma samples per
/// line and ≈286 lines per field, so a cell is really about 15 × 18 samples: a 12 × 18 character cell is the right
/// size, and it is what the analog character generators of this class of flight controller use.
///
/// The glyphs are authored at 5 × 7 and doubled into the cell, which gives 2-sample-wide strokes — the weight the
/// footage shows. The outline is a one-sample dilation, generated rather than authored. Nothing here is softened: the
/// measured 3–4 px edge rise is the transmission path (P2), which the feed applies afterwards, so the ROM is hard.
///
/// The atlas is one row of 12 × 18 cells, RG8: R is the glyph, G is its outline. Cell 0 is blank.
public static class FeedFont
{
    public const int CellW = 12, CellH = 18;
    const int SrcW = 5, SrcH = 7;
    /// Where the doubled 10 × 14 glyph sits in the cell, leaving room for the outline on every side.
    const int InsetX = 1, InsetY = 2;

    /// One entry per character: the character, then its 7 rows of 5, `#` ink and `.` blank. The order is the atlas
    /// order, and the code of a character is its index here plus one.
    static readonly string[] Glyphs =
    {
        "0 .###. #...# #..## #.#.# ##..# #...# .###.",
        "1 ..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###.",
        "2 .###. #...# ....# ...#. ..#.. .#... #####",
        "3 ##### ...#. ..##. ....# ....# #...# .###.",
        "4 ...#. ..##. .#.#. #..#. ##### ...#. ...#.",
        "5 ##### #.... ####. ....# ....# #...# .###.",
        "6 ..##. .#... #.... ####. #...# #...# .###.",
        "7 ##### ....# ...#. ..#.. .#... .#... .#...",
        "8 .###. #...# #...# .###. #...# #...# .###.",
        "9 .###. #...# #...# .#### ....# ...#. .##..",
        "A .###. #...# #...# ##### #...# #...# #...#",
        "B ####. #...# #...# ####. #...# #...# ####.",
        "C .###. #...# #.... #.... #.... #...# .###.",
        "D ####. #...# #...# #...# #...# #...# ####.",
        "E ##### #.... #.... ####. #.... #.... #####",
        "F ##### #.... #.... ####. #.... #.... #....",
        "G .###. #...# #.... #.### #...# #...# .####",
        "H #...# #...# #...# ##### #...# #...# #...#",
        "I .###. ..#.. ..#.. ..#.. ..#.. ..#.. .###.",
        "J ..### ...#. ...#. ...#. ...#. #..#. .##..",
        "K #...# #..#. #.#.. ##... #.#.. #..#. #...#",
        "L #.... #.... #.... #.... #.... #.... #####",
        "M #...# ##.## #.#.# #.#.# #...# #...# #...#",
        "N #...# ##..# #.#.# #..## #...# #...# #...#",
        "O .###. #...# #...# #...# #...# #...# .###.",
        "P ####. #...# #...# ####. #.... #.... #....",
        "Q .###. #...# #...# #...# #.#.# #..#. .##.#",
        "R ####. #...# #...# ####. #.#.. #..#. #...#",
        "S .#### #.... #.... .###. ....# ....# ####.",
        "T ##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
        "U #...# #...# #...# #...# #...# #...# .###.",
        "V #...# #...# #...# #...# #...# .#.#. ..#..",
        "W #...# #...# #...# #.#.# #.#.# ##.## #...#",
        "X #...# #...# .#.#. ..#.. .#.#. #...# #...#",
        "Y #...# #...# .#.#. ..#.. ..#.. ..#.. ..#..",
        "Z ##### ....# ...#. ..#.. .#... #.... #####",
        "e ..... ..... .###. #...# ##### #.... .###.",
        "i ..#.. ..... ..#.. ..#.. ..#.. ..#.. ..#..",
        "m ..... ..... ##.#. #.#.# #.#.# #.#.# #.#.#",
        "n ..... ..... ####. #...# #...# #...# #...#",
        "o ..... ..... .###. #...# #...# #...# .###.",
        "p ..... ..... ####. #...# #...# ####. #....",
        "r ..... ..... #.##. ##..# #.... #.... #....",
        "t .#... .#... ###.. .#... .#... .#... ..##.",
        "v ..... ..... #...# #...# #...# .#.#. ..#..",
        "z ..... ..... ##### ...#. ..#.. .#... #####",
        "! ..#.. ..#.. ..#.. ..#.. ..#.. ..... ..#..",
        "% ##..# ##.#. ...#. ..#.. .#... .#.## #..##",
        ". ..... ..... ..... ..... ..... .##.. .##..",
        ": ..... .##.. .##.. ..... .##.. .##.. .....",
        "+ ..... ..#.. ..#.. ##### ..#.. ..#.. .....",
        "- ..... ..... ..... ##### ..... ..... .....",
        "° .##.. #..#. #..#. .##.. ..... ..... .....",
        // The dot of the OSD's dotted artificial horizon (the row of bright dots in L).
        "~ ..... ..... ..#.. .###. ..#.. ..... .....",
    };

    static readonly Dictionary<char, float> Codes = Build();

    /// The number of cells in the atlas, blank included. The shader needs it to index a cell.
    public static int CellCount => Glyphs.Length + 1;

    /// The atlas cell of a character, 0 (blank) for a space or anything the ROM does not have.
    public static float Code(char c) => Codes.TryGetValue(c, out float code) ? code : 0f;

    static Dictionary<char, float> Build()
    {
        var codes = new Dictionary<char, float>(Glyphs.Length);
        for (int i = 0; i < Glyphs.Length; i++)
            codes[Glyphs[i][0]] = i + 1;
        return codes;
    }

    /// Bakes the atlas: one row of CellCount cells, R the glyph and G its one-sample outline.
    public static ImageTexture BuildAtlas()
    {
        int w = CellCount * CellW;
        var ink = new bool[w, CellH];
        for (int g = 0; g < Glyphs.Length; g++)
        {
            string bits = Glyphs[g].Substring(2).Replace(" ", "");
            int x0 = (g + 1) * CellW + InsetX;
            for (int sy = 0; sy < SrcH; sy++)
                for (int sx = 0; sx < SrcW; sx++)
                {
                    if (bits[sy * SrcW + sx] != '#')
                        continue;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                            ink[x0 + 2 * sx + dx, InsetY + 2 * sy + dy] = true;
                }
        }

        var img = Image.CreateEmpty(w, CellH, false, Image.Format.Rg8);
        for (int y = 0; y < CellH; y++)
            for (int x = 0; x < w; x++)
            {
                bool on = ink[x, y];
                bool outline = false;
                if (!on)
                {
                    // Dilate by one sample, but never across a cell boundary, or a glyph would outline its neighbour.
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
                img.SetPixel(x, y, new Color(on ? 1f : 0f, outline ? 1f : 0f, 0f));
            }
        return ImageTexture.CreateFromImage(img);
    }
}
