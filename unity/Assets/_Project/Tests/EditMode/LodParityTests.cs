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
    /// sRGB decoding of IEC 61966-2-1; the limits are the spec's 6 % and 15 %.
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
        public void Passes_HoldsTheSpecsLimits()
        {
            Assert.That(LodParity.Passes(0.0599, 0.1499), Is.True);
            Assert.That(LodParity.Passes(-0.0599, -0.1499), Is.True);
            Assert.That(LodParity.Passes(0.0601, 0.0), Is.False);
            Assert.That(LodParity.Passes(-0.0601, 0.0), Is.False);
            Assert.That(LodParity.Passes(0.0, 0.1501), Is.False);
            Assert.That(LodParity.Passes(0.0, -0.1501), Is.False);
        }
    }
}
