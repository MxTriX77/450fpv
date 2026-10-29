using System.Text;

namespace Fpv450.Feed
{
    /// <summary>
    /// The OSD's character grid as the analog path reads it (p4-feed task 1.4, OSD-01, K§11.6), ported from the Godot
    /// feed's <c>FeedOsd</c> (<c>docs/legacy/video-feed-godot/src/FeedOsd.cs</c>): 30 × 16 cells of 67.3 × 71.0 px at
    /// 1920 × 1080, inset half a cell, plus the receiver's own two text lines, as one codes texture of 30 × 18 texels.
    /// </summary>
    /// <remarks>
    /// <para><b>The grid is not the picture</b> (FeedOsd.cs:15-19): 30 × 16 cells cover 2019 × 1136 px, wider and taller
    /// than the frame, so the last two columns and rows fall outside it, as on the aircraft. Do not "fix" it to
    /// 1/30 × 1/16 of the screen.</para>
    /// <para>The content is not this class's: the OSD's characters come from <see cref="IFeedSignals.OsdCell"/>, which
    /// the OSD content model fills (design 5, task 1.5) and, until then, the stub's Godot layout
    /// (<see cref="StubOsd"/>). This class only puts them on the codes texture with the receiver's text.</para>
    /// </remarks>
    public static class FeedOsd
    {
        public const int Cols = 30, Rows = 16;
        /// The receiver's own two lines of text live in the rows after the OSD, so one texture carries both.
        public const int RxRow = Rows, TotalRows = Rows + 2;

        /// The character cell and the grid origin, as fractions of the picture (FeedOsd.cs:51-52): measured 67.3 × 71.0
        /// px at 1920 × 1080, inset half a cell.
        public const float CellW = 67.3f / 1920f, CellH = 71.0f / 1080f;
        public const float OriginX = 33.7f / 1920f, OriginY = 33.5f / 1080f;

        /// The receiver's two short lines (FeedOsd.cs:69), green on the no-signal screen and the receiver's snow only,
        /// never over the picture. Invented and deliberately generic: the footage's text is not recorded (OPSEC).
        static readonly string[] RxText = { "AV IN 1", "PAL 50" };

        /// Fills `codes` (<see cref="Cols"/> × <see cref="TotalRows"/>, row 0 at the top) with this field's OSD from
        /// `signals` in rows 0–15 and the receiver's text in rows 16–17: one <see cref="FeedFont.Code"/> per cell, as
        /// the Godot <c>Upload</c> wrote its <c>Rf</c> image (FeedOsd.cs:142-148).
        public static void WriteCodes(IFeedSignals signals, float[] codes)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    codes[r * Cols + c] = FeedFont.Code(signals.OsdCell(r, c));
            for (int r = Rows; r < TotalRows; r++)
            {
                string text = RxText[r - RxRow];
                for (int c = 0; c < Cols; c++)
                    codes[r * Cols + c] = FeedFont.Code(c < text.Length ? text[c] : ' ');
            }
        }

        /// The OSD's lit rows for the log, `row@first column "text"`, blanks inside a row kept as spaces.
        public static string Describe(IFeedSignals signals)
        {
            var text = new StringBuilder();
            var row = new char[Cols];
            for (int r = 0; r < Rows; r++)
            {
                int first = -1, last = -1;
                for (int c = 0; c < Cols; c++)
                {
                    row[c] = signals.OsdCell(r, c);
                    if (row[c] != ' ')
                    {
                        if (first < 0)
                            first = c;
                        last = c;
                    }
                }
                if (first >= 0)
                    text.Append(text.Length > 0 ? "; " : "").Append(r).Append('@').Append(first)
                        .Append(" \"").Append(row, first, last - first + 1).Append('"');
            }
            return text.Length > 0 ? text.ToString() : "blank";
        }
    }
}
