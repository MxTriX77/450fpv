using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The LOD parity measure's arithmetic (p1-v01-locust task 1.2a; asset-pipeline "Baked impostor LOD", scenario
    /// "Every LOD matches LOD0") on a six-pixel fixture, the renders over black and over white as their sRGB bytes:
    /// LOD0 three opaque pixels of (90, 120, 50) and one half covered (over black (64, 86, 34), over white
    /// (196, 203, 190), the bytes a coverage of 0.5 gives); LOD1 four opaque pixels of (88, 117, 49); LOD2 two opaque
    /// pixels of (70, 95, 40). The expected numbers were computed apart from the code, in double precision, with the
    /// sRGB decoding of IEC 61966-2-1; the limits are the spec's 6 % and 15 %. The averaging over the camera offsets
    /// (tasks 1.2c and 1.2d) on 64 literal crowns, one per offset of the 8 × 8 grid, with their numbers computed apart
    /// from the code too, in exact fractions and 40-digit square roots.
    /// </summary>
    public sealed class LodParityTests
    {
        const double Tolerance = 1e-5;
        static readonly Color32 Black = new Color32(0, 0, 0, 255), White = new Color32(255, 255, 255, 255);

        static (Color32[] overBlack, Color32[] overWhite) Opaque(Color32 colour, int pixels) =>
            (Enumerable.Repeat(colour, pixels).Concat(Enumerable.Repeat(Black, 6 - pixels)).ToArray(),
             Enumerable.Repeat(colour, pixels).Concat(Enumerable.Repeat(White, 6 - pixels)).ToArray());

        static LodParity.Crown Lod0()
        {
            var colour = new Color32(90, 120, 50, 255);
            Color32[] overBlack = { colour, colour, colour, new Color32(64, 86, 34, 255), Black, Black };
            Color32[] overWhite = { colour, colour, colour, new Color32(196, 203, 190, 255), White, White };
            return LodParity.Find(overBlack, overWhite);
        }

        static LodParity.Crown Find((Color32[] overBlack, Color32[] overWhite) renders) => LodParity.Find(renders.overBlack, renders.overWhite);

        [Test]
        public void Find_WeighsTheColourByCoverage()
        {
            LodParity.Crown crown = Lod0();
            Assert.That(crown.Coverage, Is.EqualTo(3.498731).Within(Tolerance));
            Assert.That(crown.R, Is.EqualTo(0.102321).Within(Tolerance));
            Assert.That(crown.G, Is.EqualTo(0.187646).Within(Tolerance));
            Assert.That(crown.B, Is.EqualTo(0.031921).Within(Tolerance));
            Assert.That(crown.Luminance, Is.EqualTo(0.158262).Within(Tolerance));
        }

        [Test]
        public void Find_BackgroundAlone_HasNoCrown()
        {
            LodParity.Crown crown = Find(Opaque(Black, 0));
            Assert.That(crown.Coverage, Is.EqualTo(0.0));
            Assert.That(double.IsNaN(crown.Luminance), Is.True);
            Assert.That(LodParity.Passes(LodParity.Relative(crown.Luminance, 0.158262), LodParity.Relative(crown.Coverage, 3.498731)), Is.False);
        }

        [Test]
        public void AgainstLod0_ALodInsideBothLimitsPasses()
        {
            LodParity.Crown lod0 = Lod0(), lod1 = Find(Opaque(new Color32(88, 117, 49, 255), 4));
            Assert.That(lod1.Coverage, Is.EqualTo(4.0).Within(Tolerance));
            Assert.That(lod1.Luminance, Is.EqualTo(0.150190).Within(Tolerance));
            double luminance = LodParity.Relative(lod1.Luminance, lod0.Luminance), coverage = LodParity.Relative(lod1.Coverage, lod0.Coverage);
            Assert.That(luminance, Is.EqualTo(-0.051004).Within(Tolerance));
            Assert.That(coverage, Is.EqualTo(0.143272).Within(Tolerance));
            Assert.That(LodParity.Passes(luminance, coverage), Is.True);
        }

        [Test]
        public void AgainstLod0_ALodOutsideTheLimitsFails()
        {
            LodParity.Crown lod0 = Lod0(), lod2 = Find(Opaque(new Color32(70, 95, 40, 255), 2));
            Assert.That(lod2.Coverage, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(lod2.Luminance, Is.EqualTo(0.096397).Within(Tolerance));
            double luminance = LodParity.Relative(lod2.Luminance, lod0.Luminance), coverage = LodParity.Relative(lod2.Coverage, lod0.Coverage);
            Assert.That(luminance, Is.EqualTo(-0.390903).Within(Tolerance));
            Assert.That(coverage, Is.EqualTo(-0.428364).Within(Tolerance));
            Assert.That(LodParity.Passes(luminance, coverage), Is.False);
        }

        [Test]
        public void Offsets_AreAn8x8GridAnEighthPixelApartAroundTheReviewsFraming()
        {
            float[] steps = { -0.4375f, -0.3125f, -0.1875f, -0.0625f, 0.0625f, 0.1875f, 0.3125f, 0.4375f };
            var grid = new List<Vector2>();
            foreach (float y in steps)
                foreach (float x in steps)
                    grid.Add(new Vector2(x, y));
            Assert.That(LodParity.Offsets, Is.EqualTo(grid.ToArray()));
            Assert.That(LodParity.Offsets.Length, Is.EqualTo(64));
            Assert.That(LodParity.Offsets[0], Is.EqualTo(new Vector2(-0.4375f, -0.4375f)));
            Assert.That(LodParity.Offsets[31], Is.EqualTo(new Vector2(0.4375f, -0.0625f)));
            Assert.That(LodParity.Offsets[32], Is.EqualTo(new Vector2(-0.4375f, 0.0625f)));
            Assert.That(LodParity.Offsets[63], Is.EqualTo(new Vector2(0.4375f, 0.4375f)));
        }

        [Test]
        public void Average_TakesEachValuesMeanAndItsSpreadOverTheOffsets()
        {
            LodParity.Crown[] samples =
            {
                new LodParity.Crown(90, 0.056, 0.066, 0.037), new LodParity.Crown(97, 0.061, 0.069, 0.041), new LodParity.Crown(104, 0.066, 0.072, 0.037), new LodParity.Crown(96, 0.060, 0.075, 0.041),
                new LodParity.Crown(103, 0.065, 0.067, 0.037), new LodParity.Crown(95, 0.059, 0.070, 0.041), new LodParity.Crown(102, 0.064, 0.073, 0.037), new LodParity.Crown(94, 0.058, 0.076, 0.041),
                new LodParity.Crown(101, 0.063, 0.068, 0.037), new LodParity.Crown(93, 0.057, 0.071, 0.041), new LodParity.Crown(100, 0.062, 0.074, 0.037), new LodParity.Crown(92, 0.056, 0.066, 0.041),
                new LodParity.Crown(99, 0.061, 0.069, 0.037), new LodParity.Crown(91, 0.066, 0.072, 0.041), new LodParity.Crown(98, 0.060, 0.075, 0.037), new LodParity.Crown(90, 0.065, 0.067, 0.041),
                new LodParity.Crown(97, 0.059, 0.070, 0.037), new LodParity.Crown(104, 0.064, 0.073, 0.041), new LodParity.Crown(96, 0.058, 0.076, 0.037), new LodParity.Crown(103, 0.063, 0.068, 0.041),
                new LodParity.Crown(95, 0.057, 0.071, 0.037), new LodParity.Crown(102, 0.062, 0.074, 0.041), new LodParity.Crown(94, 0.056, 0.066, 0.037), new LodParity.Crown(101, 0.061, 0.069, 0.041),
                new LodParity.Crown(93, 0.066, 0.072, 0.037), new LodParity.Crown(100, 0.060, 0.075, 0.041), new LodParity.Crown(92, 0.065, 0.067, 0.037), new LodParity.Crown(99, 0.059, 0.070, 0.041),
                new LodParity.Crown(91, 0.064, 0.073, 0.037), new LodParity.Crown(98, 0.058, 0.076, 0.041), new LodParity.Crown(90, 0.063, 0.068, 0.037), new LodParity.Crown(97, 0.057, 0.071, 0.041),
                new LodParity.Crown(104, 0.062, 0.074, 0.037), new LodParity.Crown(96, 0.056, 0.066, 0.041), new LodParity.Crown(103, 0.061, 0.069, 0.037), new LodParity.Crown(95, 0.066, 0.072, 0.041),
                new LodParity.Crown(102, 0.060, 0.075, 0.037), new LodParity.Crown(94, 0.065, 0.067, 0.041), new LodParity.Crown(101, 0.059, 0.070, 0.037), new LodParity.Crown(93, 0.064, 0.073, 0.041),
                new LodParity.Crown(100, 0.058, 0.076, 0.037), new LodParity.Crown(92, 0.063, 0.068, 0.041), new LodParity.Crown(99, 0.057, 0.071, 0.037), new LodParity.Crown(91, 0.062, 0.074, 0.041),
                new LodParity.Crown(98, 0.056, 0.066, 0.037), new LodParity.Crown(90, 0.061, 0.069, 0.041), new LodParity.Crown(97, 0.066, 0.072, 0.037), new LodParity.Crown(104, 0.060, 0.075, 0.041),
                new LodParity.Crown(96, 0.065, 0.067, 0.037), new LodParity.Crown(103, 0.059, 0.070, 0.041), new LodParity.Crown(95, 0.064, 0.073, 0.037), new LodParity.Crown(102, 0.058, 0.076, 0.041),
                new LodParity.Crown(94, 0.063, 0.068, 0.037), new LodParity.Crown(101, 0.057, 0.071, 0.041), new LodParity.Crown(93, 0.062, 0.074, 0.037), new LodParity.Crown(100, 0.056, 0.066, 0.041),
                new LodParity.Crown(92, 0.061, 0.069, 0.037), new LodParity.Crown(99, 0.066, 0.072, 0.041), new LodParity.Crown(91, 0.060, 0.075, 0.037), new LodParity.Crown(98, 0.065, 0.067, 0.041),
                new LodParity.Crown(90, 0.059, 0.070, 0.037), new LodParity.Crown(97, 0.064, 0.073, 0.041), new LodParity.Crown(104, 0.058, 0.076, 0.037), new LodParity.Crown(96, 0.063, 0.068, 0.041),
            };
            Assert.That(samples.Length, Is.EqualTo(64));
            (LodParity.Crown mean, double luminanceSpread, double coverageSpread, double luminanceError, double coverageError) = LodParity.Average(samples);
            Assert.That(mean.Coverage, Is.EqualTo(96.9843750).Within(Tolerance));
            Assert.That(mean.R, Is.EqualTo(0.0610469).Within(Tolerance));
            Assert.That(mean.G, Is.EqualTo(0.0709531).Within(Tolerance));
            Assert.That(mean.B, Is.EqualTo(0.0390000).Within(Tolerance));
            // The mean of the 64 luminances; pooling the 64 renders' pixels, weighted by coverage, would give 0.0665636.
            Assert.That(mean.Luminance, Is.EqualTo(0.0665400).Within(Tolerance));
            // (0.0696462 − 0.0617802) / 0.0665400 and (104 − 90) / 96.984375.
            Assert.That(luminanceSpread, Is.EqualTo(0.1182145).Within(Tolerance));
            Assert.That(coverageSpread, Is.EqualTo(0.1443531).Within(Tolerance));
            // The sample standard deviation (over 63) over √64, as a fraction of the mean; the population deviation
            // (over 64) would give 0.0044648 and 0.0056249.
            Assert.That(luminanceError, Is.EqualTo(0.0045001).Within(Tolerance));
            Assert.That(coverageError, Is.EqualTo(0.0056694).Within(Tolerance));
        }

        [Test]
        public void Passes_HoldsTheSpecsLimits()
        {
            Assert.That(LodParity.Passes(0.0599, 0.1499), Is.True);
            Assert.That(LodParity.Passes(-0.0599, -0.1499), Is.True);
            Assert.That(LodParity.Passes(0.0601, 0.0), Is.False);
            Assert.That(LodParity.Passes(-0.0601, 0.0), Is.False);
            Assert.That(LodParity.Passes(0.0, 0.1501), Is.False);
            Assert.That(LodParity.Passes(0.0, -0.1501), Is.False);
            // "less than": a LOD exactly at a limit fails.
            Assert.That(LodParity.Passes(0.06, 0.0), Is.False);
            Assert.That(LodParity.Passes(0.0, 0.15), Is.False);
        }
    }
}
