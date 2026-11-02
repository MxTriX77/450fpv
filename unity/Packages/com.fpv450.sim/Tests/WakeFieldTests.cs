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
    // p7c-wind 1.5: the field carries the wakes of a setup's obstacles from 1.3's wake grid, which replace 1.2's synthetic
    // belt. Off unless a setup has obstacles, so every other flight's wind is bit for bit what it is without; outside the
    // grid the open field's; in a wake the mean scales by the grid's speed and the wake eddies carry √(T² − 1)·σ_u at the
    // obstacle's scale, without divergence; the header records the obstacles and reads them back; out of bounds refused.
    public sealed class WakeFieldTests
    {
        static WindSetup WithWakes(WindLevel level)
        {
            WindSetup w = WindSetup.Of(level);
            w.Wakes = new WakeLayout { OriginX = -300, OriginZ = -300, NX = 300, NZ = 300, Volumes = WakeGridFixture.Volumes() };
            return w;
        }

        static double Profile(WindField w, Double3 q)
        {
            WindPreset p = w.Preset;
            return Math.Log((Math.Min(q.Y, p.ProfileTop) - p.Displacement) / p.Roughness) / Math.Log((p.ReferenceHeight - p.Displacement) / p.Roughness);
        }

        static double Taper(WindField w, Double3 q) => Math.Min(1, (q.Y - w.Preset.Displacement) / w.Preset.TaperHeight);

        [Test]
        public void TheWakesAreOffUnlessASetupHasObstacles()
        {
            var plain = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(7));
            var random = new FlightRandom(7);
            using var waked = new WindField(WithWakes(WindLevel.Severe), random);
            Assert.That(waked.FromDeg, Is.EqualTo(plain.FromDeg));
            Assert.That(waked.MeanSpeed, Is.EqualTo(plain.MeanSpeed));
            Assert.That(random.Opened, Does.Contain("wind.wake"));
            Assert.That(waked.Wakes.Active.FromDeg, Is.EqualTo(waked.FromDegAt(0)), "built for the history's first bearing");
            plain.Begin(4321, default, 0);
            waked.Begin(4321, default, 0);
            foreach (var point in new[] { new Double3(-400, 30, 0), new Double3(0, 30, 400), new Double3(350, 2, -350) })
                Assert.That(waked.At(point), Is.EqualTo(plain.At(point)), "outside the grid at " + point);
            var none = new FlightRandom(7);
            new WindField(WindSetup.Of(WindLevel.Severe), none);
            Assert.That(none.Opened, Does.Not.Contain("wind.wake"));
        }

        [Test]
        public void InAWakeTheMeanFollowsTheGridAndTheEddiesCarryItsTurbulence()
        {
            using var wind = new WindField(WithWakes(WindLevel.Severe), new FlightRandom(11));
            WakeGridWorker grid = wind.Wakes;
            wind.Begin(0, default, 0);
            WindPreset preset = wind.Preset;

            // The points of the wake (T > 1.3) at 0.5 H over a 2 m lattice, and the eddies there: the field less its
            // open-air turbulence and its scaled mean; frozen, so space stands for time.
            int n = 0;
            double sq = 0, along = 0, expected = 0, divergence = 0, curl = 0;
            const double h = 1e-3;
            for (int i = -140; i < 140; i += 3)
                for (int j = -250; j < 250; j += 3)
                {
                    var p = new Double3(i, 4.2, j);
                    grid.Sample(p.X, p.Z, p.Y, out WakeSample s);
                    if (!(s.Turbulence > 1.3)) continue;
                    Double3 Eddies(Double3 q)
                    {
                        grid.Sample(q.X, q.Z, q.Y, out WakeSample t);
                        return wind.At(q) - wind.Turbulence(q) * Taper(wind, q) - wind.Mean * (Profile(wind, q) * t.Speed);
                    }
                    Double3 e = Eddies(p);
                    double sigma = Math.Sqrt(s.Turbulence * s.Turbulence - 1) * wind.SigmaH;
                    sq += (e.X * e.X + e.Y * e.Y + e.Z * e.Z) / 3;
                    expected += sigma * sigma;
                    Double3 dx = (Eddies(p + new Double3(h, 0, 0)) - Eddies(p - new Double3(h, 0, 0))) * (0.5 / h);
                    Double3 dy = (Eddies(p + new Double3(0, h, 0)) - Eddies(p - new Double3(0, h, 0))) * (0.5 / h);
                    Double3 dz = (Eddies(p + new Double3(0, 0, h)) - Eddies(p - new Double3(0, 0, h))) * (0.5 / h);
                    // The grid's own gradients make the eddies' amplitude vary; their divergence is that of a plane wave's
                    // amplitude's, small against the curl at the eddies' scale.
                    divergence += Math.Abs(dx.X + dy.Y + dz.Z);
                    curl += new Double3(dy.Z - dz.Y, dz.X - dx.Z, dx.Y - dy.X).Length();
                    along += s.Speed;
                    n++;
                }
            Assert.That(n, Is.GreaterThan(200), "the wake has points at 0.5 H");
            double ratio = Math.Sqrt(sq / expected);
            TestContext.Out.WriteLine($"U {wind.MeanSpeed:F3} m/s from {wind.FromDeg:F1}°: {n} wake points, their mean speed ratio {along / n:F3}; "
                + $"the eddies' σ over √(T² − 1)·σ_u {ratio:F3}; mean |div| {divergence / n:F4} against |curl| {curl / n:F3} 1/s");
            Assert.That(ratio, Is.InRange(0.8, 1.2), "the eddies carry the grid's extra turbulence");
            Assert.That(along / n, Is.LessThan(0.95), "sheltered");
            Assert.That(divergence / n, Is.LessThan(0.2 * curl / n), "vorticity, little divergence");
        }

        [Test]
        public void TheSeedDrawsTheEddies()
        {
            using var a = new WindField(WithWakes(WindLevel.Windy), new FlightRandom(3));
            using var b = new WindField(WithWakes(WindLevel.Windy), new FlightRandom(3));
            foreach (WindField w in new[] { a, b }) w.Begin(777, default, 0);
            for (int i = -100; i < 100; i += 7)
            {
                var p = new Double3(i, 5, -20);
                Assert.That(a.At(p), Is.EqualTo(b.At(p)));
            }
        }

        [Test]
        public void TheHeaderRecordsTheWakes()
        {
            Assert.That(Fields(typeof(WakeLayout)), Is.EquivalentTo(LogHeader.WakeFieldsLogged));
            Assert.That(Fields(typeof(WindVolume)), Is.EquivalentTo(LogHeader.VolumeFieldsLogged));
            FlightSetup setup = WindTargets.BenchFlight(WindLevel.Severe, 5);
            setup.Wind = WithWakes(WindLevel.Severe);
            var core = new FlightCore(setup);
            LogHeader h = Named(LogHeader.For(setup, core));
            core.Wind.Dispose();
            LogHeader back = LogHeader.Read(new MemoryStream(h.Encode()));
            Assert.That(back.Unknown, Is.Empty);
            WakeLayout x = setup.Wind.Wakes, y = back.Wind.Wakes;
            Assert.That(y, Is.Not.Null);
            Assert.That(new[] { y.OriginX, y.OriginZ, y.NX, y.NZ }, Is.EqualTo(new[] { x.OriginX, x.OriginZ, x.NX, x.NZ }));
            Assert.That(y.Volumes, Is.EqualTo(x.Volumes));
            Assert.That(y.Obstacles().Groups, Is.EqualTo(x.Obstacles().Groups));

            FlightSetup plain = WindTargets.BenchFlight(WindLevel.Severe, 5);
            LogHeader p = Named(LogHeader.For(plain, new FlightCore(plain)));
            Assert.That(LogHeader.Read(new MemoryStream(p.Encode())).Wind.Wakes, Is.Null);
        }

        [Test]
        public void AnOutOfBoundsLayoutIsRefused()
        {
            foreach (Action<WakeLayout> spoil in new Action<WakeLayout>[] { x => x.NX = 0, x => x.NZ = -1, x => x.Volumes = null, x => x.OriginX = double.NaN })
            {
                WindSetup w = WithWakes(WindLevel.Severe);
                spoil(w.Wakes);
                Assert.Throws<ArgumentException>(() => new WindField(w, new FlightRandom(1)));
            }
            WindSetup steady = WindSetup.SteadyFrom(270, 5);
            steady.Wakes = WithWakes(WindLevel.Severe).Wakes;
            Assert.Throws<ArgumentException>(() => new WindField(steady, new FlightRandom(1)), "wakes need a pilot's level");
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
