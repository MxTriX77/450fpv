using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The OSD's stick overlay (D-084): two small boxes centred under the craft's name, in the ROM's texels, with each
    /// cursor at its stick's own position. Pixels are at 1920 × 1080, the grid's measured frame. Run by Unity's EditMode
    /// and by <c>dotnet test tools/feedtests</c>; the shader's drawing is the rendered still's (FeedCapture -feedSticks).
    /// </summary>
    public sealed class FeedSticksTests
    {
        // "SVINORIZ 10 OPTO" at row 12 from column 7, StubOsd's and FlightOsd's place.
        const int NameRow = 12, NameCol = 7, NameChars = 16;
        const float W = 1920f, H = 1080f, Px = 0.01f;

        static FeedSticks Centred => FeedSticks.Under(NameRow, NameCol, NameChars, 0f, 0f, 0f, 0f);

        /// The pair's middle is the name's ink's middle, read off the ROM; the boxes are one text row tall and square, in
        /// the row under the name, on the ROM's texel grid, and inside the picture with their outline.
        [Test]
        public void TheBoxesSitCentredUnderTheName()
        {
            FeedSticks s = Centred;
            Assert.That(s.On, Is.True);
            Assert.That(default(FeedSticks).On, Is.False, "off unless set");

            // The name's ink, from the atlas: its leftmost and rightmost inked texel columns across the 16 cells.
            byte[] rg = FeedFont.BuildAtlas();
            int aw = FeedFont.AtlasWidth, first = int.MaxValue, last = -1;
            string name = StubOsd.Name.ToUpperInvariant();
            for (int i = 0; i < name.Length; i++)
            {
                int code = (int)FeedFont.Code(name[i]);
                for (int y = 0; y < FeedFont.CellH; y++)
                    for (int x = 0; x < FeedFont.CellW; x++)
                        if (rg[(y * aw + code * FeedFont.CellW + x) * 2] == 255)
                        {
                            int gx = (NameCol + i) * FeedFont.CellW + x;
                            first = System.Math.Min(first, gx);
                            last = System.Math.Max(last, gx);
                        }
            }
            Assert.That(new[] { first, last }, Is.EqualTo(new[] { 102, 316 }), "column 7's texel 4 to column 22's texel 8");
            float inkMid = FeedOsd.OriginX + (first + last + 1) / 2f * FeedSticks.TexelW;
            float pairMid = (s.LeftBox + s.RightBox + FeedSticks.BoxW * FeedSticks.TexelW) / 2f;
            Assert.That(pairMid * W, Is.EqualTo(inkMid * W).Within(Px), "centred on the name's ink");
            Assert.That(pairMid * W, Is.EqualTo(1040.80f).Within(Px));

            // Texels 190–204 and 214–228 across, 234–251 down: 947.1–1019.2 and 1062.4–1134.5 px, 956.5–1027.5 px.
            Assert.That(s.LeftBox * W, Is.EqualTo(33.7f + 190 * 67.3f / 14).Within(Px));
            Assert.That(s.RightBox * W, Is.EqualTo(33.7f + 214 * 67.3f / 14).Within(Px));
            Assert.That(s.Top * H, Is.EqualTo(33.5f + 13 * 71f).Within(Px), "row 13's top, the row under the name");
            float bw = FeedSticks.BoxW * FeedSticks.TexelW * W, bh = FeedSticks.BoxH * FeedSticks.TexelH * H;
            Assert.That(bh, Is.EqualTo(71f).Within(Px), "one text row");
            Assert.That(bw / bh, Is.EqualTo(1f).Within(0.02f), "square");
            Assert.That((s.Top + (FeedSticks.BoxH + 1) * FeedSticks.TexelH) * H, Is.LessThan(0.9981f * H), "above the receiver's border");
        }

        /// Each cursor's centre at known sticks, in pixels: centred, at the corners, and clamped past the travel.
        [Test]
        public void ACursorSitsAtItsStick()
        {
            const float TexW = 67.3f / 14, TexH = 71f / 18; // 4.807 × 3.944 px
            FeedSticks s = Centred;
            // The left box's middle (190 + 7.5, 234 + 9) texels, the right's (214 + 7.5, 243).
            Assert.That(s.LeftX * W, Is.EqualTo(33.7f + 197.5f * TexW).Within(Px));
            Assert.That(s.LeftX * W, Is.EqualTo(983.11f).Within(Px));
            Assert.That(s.LeftY * H, Is.EqualTo(992.0f).Within(Px));
            Assert.That(s.RightX * W, Is.EqualTo(1098.48f).Within(Px));
            Assert.That(s.RightY * H, Is.EqualTo(992.0f).Within(Px));

            // Full right and up on the left, full left and down on the right: 21.63 px from the middle either way (4.5
            // texels across, 5.48 down), the dot a texel or more inside the frame's inner edge, which is a texel in.
            s = FeedSticks.Under(NameRow, NameCol, NameChars, 1f, 1f, -1f, -1f);
            Assert.That(s.LeftX * W, Is.EqualTo(33.7f + 202 * TexW).Within(Px));
            Assert.That(s.LeftX * W, Is.EqualTo(1004.74f).Within(Px));
            Assert.That(s.LeftY * H, Is.EqualTo(970.37f).Within(Px));
            Assert.That(s.RightX * W, Is.EqualTo(1076.85f).Within(Px));
            Assert.That(s.RightY * H, Is.EqualTo(1013.63f).Within(Px));
            Assert.That((s.LeftX - Centred.LeftX) * W, Is.EqualTo(21.63f).Within(Px), "across");
            Assert.That((Centred.LeftY - s.LeftY) * H, Is.EqualTo(21.63f).Within(Px), "up, as far");
            float dotW = FeedSticks.Dot / 2f * TexW, dotH = FeedSticks.Dot / 2f * TexH;
            Assert.That(s.LeftBox * W + (FeedSticks.BoxW - 1) * TexW - (s.LeftX * W + dotW), Is.EqualTo(TexW).Within(Px), "a texel to the frame's right");
            Assert.That(s.LeftY * H - dotH - (s.Top * H + TexH), Is.GreaterThanOrEqualTo(TexH), "a texel or more to its top");
            Assert.That(s.RightY * H + dotH, Is.LessThanOrEqualTo(s.Top * H + (FeedSticks.BoxH - 2) * TexH + Px), "and to its bottom");

            // A quarter right is a quarter of the travel; past the travel is the travel.
            FeedSticks q = FeedSticks.Under(NameRow, NameCol, NameChars, 0.25f, 0f, 0f, 0.5f);
            Assert.That((q.LeftX - Centred.LeftX) * W, Is.EqualTo(0.25f * FeedSticks.TravelX * TexW).Within(Px));
            Assert.That((Centred.RightY - q.RightY) * H, Is.EqualTo(0.5f * FeedSticks.TravelY * TexH).Within(Px));
            FeedSticks past = FeedSticks.Under(NameRow, NameCol, NameChars, 3f, 1.2f, -7f, -1.01f);
            Assert.That(new[] { past.LeftX, past.LeftY, past.RightX, past.RightY }, Is.EqualTo(new[] { s.LeftX, s.LeftY, s.RightX, s.RightY }));
        }
    }
}
