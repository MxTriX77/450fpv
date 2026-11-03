using System;
using Fpv450.Fiber;
using NUnit.Framework;
using UnityEngine;

namespace Fpv450.Tests.Fiber
{
    /// <summary>
    /// The thin-wire coverage rule for the fiber (p7d-tether 1.14, scenario "The line in the chase view": its coverage follows
    /// the rule within 10 % and it never sparkles or breaks into dashes across four quarter-pixel camera shifts), on the CPU
    /// copy of the shader's rule (FiberCoverage): on the FPV camera's render, and through the analog feed's read of it, field
    /// line by field line (Shaders/Feed.shader: one read a line at its centre, the sensor's two taps 0.15 line apart, bilinear
    /// on the render). The line is the dry fiber, 0.25 mm (tether.json's coating), at 1.5, 10 and 50 m. The render is the
    /// feed's at FeedSettings' defaults: 1920 samples across the picture, f = 1.005 half-widths, k1 = 0.33, the shutter's 4°
    /// margin (FpvCamera.RenderCoverage, 1.5047 half-widths), so 964.8 px/rad at its centre.
    /// </summary>
    public sealed class FiberCoverageTests
    {
        const double Diameter = 0.00025, Focal = 1.005, K1 = 0.33, Aspect = 16.0 / 9.0, SceneWidth = 1920;
        const double ApertureLines = 0.15; // Feed.shader's APERTURE_V
        static readonly double[] Distances = { 1.5, 10, 50 };
        static readonly double[] Shifts = { 0, 0.25, 0.5, 0.75 };

        static double Coverage => 1 + K1 * (1 + 1 / (Aspect * Aspect)) + Focal * Math.Tan(4 * Math.PI / 180);
        static double Width => Math.Round(SceneWidth * Coverage);
        static double Height => Math.Round(Width / Aspect);
        static double PixelsPerRad => SceneWidth * 0.5 * Focal;
        static Vector4 Read => FiberCoverage.FeedRead((float)K1, (float)Coverage);

        // The line's true width in render pixels at d metres, at the render's centre.
        static double TrueWidth(double d) => Diameter / d * PixelsPerRad;

        [Test]
        public void TheRenderWeighsTheLineAtItsTrueWidthWhereverItFalls()
        {
            foreach (double d in Distances)
            {
                double w = TrueWidth(d), least = double.MaxValue, most = 0;
                foreach (double shift in Shifts)
                {
                    double sum = 0;
                    for (int j = -4; j <= 4; j++) sum += FiberCoverage.Coverage(j + 0.5 - shift, w / 2);
                    least = Math.Min(least, sum);
                    most = Math.Max(most, sum);
                }
                TestContext.WriteLine($"{d} m: true width {w:F5} px, summed coverage {least:F5}–{most:F5} px over the four shifts");
                Assert.That(least, Is.EqualTo(w).Within(0.01 * w), d + " m: the line weighs its true width");
                Assert.That(most, Is.EqualTo(w).Within(0.01 * w), d + " m");
            }
        }

        [Test]
        public void TheFeedWeighsTheLineAs270DOverDAndNeverDropsIt()
        {
            double lines = Read.w, pitch = 2 / Aspect / lines * Width / (2 * Coverage); // render px a field line, at the centre
            double filter = FiberCoverage.FeedFilterPx(Read, Width, Height, 0, 0, 0, 1);
            TestContext.WriteLine($"render {Width}x{Height}, {PixelsPerRad:F1} px/rad; a field line {pitch:F3} px, the tent's radius across a level line {filter:F3} px; " +
                                  $"the feed's {PixelsPerRad / pitch:F1} lines/rad against the sheet's 270");
            Assert.That(filter, Is.EqualTo(pitch).Within(1e-4 * pitch), "at the centre the tent spans one field line (the render's rounded 16:9)");
            foreach (double d in Distances)
            {
                double rule = 270 * Diameter / d, least = double.MaxValue, most = 0;
                foreach (double shift in Shifts)
                    for (int parity = 0; parity < 2; parity++)
                    {
                        double weight = FieldLines(TrueWidth(d), filter, pitch, shift, parity);
                        least = Math.Min(least, weight);
                        most = Math.Max(most, weight);
                    }
                TestContext.WriteLine($"{d} m: {least:E4}–{most:E4} of a field line over the shifts and both fields, the rule 270·D/d = {rule:E4} " +
                                      $"({least / rule - 1:+0.0%;-0.0%} to {most / rule - 1:+0.0%;-0.0%}), spread {(most - least) / most:0.0%}");
                Assert.That(least, Is.EqualTo(rule).Within(0.10 * rule), d + " m: the feed's weight follows 270·D/d within 10 %");
                Assert.That(most, Is.EqualTo(rule).Within(0.10 * rule), d + " m");
                Assert.That((most - least) / most, Is.LessThan(0.10), d + " m: no field line drops it (no dashes, no sparkle)");
            }
        }

        [Test]
        public void AOnePixelTentBreaksTheLineIntoDashesOnTheFeed()
        {
            // The known-wrong rule: the render's own pixel only (the wires' material), read by the feed's field lines.
            double pitch = 2 / Aspect / Read.w * Width / (2 * Coverage);
            double least = double.MaxValue, most = 0;
            foreach (double shift in Shifts)
                for (int parity = 0; parity < 2; parity++)
                {
                    double weight = FieldLines(TrueWidth(10), FiberCoverage.FilterPx, pitch, shift, parity);
                    least = Math.Min(least, weight);
                    most = Math.Max(most, weight);
                }
            TestContext.WriteLine($"10 m with a one-pixel tent: {least:E4}–{most:E4} of a field line, spread {(most - least) / most:0.0%}");
            Assert.That((most - least) / most, Is.GreaterThan(0.5), "a one-pixel line falls between field lines");
        }

        [Test]
        public void TheFeedsTapsAlongALineTakeAnUprightLineWhereverItFalls()
        {
            double tap = 2 / Read.z * Width / (2 * Coverage); // render px between taps, at the centre
            double filter = FiberCoverage.FeedFilterPx(Read, Width, Height, 0, 0, 1, 0);
            Assert.That(filter, Is.EqualTo(tap).Within(1e-4 * tap), "an upright line: the tent spans one tap");
            foreach (double d in Distances)
            {
                double w = TrueWidth(d), least = double.MaxValue, most = 0;
                foreach (double shift in Shifts)
                {
                    // The taps along one field line, bilinear on the render's columns.
                    double sum = 0;
                    for (int t = -8; t <= 8; t++) sum += Bilinear(t * tap, shift, w, filter);
                    least = Math.Min(least, sum);
                    most = Math.Max(most, sum);
                }
                Assert.That(least * tap, Is.EqualTo(w).Within(0.10 * w), d + " m: the taps take the line's width");
                Assert.That((most - least) / most, Is.LessThan(0.10), d + " m");
            }
        }

        [Test]
        public void TheFeedsSpacingFollowsTheLens()
        {
            // Off the centre the lens's barrel reads more render pixels a field line (p_u = p_d (1 + k1 r_d²)): the tent grows
            // with it, so the edges' field lines take the line too.
            double centre = FiberCoverage.FeedFilterPx(Read, Width, Height, 0, 0, 0, 1);
            double edge = FiberCoverage.FeedFilterPx(Read, Width, Height, 0, 0.95, 0, 1);
            double side = FiberCoverage.FeedFilterPx(Read, Width, Height, 0.95, 0, 1, 0);
            TestContext.WriteLine($"a field line: {centre:F2} px at the centre, {edge:F2} px near the top; a tap: {side:F2} px near the side");
            Assert.That(edge, Is.GreaterThan(1.1 * centre));
            Assert.That(side, Is.GreaterThan(2 * FiberCoverage.FeedFilterPx(Read, Width, Height, 0, 0, 1, 0)), "radially the barrel reads 1 + 3 k1 r²");
            Assert.That(FiberCoverage.FeedFilterPx(Vector4.zero, Width, Height, 0.5, 0.5, 0, 1), Is.EqualTo(FiberCoverage.FilterPx), "no feed: one pixel");
        }

        // The feed's read of a level line at the render's centre, its centre line `line` render px down from a pixel row's top
        // edge: each render pixel at the line's coverage of it, each field line read once at its centre (the parity's half
        // line) through the sensor's two taps, bilinear; their sum, in field lines. The field lines' grid is fixed on the
        // render's, the line moves.
        static double FieldLines(double w, double filter, double pitch, double line, int parity)
        {
            double sum = 0;
            for (int k = -6; k <= 6; k++)
            {
                double y = (k + 0.5 * parity) * pitch; // render px
                sum += 0.5 * (Bilinear(y - ApertureLines * pitch, line, w, filter) + Bilinear(y + ApertureLines * pitch, line, w, filter));
            }
            return sum;
        }

        // The render read at y px, linear between its pixel centres (the half pixels), each pixel the coverage of it by a line
        // of width w px centred at `line` px.
        static double Bilinear(double y, double line, double w, double filter)
        {
            double below = Math.Floor(y - 0.5) + 0.5, f = y - below;
            return (1 - f) * FiberCoverage.Coverage(below - line, w / 2, filter) + f * FiberCoverage.Coverage(below + 1 - line, w / 2, filter);
        }
    }
}
