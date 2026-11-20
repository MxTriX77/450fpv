using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// What the drawn side's GPU port reads of the plants' air (w16-plants-in-the-air 1.3, design.md §7.1): the wind's mean at a
    /// plant (PlantAir.Mean, Respond's first half) and the modes' tables with the frame's mean and drift. Over the farmland in Windy
    /// (its hills and wakes) the tables summed at a load point, each family at the mean's share, are the field's own air there.
    /// </summary>
    public class PlantFieldTests
    {
        static void Print(string line) => TestContext.Out.WriteLine(line);

        [Test]
        public void TheTablesAreTheFieldsOwnAir()
        {
            MapWorld map = SlopeScenarios.Farmland();
            var world = new FlightWorld(map);
            using var wind = new WindField(WindSetup.ForMap(map, WindLevel.Windy), new FlightRandom(0x16A3), world.Hills);
            var plants = new PlantAir(wind);
            PlantMechanics[] kinds = { PlantAirTests.Green(0.97), PlantAirTests.Dry(1.4) };
            var open = new PlantMode[plants.OpenModeCount];
            var wakes = new PlantMode[plants.WakeModeCount];
            plants.WakeModes(wakes);
            var rng = new Random(13);
            var xz = new XZ[1];
            var g = new GroundSample[1];
            int compared = 0, withWake = 0;
            double worst = 0;
            foreach (double t in new[] { 0.0, 12.345, 400.5 })
            {
                plants.Begin(t);
                Double3 v = plants.FrameMean, d = plants.FrameDrift;
                for (int i = 0; i < 200; i++)
                {
                    double x = -450 + 900 * rng.NextDouble(), z = -450 + 900 * rng.NextDouble();
                    xz[0] = new XZ(x, z);
                    map.SampleGround(xz, g);
                    if (!(g[0].Height > double.NegativeInfinity)) continue;
                    PlantMechanics m = kinds[i % 2];
                    var basePoint = new Double3(x, g[0].Height + (i % 3 == 0 ? 4 : 0), z);
                    plants.Mean(m, basePoint, g[0].Height, out PlantMean mean);
                    if (world.Hills != null && world.Hills.Steps > 0 && world.Hills.Lee(x, z, v, out LeeFrame lee) && mean.LoadY >= lee.Lip) continue;
                    var p = new Double3(x, mean.LoadY, z);
                    Double3 sum = default;
                    plants.OpenModes(mean.Lambda, open);
                    foreach (PlantMode o in open)
                        sum = sum + o.Amplitude * (mean.Open * DetMath.Cos(Double3.Dot(o.Wave, p) + o.Phase - (o.Wave.X * d.X + o.Wave.Z * d.Z)));
                    int strip = plants.StripOf(mean);
                    if (strip >= 0)
                    {
                        var eddies = new PlantMode[plants.StripModeCount(strip)];
                        double c = plants.StripModes(strip, eddies);
                        foreach (PlantMode o in eddies)
                            sum = sum + o.Amplitude * (mean.StripShare * DetMath.Cos(Double3.Dot(o.Wave, p) + o.Phase - c * (o.Wave.X * d.X + o.Wave.Z * d.Z)));
                    }
                    if (mean.Wake > 0)
                    {
                        withWake++;
                        foreach (PlantMode o in wakes)
                        {
                            double along = Double3.Dot(o.Wave, p) - (o.Wave.X * d.X + o.Wave.Z * d.Z);
                            sum = sum + o.Amplitude * (mean.Wake * DetMath.Cos(along * o.Scale * mean.WakeInverse + o.Phase));
                        }
                    }
                    Double3 field = plants.Air(p, g[0].Height);
                    worst = Math.Max(worst, (field - (mean.Air + sum)).Length());
                    compared++;
                }
            }
            Print($"{compared} load points over the farmland at 3 frames ({withWake} in a wake): the tables' sum against the field's air within {worst:E2} m/s");
            Assert.That(compared, Is.GreaterThan(500));
            Assert.That(withWake, Is.GreaterThan(0), "some points lie in a wake");
            Assert.That(worst, Is.LessThan(1e-9));
        }
    }
}
