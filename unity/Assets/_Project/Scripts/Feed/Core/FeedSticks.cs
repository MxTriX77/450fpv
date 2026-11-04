using System;

namespace Fpv450.Feed
{
    /// <summary>
    /// The OSD's stick overlay (D-084, the pilot's X, D-078): two small boxes, the left stick's and the right's, each with
    /// a cursor dot at its stick. The field pass draws them in the OSD's own pixels, the ROM's white ink with its one-texel
    /// dark outline, and inserts them with the characters before the analog stages, so they soften, fringe and tear with
    /// the text (Feed.shader's <c>stick_ink</c>). They are not character cells: a cursor sits at its stick's own position.
    /// </summary>
    /// <remarks>
    /// <para>The sizes are the ROM's texels (<see cref="FeedFont"/>: 14 × 18 to a 67.3 × 71.0 px cell, 4.81 × 3.94 px at
    /// 1920 × 1080), so a box's one-texel frame is a glyph's stroke. Each box is 15 × 18 texels, 72.1 × 71.0 px: one text
    /// row tall and square within 2 %. The two are 9 texels (43.3 px) apart, and a cursor is 2 × 2 texels (9.6 × 7.9 px).
    /// The boxes sit on the ROM's texel grid; a cursor moves continuously, 21.6 px either way across and down (4.5 and 5.48
    /// texels), square in the 16:9 picture, and at full travel a dark texel still parts its dot from the frame.</para>
    /// <para>The rig takes the overlay once per field (<see cref="IOsdSticks"/>, FpvCamera.Step), from the latest sample
    /// its source holds: the cursors move at 50 fields/s, never at the OSD text's 12 Hz refresh.</para>
    /// </remarks>
    public struct FeedSticks
    {
        /// A box's width and height, the gap between the two boxes and the cursor's side, in the ROM's texels.
        public const int BoxW = 15, BoxH = 18, Gap = 9, Dot = 2;
        /// One ROM texel, as a fraction of the picture's width and height.
        public const float TexelW = FeedOsd.CellW / FeedFont.CellW, TexelH = FeedOsd.CellH / FeedFont.CellH;
        /// A cursor's travel either side of its box's middle, in texels: across, to one texel inside the frame (the frame,
        /// the gap, half the dot); down, as far in pixels, the picture being 16:9.
        public const float TravelX = BoxW / 2f - 2 - Dot / 2f, TravelY = TravelX * TexelW * 16f / (TexelH * 9f);

        /// Drawn (X). Off, the pass draws what it drew without the overlay.
        public bool On;
        /// The left box's and the right box's left edges, and their top, as fractions of the picture (y down).
        public float LeftBox, RightBox, Top;
        /// The cursors' centres, as fractions of the picture (y down): the left stick's, then the right's.
        public float LeftX, LeftY, RightX, RightY;

        /// <summary>
        /// The overlay on, its two boxes centred under the ink of the <paramref name="chars"/> characters written from
        /// grid cell (<paramref name="row"/>, <paramref name="col"/>), in the row below them; the cursors at the sticks,
        /// each axis −1 (left, down) to 1 (right, up), clamped to its travel.
        /// </summary>
        public static FeedSticks Under(int row, int col, int chars, float leftX, float leftY, float rightX, float rightY)
        {
            // The text's ink: the ROM's capitals are 5 texels wide, centred in their 14 as BuildAtlas centres them.
            const int Ink = 5;
            float inkMid = col * FeedFont.CellW + (FeedFont.CellW - Ink) / 2 + ((chars - 1) * FeedFont.CellW + Ink) / 2f;
            int left = (int)Math.Floor(inkMid - (2 * BoxW + Gap) / 2f), right = left + BoxW + Gap, top = (row + 1) * FeedFont.CellH;
            return new FeedSticks
            {
                On = true,
                LeftBox = X(left),
                RightBox = X(right),
                Top = Y(top),
                LeftX = X(left + BoxW / 2f + Clamp(leftX) * TravelX),
                LeftY = Y(top + BoxH / 2f - Clamp(leftY) * TravelY),
                RightX = X(right + BoxW / 2f + Clamp(rightX) * TravelX),
                RightY = Y(top + BoxH / 2f - Clamp(rightY) * TravelY),
            };
        }

        static float X(float texels) => FeedOsd.OriginX + texels * TexelW;
        static float Y(float texels) => FeedOsd.OriginY + texels * TexelH;
        static float Clamp(float v) => Math.Max(-1f, Math.Min(1f, v));
    }

    /// <summary>
    /// A signals source with the stick overlay (D-084): the rig reads it once per video field, as it lays out the OSD.
    /// </summary>
    public interface IOsdSticks
    {
        /// The overlay at the latest stick sample; default (off) while it is off.
        FeedSticks OsdSticks { get; }
    }
}
