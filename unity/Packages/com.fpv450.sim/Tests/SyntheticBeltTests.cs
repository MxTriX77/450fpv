using System;
using System.IO;
using System.Reflection;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.2's synthetic belt in the stand-in wind (SyntheticBelt), the harness's belt until 1.3's wake grid: off
    // unless a setup asks for it, so every other flight's wind is bit for bit what it was; inside its region the mean
    // drops by the deficit and the eddies carry the boosted intensity without divergence (they have vorticity); the
    // header records it and reads it back, and an out-of-bounds belt is refused.
    public sealed class SyntheticBeltTests
    {
        static SyntheticBelt Belt() => new SyntheticBelt { Edge = new Double3(0, 0, 100), Across = new Double3(0, 0, 1), Height = 9 };

        static WindSetup WithBelt(WindLevel level)
        {
            WindSetup w = WindSetup.Of(level);
            w.Belt = Belt();
            return w;
        }

        [Test]
        public void TheBeltIsOffUnlessASetupAsks()
        {
            var plain = new StandInWind(WindSetup.Of(WindLevel.Severe), new FlightRandom(7));
            var random = new FlightRandom(7);
            var belted = new StandInWind(WithBelt(WindLevel.Severe), random);
            Assert.That(belted.FromDeg, Is.EqualTo(plain.FromDeg));
            Assert.That(belted.MeanSpeed, Is.EqualTo(plain.MeanSpeed));
            Assert.That(belted.StartGust, Is.EqualTo(plain.StartGust));
            Assert.That(random.Opened, Does.Contain("wind.belt"));
            var gust = new Double3(0.3, -0.1, 0.2);
            foreach (double z in new[] { -50.0, 0, 99.99, 100, 190, 190.01, 400 })
            {
                var point = new Double3(3, 30, z);
                Assert.That(belted.At(gust, point), Is.EqualTo(plain.At(gust, point)), "outside the region at z " + z);
            }
            var none = new FlightRandom(7);
            new StandInWind(WindSetup.Of(WindLevel.Severe), none);
            Assert.That(none.Opened, Does.Not.Contain("wind.belt"));
        }

        [Test]
        public void InsideTheMeanDropsAndTheEddiesRise()
        {
            var wind = new StandInWind(WithBelt(WindLevel.Severe), new FlightRandom(11));
            SyntheticBelt b = wind.Setup.Belt;
            double sigma = Math.Sqrt(b.Boost * b.Boost - 1) * WindPreset.Intensity * wind.MeanSpeed;

            // The plateau (1 to 9 H in): the mean wind times (1 − deficit), the eddies σ_b per component, over a grid
            // of points (the field is frozen, so space stands for time).
            int n = 0;
            Double3 sum = default, sq = default;
            double divergence = 0, curl = 0;
            const double h = 1e-3;
            for (int i = 0; i < 40; i++)
                for (int j = 0; j < 40; j++)
                    for (int k = 0; k < 4; k++)
                    {
                        var p = new Double3(-60 + 3 * i, 20 + 7 * k, 100 + 9 * (1.2 + 6.6 * j / 39.0));
                        Assert.That(b.Weight(p), Is.EqualTo(1), "on the plateau");
                        Double3 e = wind.At(default, p) - wind.Mean * (1 - b.Deficit);
                        sum = sum + e;
                        sq = sq + new Double3(e.X * e.X, e.Y * e.Y, e.Z * e.Z);
                        n++;
                        Double3 dx = (wind.At(default, p + new Double3(h, 0, 0)) - wind.At(default, p - new Double3(h, 0, 0))) * (0.5 / h);
                        Double3 dy = (wind.At(default, p + new Double3(0, h, 0)) - wind.At(default, p - new Double3(0, h, 0))) * (0.5 / h);
                        Double3 dz = (wind.At(default, p + new Double3(0, 0, h)) - wind.At(default, p - new Double3(0, 0, h))) * (0.5 / h);
                        divergence += Math.Abs(dx.X + dy.Y + dz.Z);
                        curl += new Double3(dy.Z - dz.Y, dz.X - dx.Z, dx.Y - dy.X).Length();
                    }
            Double3 mean = sum * (1.0 / n);
            double[] sd = { Math.Sqrt(sq.X / n - mean.X * mean.X), Math.Sqrt(sq.Y / n - mean.Y * mean.Y), Math.Sqrt(sq.Z / n - mean.Z * mean.Z) };
            TestContext.Out.WriteLine($"U {wind.MeanSpeed:F3} m/s: the eddies' mean {mean}, σ {sd[0]:F3} / {sd[1]:F3} / {sd[2]:F3} m/s against σ_b {sigma:F3}; "
                + $"mean |div| {divergence / n:E2} 1/s against mean |curl| {curl / n:F3} 1/s");
            foreach (double s in sd) Assert.That(s / sigma, Is.InRange(0.7, 1.3), "each component's σ near σ_b");
            Assert.That(mean.Length(), Is.LessThan(0.25 * sigma), "the eddies average out, so the mean is the deficit's");
            Assert.That(divergence / n, Is.LessThan(1e-4 * curl / n), "no divergence (to the differences' truncation), and vorticity");
            Assert.That(curl / n, Is.GreaterThan(0.5 * sigma * 2 * Math.PI / b.EddyMax), "vorticity of the eddies' scale");

            // The ramps: 0 at the edge, smooth to 1 one height in.
            Assert.That(b.Weight(new Double3(0, 0, 100)), Is.EqualTo(0));
            Assert.That(b.Weight(new Double3(0, 0, 104.5)), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(b.Weight(new Double3(0, 0, 109)), Is.EqualTo(1));
            Assert.That(b.Weight(new Double3(0, 0, 185.5)), Is.EqualTo(0.5).Within(1e-12));
        }

        [Test]
        public void TheSeedDrawsTheEddies()
        {
            var a = new StandInWind(WithBelt(WindLevel.Windy), new FlightRandom(3));
            var b = new StandInWind(WithBelt(WindLevel.Windy), new FlightRandom(3));
            var c = new StandInWind(WithBelt(WindLevel.Windy), new FlightRandom(4));
            var p = new Double3(1, 25, 140);
            Assert.That(a.At(default, p), Is.EqualTo(b.At(default, p)));
            Assert.That(a.At(default, p) - a.Mean * 0.8, Is.Not.EqualTo(c.At(default, p) - c.Mean * 0.8));
        }

        [Test]
        public void TheHeaderRecordsTheBelt()
        {
            Assert.That(Fields(typeof(SyntheticBelt)), Is.EquivalentTo(LogHeader.BeltFieldsLogged));
            FlightSetup setup = WindTargets.BenchFlight(WindLevel.Severe, 5);
            setup.Wind = WithBelt(WindLevel.Severe);
            setup.Wind.Belt.Deficit = 0.25;
            setup.Wind.Belt.Modes = 17;
            LogHeader h = Named(LogHeader.For(setup, new FlightCore(setup)));
            LogHeader back = LogHeader.Read(new MemoryStream(h.Encode()));
            Assert.That(back.Unknown, Is.Empty);
            SyntheticBelt x = setup.Wind.Belt, y = back.Wind.Belt;
            Assert.That(y, Is.Not.Null);
            foreach (string f in LogHeader.BeltFieldsLogged)
            {
                FieldInfo field = typeof(SyntheticBelt).GetField(f);
                Assert.That(field.GetValue(y), Is.EqualTo(field.GetValue(x)), f);
            }

            // Without a belt, none comes back.
            FlightSetup plain = WindTargets.BenchFlight(WindLevel.Severe, 5);
            LogHeader p = Named(LogHeader.For(plain, new FlightCore(plain)));
            Assert.That(LogHeader.Read(new MemoryStream(p.Encode())).Wind.Belt, Is.Null);
        }

        [Test]
        public void AnOutOfBoundsBeltIsRefused()
        {
            foreach (Action<SyntheticBelt> spoil in new Action<SyntheticBelt>[]
                     {
                         x => x.Height = 0, x => x.Across = new Double3(0, 0, 2), x => x.Across = new Double3(0, 1, 0), x => x.Deficit = 1.5,
                         x => x.Boost = 0.5, x => x.EddyMin = 0, x => x.EddyMax = 1, x => x.Modes = 0, x => x.Ramp = 6,
                     })
            {
                WindSetup w = WithBelt(WindLevel.Severe);
                spoil(w.Belt);
                Assert.Throws<ArgumentException>(() => new StandInWind(w, new FlightRandom(1)));
            }
            WindSetup steady = WindSetup.SteadyFrom(270, 5);
            steady.Belt = Belt();
            Assert.Throws<ArgumentException>(() => new StandInWind(steady, new FlightRandom(1)), "a belt needs a pilot's level");
        }

        static LogHeader Named(LogHeader h)
        {
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            return h;
        }

        static string[] Fields(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var names = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
            return names;
        }
    }
}
