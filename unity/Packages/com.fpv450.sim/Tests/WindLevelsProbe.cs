using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The pitch gap's budget (p7c-wind 1.5, the physics ledger's gap 3): at clip P's cruise trim (the harness pilot holding
    /// 22.7° nose down in still air), each mechanism's pitching moment about the CoM per unit of a uniform gust or of a gust
    /// gradient, open loop, at the rotors' trimmed speeds (the controller has not answered yet). Explicit: it prints the
    /// design's table.
    /// </summary>
    public sealed class WindLevelsProbe
    {
        const double DegToRad = 0.017453292519943295;

        // The trim: the harness pilot holds clip P's pitch on a course north at 35 m in still air for 20 s.
        static FlightCore Trim(DroneSpec spec, out FlightSetup setup)
        {
            setup = CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Still));
            setup.VortexRing = spec.BuildVortexRing();
            setup.RotorWake = spec.BuildRotorWake();
            setup.Flapping = spec.BuildFlapping();
            setup.Interference = spec.BuildInterference();
            setup.StartPosition = new Double3(0, 35, 0);
            setup.StartVelocity = new Double3(0, 0, WindHarness.ReleaseSpeed);
            DetMath.SinCos(-HarnessPilot.ClipPPitchDeg * DegToRad / 2, out double sp, out double cp);
            setup.StartAttitude = new Quat(sp, 0, 0, cp);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var p = new HarnessPilot(core.Controller) { Course = 0, Height = 35 };
            long release = core.State.Step;
            int frame = 0;
            for (int k = 0; k < 20000; k++)
            {
                if (k * HarnessPilot.CameraHz >= frame * 1000.0) { p.See(HarnessPilot.Take(core.State, k, 0)); frame++; }
                core.Advance(p.Sticks(k, (release + k) * FlightCore.StepUs));
            }
            return core;
        }

        /// <summary>The pitching moment's parts about body +x (N·m) at the state for a wind field (world m/s at a world
        /// point): the rotors' thrust about the CoM, their hub moments, their H-forces, the body's drag, the coil's, the hull's
        /// moment (p7c-wind design 12).</summary>
        public static double[] Parts(FlightCore core, FlightSetup setup, FlightState s, Func<Double3, Double3> wind)
        {
            Propulsion pr = core.Propulsion;
            Double3 com = core.Body.CenterOfMass;
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = wind(s.Position + s.Attitude.Rotate(pr.Hub(i)));
            pr.BeginStep(s, hubs);
            double thrust = 0, hub = 0, h = 0;
            for (int i = 0; i < 4; i++)
            {
                Double3 r = pr.Hub(i) - com;
                double t = pr.Thrust(i), w = s.RotorSpeed[i];
                thrust -= r.Z * t;
                h += Double3.Cross(r, pr.HForce(i, w)).X;
                hub += pr.HubMoment(i, w, t).X;
            }
            Double3 v = s.Attitude.InverseRotate(s.Velocity);
            Double3 Air(Double3 point) => v + Double3.Cross(s.BodyRate, point) - s.Attitude.InverseRotate(wind(s.Position + s.Attitude.Rotate(point)));
            Double3 cpt = core.Aero.Spec.BodyCenterOfPressure;
            double body = Double3.Cross(cpt - com, core.Aero.Body(Air(cpt))).X;
            ref readonly DronePart coil = ref setup.Mass.Part(core.Aero.Spec.CoilPart);
            double coilM = Double3.Cross(coil.Position - com, core.Aero.Coil(Air(coil.Position), coil)).X;
            double hull = new Aero(GoldenFlights.WithTheHull(core.Aero.Spec)).Hull(Air(cpt)).X; // off in flight (D-107): what it would add
            return new[] { thrust, hub, h, body, coilM, hull };
        }

        [Test, Explicit]
        public void TheBudgetAtTheCruise()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightCore core = Trim(spec, out FlightSetup setup);
            FlightState s = core.State;
            TrackerAngles.Of(s.Attitude, out _, out double pitch, out _);
            Double3 com = core.Body.CenterOfMass;
            TestContext.WriteLine($"trim: {s.Velocity.Length():F2} m/s, pitch {pitch / DegToRad:F2}°, CoM {com.X:F4} {com.Y:F4} {com.Z:F4}, Ixx {core.Body.Inertia.XX:F4}");
            for (int i = 0; i < 4; i++)
            {
                Double3 r = core.Propulsion.Hub(i) - com;
                TestContext.WriteLine($"  hub {i}: r {r.X:F3} {r.Y:F3} {r.Z:F3}, {s.RotorSpeed[i]:F0} rad/s");
            }
            string[] names = { "thrust", "hub", "H-force", "body drag", "coil drag", "hull" };
            Double3 at = s.Position;
            Double3 forward = s.Attitude.Rotate(new Double3(0, 0, 1));
            forward = new Double3(forward.X, 0, forward.Z) * (1 / Math.Sqrt(forward.X * forward.X + forward.Z * forward.Z));
            void Row(string label, Func<double, Func<Double3, Double3>> field, double d)
            {
                double[] p = Parts(core, setup, s, field(d)), m = Parts(core, setup, s, field(-d));
                string line = label + ":";
                double sum = 0;
                for (int k = 0; k < p.Length; k++)
                {
                    double g = (p[k] - m[k]) / (2 * d);
                    sum += g;
                    line += $" {names[k]} {g:+0.0000;-0.0000}";
                }
                TestContext.WriteLine(line + $"; sum {sum:+0.0000;-0.0000} N·m per unit");
            }
            Row("updraft w (per m/s)", d => x => new Double3(0, d, 0), 0.5);
            Row("headwind u (per m/s)", d => x => forward * -d, 0.5);
            Row("updraft growing forward dw/dx (per 1/s)", d => x => new Double3(0, d * Double3.Dot(x - at, forward), 0), 0.5);
            Row("headwind growing upward du/dz (per 1/s)", d => x => forward * (-d * (x.Y - at.Y)), 0.5);
            Row("headwind growing forward du/dx (per 1/s)", d => x => forward * (-d * Double3.Dot(x - at, forward)), 0.5);
        }

        /// <summary>
        /// The field's gusts off the forecast's direction (D-086, P33): along the harness's course (the drawn wind abeam from
        /// the left, at the cruise's ground speed) at 35 m over the open field, and held at 0.75 H in the middle of the harness's wooded strip,
        /// Severe, 40 seeds × 10 min sampled at 10 Hz: the share of time the horizontal wind at the drone blows from more than
        /// 45° and 90° off the drawn direction, the gusts (runs of 0.2–10 s) beyond 45°, per minute, and the share of time
        /// the crosswind is from the right (reversed). Explicit.
        /// </summary>
        [Test, Explicit]
        public void GustsOffTheForecast()
        {
            foreach (bool canopy in new[] { false, true })
            {
                double over45 = 0, over90 = 0, reversed = 0, gusts = 0, samples = 0, minutes = 0;
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var drawn = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed));
                    double from = drawn.FromDeg, course = (from + 90) % 360;
                    drawn.Dispose();
                    WindSetup setup = WindSetup.Of(WindLevel.Severe);
                    CanopyStrip strip = WindHarness.Strip(course);
                    if (canopy) setup.Canopies = new[] { strip };
                    var wind = new WindField(setup, new FlightRandom(seed));
                    DetMath.SinCos(course * DegToRad, out double sin, out double cos);
                    DetMath.SinCos(from * DegToRad, out double fs, out double fc);
                    double height = canopy ? 0.75 * WindHarness.StripHeight : 35, run = 0;
                    for (long step = 0; step < 600_000; step += 100)
                    {
                        // Over the field the drone flies the course; in the strip it holds its middle (the eddies blow past).
                        double x = WindHarness.GroundSpeed * step * 0.001;
                        Double3 p = canopy ? new Double3(strip.CentreX, height, strip.CentreZ) : new Double3(sin * x, height, cos * x);
                        wind.Begin(step, p, 0);
                        Double3 w = wind.At(p, 0);
                        // The wind blows toward −(sin, cos) of the bearing it comes from; its own bearing from: atan2 of −w.
                        double speed = Math.Sqrt(w.X * w.X + w.Z * w.Z);
                        double off = speed > 0 ? Math.Acos(Math.Max(-1, Math.Min(1, -(w.X * fs + w.Z * fc) / speed))) / DegToRad : 0;
                        samples++;
                        if (off > 45) { over45++; run++; }
                        else { if (run >= 2 && run <= 100) gusts++; run = 0; }
                        if (off > 90) over90++;
                        // The crosswind from the left of the course: the wind toward the course's right, (cos, −sin).
                        if (w.X * cos - w.Z * sin < 0) reversed++;
                    }
                    minutes += 10;
                    wind.Dispose();
                }
                TestContext.WriteLine($"{(canopy ? "in the strip at 0.75 H" : "open field at 35 m")}: off > 45° {100 * over45 / samples:F2} % of the time, > 90° {100 * over90 / samples:F3} %, "
                    + $"gusts beyond 45° (0.2–10 s) {gusts / minutes:F2} per min, the crosswind reversed {100 * reversed / samples:F3} %");
            }
        }

        // The airframes the attribution flies: the whole one, then each source of pitch taken out in turn.
        static readonly (string Name, bool Steady, Action<FlightSetup> Change)[] Variants =
        {
            ("whole", false, null),
            ("steady", true, null),
            ("noownwake", false, s => s.RotorWake = null),
            ("noasymmetry", false, s => s.Asymmetry = null),
            ("nointerference", false, s => s.Interference = null),
            ("noflapping", false, s => s.Flapping = null),
            ("steady-noownwake", true, s => s.RotorWake = null),
            ("steady-noownwake-noasym", true, s => { s.RotorWake = null; s.Asymmetry = null; }),
            ("hull", false, s => s.Aero = GoldenFlights.WithTheHull(s.Aero)), // p7c-wind design 12, off by default (D-107)
            ("steady-hull", true, s => s.Aero = GoldenFlights.WithTheHull(s.Aero)),
            ("potential", false, s => { s.Aero = GoldenFlights.WithTheHull(s.Aero); s.Aero.HullPotential = true; }), // its known-wrong
        };

        /// <summary>
        /// Where the harness's pitch comes from: Severe's nominal runs on seeds 1–N (PITCH_PROBE_SEEDS, default 5) with each
        /// source taken out, written as the harness's CSVs under PITCH_PROBE_OUT/&lt;variant&gt;/ for
        /// tools/wind/pitch_probe.py. Explicit.
        /// </summary>
        [Test, Explicit]
        public void TheAttribution()
        {
            string root = Environment.GetEnvironmentVariable("PITCH_PROBE_OUT");
            Assume.That(root, Is.Not.Null.And.Not.Empty, "set PITCH_PROBE_OUT");
            int n = int.TryParse(Environment.GetEnvironmentVariable("PITCH_PROBE_SEEDS"), out int k) ? k : 5;
            DroneSpec spec = RepositoryDrone.Spec();
            var jobs = new System.Collections.Generic.List<(string, int)>();
            foreach (var v in Variants) for (int i = 0; i < n; i++) jobs.Add((v.Name, i));
            System.Threading.Tasks.Parallel.ForEach(jobs, job =>
            {
                var v = Array.Find(Variants, x => x.Name == job.Item1);
                WindHarness.Run r = WindHarness.Fly(spec, WindLevel.Severe, (ulong)(job.Item2 + 1), job.Item2, n, WindHarness.Pilots[0],
                    belt: !v.Steady, steady: v.Steady, airframe: v.Change);
                string dir = System.IO.Path.Combine(root, v.Name);
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"seed-{r.Seed}.csv"), WindHarness.Csv(r));
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"seed-{r.Seed}.segment"), r.Segment ?? "");
            });
        }
    }
}
