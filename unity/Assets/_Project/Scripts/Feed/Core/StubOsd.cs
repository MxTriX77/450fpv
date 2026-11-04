using System;
using System.Globalization;

namespace Fpv450.Feed
{
    /// <summary>
    /// A signals source whose OSD is laid out once per video field (blinking needs the field): the rig calls
    /// <see cref="BuildOsd"/> on each new field before it reads <see cref="IFeedSignals.OsdCell"/>, as the Godot feed
    /// built its OSD once per field, never per rendered frame (VideoFeed.cs:128-133).
    /// </summary>
    public interface IFieldOsd
    {
        void BuildOsd(int field);
    }

    /// <summary>
    /// The OSD's labelled stand-in content (FEED-03, D-008) until the OSD content model lands (p4-feed design 5, task
    /// 1.5): the Godot OSD's own layout, <c>FeedOsd.Build</c> (<c>docs/legacy/video-feed-godot/src/FeedOsd.cs:82-125</c>),
    /// ported row for row, with the two strings of SEC-05 and every value invented.
    /// </summary>
    /// <remarks>
    /// <para>Row and column assignment, as the Godot layout measured it: row 0 the mode word at columns 0–2 and the
    /// altitude ending at column 26; row 7 the reticle at columns 13–15; row 10 the arming indicator at 0–7; row 11
    /// the unit label and the MM:SS timer; row 12 a battery mark, the per-cell voltage, the airframe name at column 7
    /// and a number ending at column 27; row 13 a battery mark, the pack voltage, a number ending at column 26 and its
    /// amps unit at 27.</para>
    /// <para>The name: the Godot code wrote <c>Svinorez 10 Opto</c>; the port writes <b>"Svinoriz 10 Opto"</b>
    /// (D-011, SEC-05). Both are 16 characters, so the number to its right keeps its column and a clear cell either
    /// side. The ROM's face is all capitals, so the name reaches the screen upper-cased: that is the font, not an edit
    /// to the name. "! SAFE !" fills the eight cells of the arming indicator. Never revert either.</para>
    /// <para>The values: the pack voltage and the current are the signals' (labelled stubs on
    /// <see cref="StubFeedSignals"/>: 25.2 V and 0 A); the altitude, the distance and the flight timer are this
    /// class's, 0 unless the noclip view sets them the way the Godot stand-in did (<c>NoclipFeed</c>).
    /// Nothing is read off the footage.</para>
    /// </remarks>
    public sealed class StubOsd
    {
        /// Stub: the altitude readout, m. 0 is the Godot feed's own value with its signals pinned.
        public float AltitudeM;
        /// Stub: the distance readout, m (shown in km).
        public float DistanceM;
        /// Stub: the flight timer, s.
        public double ElapsedSeconds;

        /// The grid this layout fills, [row, column]; blank until the first <see cref="Build"/>.
        public readonly char[,] Grid = new char[FeedOsd.Rows, FeedOsd.Cols];

        /// The airframe name, D-011, kept as the decision writes it; upper-cased for the ROM.
        public const string Name = "Svinoriz 10 Opto";
        /// The armament indicator, SEC-05: eight cells, what the footage's own indicator occupies.
        public const string Armed = "! SAFE !";
        /// The three-character flight-mode word (FeedOsd.cs:64).
        const string Mode = "AIR";
        static readonly string Airframe = Name.ToUpperInvariant();

        /// Warning elements blink 12 fields on and 9 off (FeedOsd.cs:55).
        const int BlinkOnFields = 12, BlinkOffFields = 9;
        const int RowTop = 0, RowReticle = 7, RowArmed = 10, RowTimer = 11, RowCell = 12, RowPack = 13;
        const int ColLeft = 0, ColStack = 1, ColReticle = 13;
        const int ColName = 7, ColAltEnd = 26, ColUpperEnd = 27, ColLowerEnd = 26, ColAmps = 27;
        /// Warning thresholds for the stand-in readouts (FeedOsd.cs:76): a cell's volts and the link's 0–1 margin.
        const float LowCellVolts = 3.50f, LowLinkFraction = 0.35f;
        /// The Godot stand-in's pack is 6S (FeedSignals.cs:36); phase 5 picks the real battery.
        const float Cells = 6f;

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public StubOsd() => Clear();

        /// Lays the grid out for one video field (FeedOsd.cs:82-125). Float math is cast to float, as the rest of
        /// the feed core is, so Unity's Mono rounds as CoreCLR does.
        public void Build(IFeedSignals s, int field)
        {
            Clear();

            // Top row: the mode word at the left, the altitude label, value and unit at the right.
            Put(RowTop, ColLeft, Mode);
            PutRight(RowTop, ColAltEnd, ">" + Math.Max(0, (int)MathF.Round(AltitudeM)).ToString(Invariant) + "m");

            // Centre: the reticle, a bar with a lozenge at the middle, drawn across three cells.
            Put(RowReticle, ColReticle, "{|}");

            // The bottom-left stack.
            float packVolts = s.PackVolts;
            float cell = (float)(packVolts / Cells);
            int seconds = (int)ElapsedSeconds;
            Put(RowArmed, ColLeft, Armed);
            Put(RowTimer, ColLeft, "[");
            Put(RowTimer, ColStack, (seconds / 60 % 100).ToString("00", Invariant) + ":" + (seconds % 60).ToString("00", Invariant));
            Put(RowCell, ColLeft, "$");
            Put(RowCell, ColStack, cell.ToString("0.00", Invariant) + "в");
            Put(RowPack, ColLeft, "$");
            Put(RowPack, ColStack, packVolts.ToString("00.0", Invariant) + "в");

            // The airframe name shares the per-cell voltage's row, and the two right-aligned numbers sit one per row,
            // the lower one carrying the amps unit.
            Put(RowCell, ColName, Airframe);
            PutRight(RowCell, ColUpperEnd, ((float)(DistanceM / 1000f)).ToString("0.00", Invariant));
            PutRight(RowPack, ColLowerEnd, s.MotorCurrentTotalA.ToString("00.00", Invariant));
            Put(RowPack, ColAmps, "a");

            // Warning text, blinking, over the airframe name: the message field of that row.
            string warning = cell < LowCellVolts ? "BATT LOW"
                : FeedSignalScale.LinkMargin(s.LinkMarginDb) < LowLinkFraction ? "LOW LINK" : null;
            if (warning != null && Blink(field))
            {
                for (int c = 0; c < Airframe.Length; c++)
                    Grid[RowCell, ColName + c] = ' ';
                Put(RowCell, ColName + (Airframe.Length - warning.Length) / 2, warning);
            }
        }

        /// True while a blinking element is on.
        static bool Blink(int field) => field % (BlinkOnFields + BlinkOffFields) < BlinkOnFields;

        void Clear()
        {
            for (int r = 0; r < FeedOsd.Rows; r++)
                for (int c = 0; c < FeedOsd.Cols; c++)
                    Grid[r, c] = ' ';
        }

        void Put(int row, int col, string text)
        {
            for (int i = 0; i < text.Length && col + i < FeedOsd.Cols; i++)
                if (col + i >= 0)
                    Grid[row, col + i] = text[i];
        }

        void PutRight(int row, int lastCol, string text) => Put(row, lastCol - text.Length + 1, text);
    }
}
