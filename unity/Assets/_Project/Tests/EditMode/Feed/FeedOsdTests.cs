using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The OSD's ROM, grid and stub layout against the Godot feed's <c>FeedFont</c> and <c>FeedOsd</c>
    /// (<c>docs/legacy/video-feed-godot/src/</c>), and SEC-05's two strings (p4-feed task 1.4, the osd spec). Run by
    /// Unity's EditMode and by <c>dotnet test tools/feedtests</c>.
    /// </summary>
    public sealed class FeedOsdTests
    {
        /// The Godot layout at the Godot capture's pinned signals (`--video-signals 1,1,0`: altitude, distance and
        /// timer 0, the 6S stand-in pack at 25.2 V, 0 A), read off FeedOsd.cs:82-125 cell by cell. The one change is
        /// the name's E → I (D-011); both names are 16 characters, so every cell stays where Godot put it.
        static readonly string[] GodotPinned =
        {
            "AIR                     >0m   ",
            "                              ",
            "                              ",
            "                              ",
            "                              ",
            "                              ",
            "                              ",
            "             {|}              ",
            "                              ",
            "                              ",
            "! SAFE !                      ",
            "[00:00                        ",
            "$4.20в SVINORIZ 10 OPTO 0.00  ",
            "$25.2в                00.00a  ",
            "                              ",
            "                              ",
        };

        static string Row(IFeedSignals s, int r)
        {
            var row = new char[FeedOsd.Cols];
            for (int c = 0; c < FeedOsd.Cols; c++)
                row[c] = s.OsdCell(r, c);
            return new string(row);
        }

        [Test]
        public void TheStubIsTheGodotLayoutCellByCell()
        {
            var s = new StubFeedSignals();
            s.BuildOsd(600);
            for (int r = 0; r < FeedOsd.Rows; r++)
                Assert.That(Row(s, r), Is.EqualTo(GodotPinned[r]), $"row {r}");
        }

        /// SEC-05: the OSD reads "Svinoriz 10 Opto" and "! SAFE !", never reverted.
        [Test]
        public void TheStringsAreSvinorizAndSafe()
        {
            Assert.That(StubOsd.Name, Is.EqualTo("Svinoriz 10 Opto"));
            Assert.That(StubOsd.Armed, Is.EqualTo("! SAFE !"));
            var s = new StubFeedSignals();
            s.BuildOsd(1);
            Assert.That(Row(s, 12).Substring(7, 16), Is.EqualTo("SVINORIZ 10 OPTO"));
            Assert.That(Row(s, 10).Substring(0, 8), Is.EqualTo("! SAFE !"));
        }

        /// Every character the layout writes, warnings and the receiver's text included, has a glyph in the ROM.
        [Test]
        public void TheRomDrawsEveryCharacterTheLayoutWrites()
        {
            var s = new StubFeedSignals { PackVolts = 19.8f, LinkMarginDb = 0f, MotorCurrentTotalA = 61.25f };
            s.Osd.AltitudeM = 1234f;
            s.Osd.DistanceM = 5678f;
            s.Osd.ElapsedSeconds = 3599.0;
            foreach (int field in new[] { 0, 11, 12, 20 })
            {
                s.BuildOsd(field);
                for (int r = 0; r < FeedOsd.Rows; r++)
                    foreach (char c in Row(s, r))
                        Assert.That(c == ' ' || FeedFont.Has(c), $"'{c}' in row {r} has no glyph");
            }
            foreach (char c in "AV IN 1PAL 50")
                Assert.That(c == ' ' || FeedFont.Has(c), $"'{c}' in the receiver's text has no glyph");
        }

        /// A warning blinks over the name, 12 fields on and 9 off (FeedOsd.cs:55, 112-118).
        [Test]
        public void ALowPackBlinksOverTheName()
        {
            var s = new StubFeedSignals { PackVolts = 20.4f }; // 3.40 V a cell, under the 3.50 threshold
            s.BuildOsd(11);
            Assert.That(Row(s, 12).Substring(7, 16), Is.EqualTo("    BATT LOW    "));
            s.BuildOsd(12);
            Assert.That(Row(s, 12).Substring(7, 16), Is.EqualTo("SVINORIZ 10 OPTO"));
            s.BuildOsd(21);
            Assert.That(Row(s, 12).Substring(7, 16), Is.EqualTo("    BATT LOW    "));
        }

        /// The codes texture: the OSD's codes in rows 0–15, the receiver's two lines in rows 16–17, row 0 at the top.
        [Test]
        public void TheCodesCarryTheGridAndTheReceiversText()
        {
            var s = new StubFeedSignals();
            s.BuildOsd(1);
            var codes = new float[FeedOsd.Cols * FeedOsd.TotalRows];
            FeedOsd.WriteCodes(s, codes);
            for (int r = 0; r < FeedOsd.Rows; r++)
                for (int c = 0; c < FeedOsd.Cols; c++)
                    Assert.That(codes[r * FeedOsd.Cols + c], Is.EqualTo(FeedFont.Code(s.OsdCell(r, c))));
            string[] rx = { "AV IN 1", "PAL 50" };
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < FeedOsd.Cols; c++)
                    Assert.That(codes[(FeedOsd.RxRow + r) * FeedOsd.Cols + c],
                        Is.EqualTo(FeedFont.Code(c < rx[r].Length ? rx[r][c] : ' ')));
            Assert.That(FeedFont.Code(' '), Is.EqualTo(0f));
            Assert.That(FeedFont.Code('0'), Is.EqualTo(1f));
            Assert.That(FeedFont.Code('}'), Is.EqualTo(FeedFont.CellCount - 1f));
        }

        /// The grid's geometry is the measured one (K§11.6): a 67.3 × 71.0 px cell inset half a cell, which overflows
        /// the 1920 × 1080 frame; never 1/30 × 1/16 of it.
        [Test]
        public void TheGridIsTheMeasuredOne()
        {
            Assert.That(FeedOsd.CellW * 1920f, Is.EqualTo(67.3f).Within(1e-3f));
            Assert.That(FeedOsd.CellH * 1080f, Is.EqualTo(71.0f).Within(1e-3f));
            Assert.That(FeedOsd.OriginX * 1920f, Is.EqualTo(33.7f).Within(1e-3f));
            Assert.That(FeedOsd.OriginY * 1080f, Is.EqualTo(33.5f).Within(1e-3f));
            Assert.That(FeedOsd.OriginX + FeedOsd.Cols * FeedOsd.CellW, Is.GreaterThan(1f));
            Assert.That(FeedOsd.OriginY + FeedOsd.Rows * FeedOsd.CellH, Is.GreaterThan(1f));
        }

        /// The atlas (FeedFont.cs:114-152): one row of 14 × 18 cells, cell 0 blank, each glyph centred, and its
        /// one-texel outline never crossing into the next cell.
        [Test]
        public void TheAtlasIsTheGodotRom()
        {
            byte[] rg = FeedFont.BuildAtlas();
            int w = FeedFont.AtlasWidth, h = FeedFont.CellH;
            Assert.That(FeedFont.CellCount, Is.EqualTo(50));
            Assert.That(rg.Length, Is.EqualTo(w * h * 2));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < FeedFont.CellW; x++)
                    Assert.That(rg[(y * w + x) * 2] + rg[(y * w + x) * 2 + 1], Is.EqualTo(0), "cell 0 is blank");

            // '0' is cell 1: 5 × 8 ink at columns 4–8 and rows 5–12 of its cell, and a 7 × 10 box of ink and outline.
            int ink = 0, inkMinX = 99, inkMaxX = -1, inkMinY = 99, inkMaxY = -1, any = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < FeedFont.CellW; x++)
                {
                    int i = (y * w + FeedFont.CellW + x) * 2;
                    if (rg[i] == 255)
                    {
                        ink++;
                        inkMinX = System.Math.Min(inkMinX, x); inkMaxX = System.Math.Max(inkMaxX, x);
                        inkMinY = System.Math.Min(inkMinY, y); inkMaxY = System.Math.Max(inkMaxY, y);
                    }
                    if (rg[i] == 255 || rg[i + 1] == 255)
                        any++;
                    Assert.That(rg[i] == 255 && rg[i + 1] == 255, Is.False, "ink and outline never overlap");
                }
            Assert.That(new[] { inkMinX, inkMaxX, inkMinY, inkMaxY }, Is.EqualTo(new[] { 4, 8, 5, 12 }));
            Assert.That(ink, Is.EqualTo(18));
            Assert.That(any, Is.LessThanOrEqualTo(7 * 10));

            // The battery mark is 10 × 14, centred: its ink and outline leave the cell's edge columns and rows clear.
            int battery = (int)FeedFont.Code('$');
            for (int y = 0; y < h; y++)
            {
                int left = (y * w + battery * FeedFont.CellW) * 2, right = (y * w + battery * FeedFont.CellW + FeedFont.CellW - 1) * 2;
                Assert.That(rg[left] + rg[left + 1] + rg[right] + rg[right + 1], Is.EqualTo(0), $"row {y}: the battery stays inside its cell");
            }
        }
    }
}
