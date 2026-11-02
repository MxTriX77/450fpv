using System;
using System.IO;
using System.Reflection;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.5: the field carries a wooded strip's canopy flow (CanopyFlow, belt-geometry-p.md). Off unless a setup has
    // a strip, so every other flight's wind is bit for bit what it is without, and outside the strip the open field's;
    // its roughness is Raupach (1994)'s and its profile continuous to the open field's at the blending height; at its top
    // the eddies carry the canopy's σ/u* beyond the open field's, along, across and up, and below it every σ falls as
    // the mean does to the CanopyDecay power; the seed draws them; the header records the strip; out of bounds or
    // without a pilot's level refused (with wakes, a map's: MapWindTests).
    public sealed class CanopyFieldTests
    {
        // The harness's strip (16.6 m, λ 0.3) across a course north from the origin, the wind from the west.
        static CanopyStrip Strip() => new CanopyStrip
        {
            CentreX = 0, CentreZ = 300, AxisDeg = 90, Length = 1000, Width = 141, Height = 16.6, FrontalAreaIndex = 0.3,
        };

        static WindSetup WithStrip(WindLevel level)
        {
            WindSetup w = WindSetup.Of(level);
            w.Canopies = new[] { Strip() };
            return w;
        }

        [Test]
        public void TheCanopyIsOffUnlessASetupHasAStrip()
        {
            var plain = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(7));
            var random = new FlightRandom(7);
            var strip = new WindField(WithStrip(WindLevel.Severe), random);
            Assert.That(strip.FromDeg, Is.EqualTo(plain.FromDeg));
            Assert.That(strip.MeanSpeed, Is.EqualTo(plain.MeanSpeed));
            Assert.That(random.Opened, Does.Contain("wind.canopy"));
            Assert.That(plain.Canopy, Is.Null);
            int inside = 0;
            foreach (long step in new long[] { 0, 1234, 30000 })
            {
                plain.Begin(step, default, 0);
                strip.Begin(step, default, 0);
                for (double x = -300; x <= 300; x += 37)
                    for (double z = -100; z <= 700; z += 23)
                        foreach (double y in new[] { 5.0, 12, 30, 60 })
                        {
                            var p = new Double3(x, y, z);
                            if (strip.Canopy.Weight(p) > 0 && y < strip.Canopy.BlendingHeight)
                            {
                                inside++;
                                Assert.That(strip.At(p), Is.Not.EqualTo(plain.At(p)), "inside the strip its flow");
                            }
                            else Assert.That(strip.At(p), Is.EqualTo(plain.At(p)), "outside the strip the open field's, bit for bit");
                        }
            }
            Assert.That(inside, Is.GreaterThan(100));
        }

        [Test]
        public void TheStripsRoughnessIsRaupachsAndItsProfileContinuous()
        {
            var wind = new WindField(WithStrip(WindLevel.Severe), new FlightRandom(3));
            CanopyFlow c = wind.Canopy;
            double h = 16.6, lambda = 0.3, x = Math.Sqrt(7.5 * 2 * lambda);
            double dh = 1 - (1 - Math.Exp(-x)) / x, beta = Math.Min(Math.Sqrt(0.003 + 0.3 * lambda), 0.3);
            double z0h = (1 - dh) * Math.Exp(-0.4 / beta + Math.Log(2) - 0.5);
            TestContext.Out.WriteLine($"h {h} m, λ {lambda}: d/h {c.Displacement / h:F3}, z0/h {c.Roughness / h:F3}, β {c.Beta:F3}, α {c.Attenuation:F3}, "
                + $"z_w {c.SublayerTop:F1} m, z_b {c.BlendingHeight:F1} m; U {wind.MeanSpeed:F2} m/s at 35 m: u* {c.FrictionVelocity:F3} m/s, U(h) {c.TopSpeed:F2} m/s; "
                + $"eddies' σ {c.ExcessU:F2} / {c.ExcessV:F2} / {c.ExcessW:F2} m/s beyond the open field's {wind.SigmaH:F2} / {wind.SigmaV:F2}");
            Assert.That(c.Displacement / h, Is.EqualTo(dh).Within(1e-12));
            Assert.That(c.Beta, Is.EqualTo(beta).Within(1e-12));
            Assert.That(c.Roughness / h, Is.EqualTo(z0h).Within(1e-12));
            Assert.That(c.SublayerTop, Is.EqualTo(c.Displacement + 2 * (h - c.Displacement)).Within(1e-9));
            Assert.That(c.TopSpeed, Is.EqualTo(c.FrictionVelocity / beta).Within(1e-12));
            Assert.That(c.Attenuation, Is.InRange(0.5, 4), "Cionco's range of α");

            // Continuous at h, z_w and z_b (there the open field's mean), and slower than the open field below z_b.
            double Open(double z) => wind.MeanSpeed * Math.Log((Math.Min(z, wind.Preset.ProfileTop) - wind.Preset.Displacement) / wind.Preset.Roughness)
                / Math.Log((wind.Preset.ReferenceHeight - wind.Preset.Displacement) / wind.Preset.Roughness);
            foreach (double z in new[] { c.Height, c.SublayerTop })
                Assert.That(c.Speed(z - 1e-9), Is.EqualTo(c.Speed(z + 1e-9)).Within(1e-6), "continuous at " + z);
            Assert.That(c.Speed(c.BlendingHeight - 1e-9), Is.EqualTo(Open(c.BlendingHeight)).Within(1e-6), "the open field's at z_b");
            for (double z = 1; z < c.BlendingHeight; z += 0.5)
                Assert.That(c.Speed(z), Is.LessThan(Open(z)), "sheltered at " + z);
            Assert.That(c.Decay(c.Height), Is.EqualTo(1));
            Assert.That(c.Decay(c.Height / 2), Is.EqualTo(Math.Pow(c.Speed(c.Height / 2) / c.TopSpeed, wind.Preset.CanopyDecay)).Within(1e-9));
        }

        [Test]
        public void TheCanopysTopCarriesItsSigmaOverUStar()
        {
            // The strip's air less the open field's on the same seed is the canopy's eddies at its top (w = 1, r = 1), and
            // its mean along the wind is the strip's profile.
            foreach (WindLevel level in new[] { WindLevel.Severe, WindLevel.Windy })
            {
                var plain = new WindField(WindSetup.Of(level), new FlightRandom(11));
                var strip = new WindField(WithStrip(level), new FlightRandom(11));
                CanopyFlow c = strip.Canopy;
                Double3 along = WindField.Toward(strip.FromDeg, 1), across = new Double3(-along.Z, 0, along.X);
                double su = 0, sv = 0, sw = 0;
                int n = 0;
                foreach (long step in new long[] { 0, 20000, 40000, 60000 })
                {
                    plain.Begin(step, default, 0);
                    strip.Begin(step, default, 0);
                    for (double x = -450; x <= 450; x += 3.1)
                        for (double z = 300 - 50; z <= 300 + 50; z += 9.7)
                        {
                            var p = new Double3(x, c.Height, z);
                            Double3 d = strip.At(p) - plain.At(p) - strip.Mean * (c.Speed(c.Height) / strip.MeanSpeed)
                                + plain.Mean * (Math.Log((c.Height - plain.Preset.Displacement) / plain.Preset.Roughness)
                                / Math.Log((plain.Preset.ReferenceHeight - plain.Preset.Displacement) / plain.Preset.Roughness));
                            su += Math.Pow(Double3.Dot(d, along), 2);
                            sv += Math.Pow(Double3.Dot(d, across), 2);
                            sw += d.Y * d.Y;
                            n++;
                        }
                }
                su = Math.Sqrt(su / n);
                sv = Math.Sqrt(sv / n);
                sw = Math.Sqrt(sw / n);
                double ustar = c.FrictionVelocity;
                double tu = Math.Sqrt(su * su + strip.SigmaH * strip.SigmaH) / ustar, tv = Math.Sqrt(sv * sv + strip.SigmaH * strip.SigmaH) / ustar,
                    tw = Math.Sqrt(sw * sw + strip.SigmaV * strip.SigmaV) / ustar;
                TestContext.Out.WriteLine($"{level}: u* {ustar:F3} m/s; the eddies' σ {su:F3} / {sv:F3} / {sw:F3} m/s against {c.ExcessU:F3} / {c.ExcessV:F3} / {c.ExcessW:F3}; "
                    + $"with the open field's, σ/u* {tu:F2} / {tv:F2} / {tw:F2} (the preset's {strip.Preset.CanopySigmaU} / {strip.Preset.CanopySigmaV} / {strip.Preset.CanopySigmaW}); {n} points");
                Assert.That(su / c.ExcessU, Is.InRange(0.85, 1.15), "σ_u");
                Assert.That(sv / c.ExcessV, Is.InRange(0.85, 1.15), "σ_v");
                Assert.That(sw / c.ExcessW, Is.InRange(0.85, 1.15), "σ_w");
                Assert.That(su > sv && sv > sw, Is.True, "u along the wind, then v, then w");
            }
        }

        [Test]
        public void BelowTheTopEverySigmaFalls()
        {
            var strip = new WindField(WithStrip(WindLevel.Severe), new FlightRandom(5));
            CanopyFlow c = strip.Canopy;
            double Sigma(double y)
            {
                double s = 0;
                int n = 0;
                foreach (long step in new long[] { 0, 30000 })
                {
                    strip.Begin(step, default, 0);
                    for (double x = -450; x <= 450; x += 3.1)
                    {
                        Double3 e = c.Eddies(new Double3(x, y, 300)) * c.Decay(y);
                        s += e.X * e.X + e.Z * e.Z;
                        n++;
                    }
                }
                return Math.Sqrt(s / n);
            }
            double top = Sigma(c.Height), half = Sigma(c.Height / 2);
            TestContext.Out.WriteLine($"the eddies' horizontal σ at h {top:F3} m/s, at h/2 {half:F3} m/s: {half / top:F3} (Decay {c.Decay(c.Height / 2):F3})");
            Assert.That(half / top, Is.EqualTo(c.Decay(c.Height / 2)).Within(0.1));
            Assert.That(c.Layer(c.SublayerTop), Is.EqualTo(1));
            Assert.That(c.Layer(c.BlendingHeight), Is.EqualTo(0));
            Assert.That(c.Weight(new Double3(0, 10, 300 + 70.5)), Is.EqualTo(0), "the strip's edge");
            Assert.That(c.Weight(new Double3(0, 10, 300)), Is.EqualTo(1), "its middle");
            Assert.That(c.Weight(new Double3(0, 10, 300 + 70.5 - 8.3)), Is.InRange(0.3, 0.7), "half an edge blend in");
        }

        [Test]
        public void TheSeedDrawsTheCanopy()
        {
            var a = new WindField(WithStrip(WindLevel.Windy), new FlightRandom(3));
            var b = new WindField(WithStrip(WindLevel.Windy), new FlightRandom(3));
            var other = new WindField(WithStrip(WindLevel.Windy), new FlightRandom(4));
            foreach (WindField w in new[] { a, b, other }) w.Begin(777, default, 0);
            int differ = 0;
            for (int i = -100; i < 100; i += 7)
            {
                var p = new Double3(i, 9, 290);
                Assert.That(a.At(p), Is.EqualTo(b.At(p)));
                if (!a.Canopy.Eddies(p).Equals(other.Canopy.Eddies(p))) differ++;
            }
            Assert.That(differ, Is.GreaterThan(20), "another seed, other eddies");
        }

        [Test]
        public void TheHeaderRecordsTheStrip()
        {
            Assert.That(Fields(typeof(CanopyStrip)), Is.EquivalentTo(LogHeader.CanopyFieldsLogged));
            FlightSetup setup = WindTargets.BenchFlight(WindLevel.Severe, 5);
            setup.Wind = WithStrip(WindLevel.Severe);
            LogHeader h = Named(LogHeader.For(setup, new FlightCore(setup)));
            LogHeader back = LogHeader.Read(new MemoryStream(h.Encode()));
            Assert.That(back.Unknown, Is.Empty);
            CanopyStrip x = setup.Wind.Canopies[0];
            Assert.That(back.Wind.Canopies, Has.Length.EqualTo(1));
            CanopyStrip y = back.Wind.Canopies[0];
            Assert.That(new[] { y.CentreX, y.CentreZ, y.AxisDeg, y.Length, y.Width, y.Height, y.FrontalAreaIndex },
                Is.EqualTo(new[] { x.CentreX, x.CentreZ, x.AxisDeg, x.Length, x.Width, x.Height, x.FrontalAreaIndex }));
            Assert.That(back.WindParameters, Is.EqualTo(LogHeader.PresetOf(WindLevel.Severe)));

            FlightSetup plain = WindTargets.BenchFlight(WindLevel.Severe, 5);
            LogHeader p = Named(LogHeader.For(plain, new FlightCore(plain)));
            Assert.That(LogHeader.Read(new MemoryStream(p.Encode())).Wind.Canopies, Is.Null);
        }

        [Test]
        public void AnOutOfBoundsStripIsRefused()
        {
            foreach (Action<CanopyStrip> spoil in new Action<CanopyStrip>[]
                     { s => s.Height = 0.5, s => s.Width = 0, s => s.FrontalAreaIndex = 0, s => s.CentreX = double.NaN, s => s.Height = 40 })
            {
                WindSetup w = WithStrip(WindLevel.Severe);
                spoil(w.Canopies[0]);
                Assert.Throws<ArgumentException>(() => new WindField(w, new FlightRandom(1)));
            }
            WindSetup steady = WindSetup.SteadyFrom(270, 5);
            steady.Canopies = new[] { Strip() };
            Assert.Throws<ArgumentException>(() => new WindField(steady, new FlightRandom(1)), "a canopy needs a pilot's level");
            WindSetup none = WindSetup.Of(WindLevel.Severe);
            none.Canopies = new CanopyStrip[0];
            Assert.Throws<ArgumentException>(() => new WindField(none, new FlightRandom(1)), "null or at least one strip");
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
