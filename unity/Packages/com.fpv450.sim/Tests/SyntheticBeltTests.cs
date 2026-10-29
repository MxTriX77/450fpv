using System;
using System.IO;
using System.Reflection;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.2's synthetic belt (SyntheticBelt), the harness's belt until 1.3's wake grid, in 1.4's field: off
    // unless a setup asks for it, so every other flight's wind is bit for bit what it is without; inside its region the
    // mean drops by the deficit and the eddies carry the boosted intensity without divergence (they have vorticity), and
    // they advect with the field's drift; the header records it and reads it back, and an out-of-bounds belt is refused.
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
            var plain = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(7));
            var random = new FlightRandom(7);
            var belted = new WindField(WithBelt(WindLevel.Severe), random);
            Assert.That(belted.FromDeg, Is.EqualTo(plain.FromDeg));
            Assert.That(belted.MeanSpeed, Is.EqualTo(plain.MeanSpeed));
            Assert.That(random.Opened, Does.Contain("wind.belt"));
            plain.Begin(4321, default, 0);
            belted.Begin(4321, default, 0);
            foreach (double z in new[] { -50.0, 0, 99.99, 100, 190, 190.01, 400 })
            {
                var point = new Double3(3, 30, z);
                Assert.That(belted.At(point), Is.EqualTo(plain.At(point)), "outside the region at z " + z);
            }
            var none = new FlightRandom(7);
            new WindField(WindSetup.Of(WindLevel.Severe), none);
            Assert.That(none.Opened, Does.Not.Contain("wind.belt"));
        }

        [Test]
        public void InsideTheMeanDropsAndTheEddiesRise()
        {
            var wind = new WindField(WithBelt(WindLevel.Severe), new FlightRandom(11));
            SyntheticBelt b = wind.Setup.Belt;
            double sigma = Math.Sqrt(b.Boost * b.Boost - 1) * wind.Preset.Intensity * wind.MeanSpeed;
            wind.Begin(0, default, 0);
            // The belt alone: the field less its open-air layers at the same point.
            Double3 Eddies(Double3 q) => wind.At(q) - Open(wind, q) - wind.Mean * Profile(wind, q) * (-b.Deficit * b.Weight(q));

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
                        Double3 e = Eddies(p);
                        sum = sum + e;
                        sq = sq + new Double3(e.X * e.X, e.Y * e.Y, e.Z * e.Z);
                        n++;
                        Double3 dx = (Eddies(p + new Double3(h, 0, 0)) - Eddies(p - new Double3(h, 0, 0))) * (0.5 / h);
                        Double3 dy = (Eddies(p + new Double3(0, h, 0)) - Eddies(p - new Double3(0, h, 0))) * (0.5 / h);
                        Double3 dz = (Eddies(p + new Double3(0, 0, h)) - Eddies(p - new Double3(0, 0, h))) * (0.5 / h);
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

            // Advected: 4 s on, the eddies a point meets are the ones that were the drift upwind of it (both on the
            // plateau, so the weight is 1 at either).
            var q = new Double3(2, 30, 125);
            Double3 then = Eddies(q);
            wind.Begin(4000, default, 0);
            Double3 drift = wind.Drift;
            Assert.That(b.Weight(q + drift), Is.EqualTo(1), "still on the plateau");
            Double3 now = Eddies(q + drift);
            Assert.That((now - then).Length(), Is.LessThan(1e-9 * sigma), "frozen and advected with the drift " + drift);
            Assert.That(drift.Length(), Is.GreaterThan(3.5 * wind.MeanSpeed), "the drift is the mean wind's");
        }

        // The field's open-air layers at a point (the profile's mean and the tapered turbulence), and the profile.
        static double Profile(WindField w, Double3 q)
        {
            WindPreset p = w.Preset;
            return Math.Log((Math.Min(q.Y, p.ProfileTop) - p.Displacement) / p.Roughness) / Math.Log((p.ReferenceHeight - p.Displacement) / p.Roughness);
        }

        static Double3 Open(WindField w, Double3 q) =>
            w.Mean * Profile(w, q) + w.Turbulence(q) * Math.Min(1, (q.Y - w.Preset.Displacement) / w.Preset.TaperHeight);

        [Test]
        public void TheSeedDrawsTheEddies()
        {
            var a = new WindField(WithBelt(WindLevel.Windy), new FlightRandom(3));
            var b = new WindField(WithBelt(WindLevel.Windy), new FlightRandom(3));
            var c = new WindField(WithBelt(WindLevel.Windy), new FlightRandom(4));
            var p = new Double3(1, 25, 140);
            foreach (WindField w in new[] { a, b, c }) w.Begin(777, default, 0);
            Assert.That(a.At(p), Is.EqualTo(b.At(p)));
            Assert.That(a.At(p) - Open(a, p), Is.Not.EqualTo(c.At(p) - Open(c, p)));
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
                Assert.Throws<ArgumentException>(() => new WindField(w, new FlightRandom(1)));
            }
            WindSetup steady = WindSetup.SteadyFrom(270, 5);
            steady.Belt = Belt();
            Assert.Throws<ArgumentException>(() => new WindField(steady, new FlightRandom(1)), "a belt needs a pilot's level");
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
