using System;
using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The wet look's numbers (w21-rain-on-camera 1.3) against what Lekner and Dorf 1988 print, and the drop spectrum's
    /// against the lens's (design 3.1). Unity's EditMode runs these, and so does <c>dotnet test tools/feedtests</c>.
    /// </summary>
    public sealed class WetOpticsTests
    {
        // RainPreset's numbers (the sim's, published: Marshall and Palmer's D0 and Atlas et al.'s fall speed).
        const double DropScale = 0.895, DropExponent = 0.21, FallA = 9.65, FallB = 10.3, FallC = 0.6;

        [Test]
        public void InternalReflectionIsTheOneLeknerAndDorfPrint()
        {
            // Their eq. 9 gives 0.475 for water; Angstrom's total internal reflection alone, 1 - 1/n², 0.437.
            Assert.That(WetOptics.InternalReflection, Is.EqualTo(0.475).Within(0.0005));
            Assert.That(1.0 - 1.0 / (WetOptics.WaterIndex * WetOptics.WaterIndex), Is.EqualTo(0.437).Within(0.001));
            // Reciprocity: the water's own internal reflectance for diffuse light is eq. 9's p.
            Assert.That(WetOptics.DiffuseReflectance(1.0 / WetOptics.WaterIndex), Is.EqualTo(WetOptics.InternalReflection).Within(1e-9));
        }

        [Test]
        public void GrainsUnderWaterAbsorbAsThePaperPrints()
        {
            // a_w / a_d at small absorption, 1.07, 1.08 and 1.10 for n_r 1.5, 2 and 2.5 (their text after eq. 11).
            Assert.That(WetOptics.GrainAbsorptionFor(1.5), Is.EqualTo(1.07).Within(0.005));
            Assert.That(WetOptics.GrainAbsorptionFor(2.0), Is.EqualTo(1.08).Within(0.005));
            Assert.That(WetOptics.GrainAbsorptionFor(2.5), Is.EqualTo(1.10).Within(0.005));
            Assert.That(WetOptics.GrainAbsorption, Is.EqualTo(WetOptics.GrainAbsorptionFor(WetOptics.GrainIndex)));
        }

        [Test]
        public void WetSandIsAsAngstromMeasured()
        {
            // Angstrom's sand (LD's table III): 0.182 dry, 0.091 wet.
            Assert.That(WetOptics.Body(0.182), Is.EqualTo(0.091).Within(0.005));
            Assert.That(WetOptics.WetAlbedo(0.182), Is.EqualTo(WetOptics.EntryReflectance + WetOptics.Body(0.182)));
        }

        [Test]
        public void TheBodyDarkensAndKeepsItsOrder()
        {
            Assert.That(WetOptics.Body(0.0), Is.EqualTo(0.0), "a metal has no body to darken");
            double last = 0.0;
            for (int i = 1; i < 100; i++)
            {
                double a = i / 100.0, d = WetOptics.Body(a);
                Assert.That(d, Is.LessThan(a), $"albedo {a}: wet darker than dry");
                Assert.That(d, Is.GreaterThan(last), $"albedo {a}: a brighter surface stays brighter wet");
                last = d;
            }
            // Darker channels darken more (the colour holds): D/A rises with A.
            Assert.That(WetOptics.Body(0.05) / 0.05, Is.LessThan(WetOptics.Body(0.3) / 0.3));
        }

        [Test]
        public void TheTopIsWaterAndStandingWaterIsCalm()
        {
            Assert.That(WetOptics.WaterF0, Is.EqualTo(1.0 / 49.0).Within(1e-12));
            Assert.That(WetOptics.PondRoughness, Is.EqualTo(Math.Sqrt(0.003)).Within(1e-12));
            Assert.That(WetOptics.PondSmoothness, Is.EqualTo(0.766).Within(0.001));
            Assert.That(WetOptics.PondCosine, Is.EqualTo(Math.Cos(Math.Atan(0.02))).Within(1e-12));
        }

        [Test]
        public void TheSpectrumCarriesTheRateAsTheLensDoes()
        {
            // The lens's bins (design 3.1): an intercept of 6 800-7 400 against the published 8 000, over both classes.
            foreach (double rate in new[] { 2.5, 5.0, 10.0, 25.0, 50.0 })
            {
                double n0 = WetOptics.Intercept(rate, DropScale, DropExponent, FallA, FallB, FallC);
                Assert.That(n0, Is.InRange(6500.0, 7600.0), $"{rate} mm/h");
            }
            Assert.That(WetOptics.ExtinctionPerM(0.0, DropScale, DropExponent, FallA, FallB, FallC), Is.EqualTo(0.0));
            double rain = WetOptics.ExtinctionPerM(5.0, DropScale, DropExponent, FallA, FallB, FallC);
            double severe = WetOptics.ExtinctionPerM(30.0, DropScale, DropExponent, FallA, FallB, FallC);
            // Q π N0 / (2 Λ³): 4.3e-4 /m at 5 mm/h, 1.4e-3 at 30 (the design's hand numbers).
            Assert.That(rain, Is.EqualTo(4.3e-4).Within(0.2e-4));
            Assert.That(severe, Is.EqualTo(1.39e-3).Within(0.03e-3));
        }
    }
}
