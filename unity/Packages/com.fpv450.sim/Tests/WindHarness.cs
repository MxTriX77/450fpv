using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// K§8.5's acceptance harness (wind.md §6.2; p7c-wind 1.2, design 2): HarnessPilot flies the test flight and the
    /// camera's frames are logged in `track_attitude.py`'s columns at 29.917 Hz, the input `wind_stats.py` reads.
    /// - **The flight:** the repository's drone at its nominal loadout, released 16 steps after arming at
    ///   `ReleaseSpeed` along the course, nose down at the pilot's θ_ref, level, at the run's height. The course is
    ///   laid with the seed's drawn wind abeam from the left (bearing = the wind's + 90°), straight, `Seconds` long:
    ///   `BeltSeconds` from the belt's upwind edge to 10 belt heights downwind, the rest over open field.
    /// - **The belt** (p7c-wind 1.5): a row of trees (BeltLayout) whose wakes the field takes from 1.3's wake grid, until
    ///   the test range's world lands (D-046). It crosses the course `BeltStartSeconds` of flight past the release at
    ///   `BeltAngleDeg` to it, running forward on the upwind side, so that with the drawn wind abeam the course runs from
    ///   the belt's upwind edge to 10 belt heights downwind of it in `BeltSeconds` (K§8.5). The segment is where the
    ///   camera's frames are on that stretch of the course, from their positions; the wind's own direction moves the wake.
    /// - **The measurement:** the frames from `DiscardSeconds` on, numbered from 1 with time_s = (frame − 1)/29.917
    ///   (the tracker's convention), roll and pitch from TrackerAngles, the yaw rate the frame-to-frame change of the
    ///   heading × 29.917, every confidence 1, dup 0, flag ok.
    /// - **The start:** each run starts where the seed's prevailing wind stays within 90° of its drawn direction for the
    ///   whole run (StartSecond). The stand-in's direction never moves, so that is always its start, 0.
    /// </summary>
    public static class WindHarness
    {
        /// <summary>The run, the discarded start and the belt, s (K§8.5: 32 s of field and 7 s of belt, the first 5 s
        /// discarded; the belt 4 s into the measured part, as P's began at f121, assumed).</summary>
        public const double Seconds = 39, DiscardSeconds = 5, BeltStartSeconds = 9, BeltSeconds = 7;

        /// <summary>The release speed along the course, m/s (derived, p7c-wind 1.6: the airspeed clip P's 22.7° of pitch
        /// settles at in still air, HeldPitchAirspeed, 18.82 m/s; PHY-17a's published cruise is 17–22).</summary>
        public const double ReleaseSpeed = 18.8;

        /// <summary>The runs' heights spread evenly over this band, m above the floor (assumed, wind.md §6.2).</summary>
        public const double LowHeight = 20, HighHeight = 50;

        /// <summary>The belt's angle to the course, degrees (assumed: K§8.5's belt crossing with the wind abeam needs it
        /// oblique; at 45° the course gains a metre downwind of the belt for every metre along).</summary>
        public const double BeltAngleDeg = 45;

        /// <summary>The belt's stretch of the course in belt heights past its leeward edge (K§8.5).</summary>
        public const double BeltHeights = 10;

        /// <summary>The belt's trees: the stand-in maple's wind volume (Z09_tree_maple, WakeGridFixture: a crown of radius
        /// 3.1 m from 2.2 to 8.4 m, porosity 0.45, Godot's) scaled to the belt's height, every 5 m scaled.</summary>
        public const double MapleTop = WakeGridFixture.MapleTop, CrownRatio = WakeGridFixture.MapleRadius / MapleTop,
            BaseRatio = WakeGridFixture.MapleBase / MapleTop, SpacingRatio = WakeGridFixture.TreeSpacing / MapleTop;

        /// <summary>The trees' porosity (assumed: the stand-in maple's, Godot's 0.45; 1.5 judges it against T3).</summary>
        public static double BeltPorosity = WakeGridFixture.MaplePorosity;

        /// <summary>The belt's height H, m (derived: from its upwind edge to 10 H past its leeward edge in BeltSeconds at
        /// ReleaseSpeed along the course at BeltAngleDeg: 7·18.8 = H·(10 + 2·CrownRatio/sin 45°), 11.9 m; 1.2's region was
        /// 13.2 m at the cruise and 9.1 m at 13 m/s).</summary>
        public static readonly double BeltHeight = BeltSeconds * ReleaseSpeed / (BeltHeights + 2 * CrownRatio / System.Math.Sin(BeltAngleDeg * DegToRad));

        /// <summary>A pilot: the nominal one or a variant (wind.md §6.1).</summary>
        public sealed class Pilot
        {
            public string Name;
            public double Delay = 0.10, TauV = 0.5, Split = 0.64;
        }

        /// <summary>The nominal pilot and its variants, flown on the same seeds; s = 1 and s = 0 are for T11b only.</summary>
        public static readonly Pilot[] Pilots =
        {
            new Pilot { Name = "nominal" },
            new Pilot { Name = "d0", Delay = 0 },
            new Pilot { Name = "d0.2", Delay = 0.2 },
            new Pilot { Name = "tv0.3", TauV = 0.3 },
            new Pilot { Name = "tv1.0", TauV = 1.0 },
            new Pilot { Name = "s1", Split = 1 },
            new Pilot { Name = "s0", Split = 0 },
        };

        /// <summary>One logged camera frame, and the flight's truth beside it (for the run's summary, never the CSV).</summary>
        public struct Sample
        {
            public int Frame;
            public double Time, RollDeg, PitchDeg, YawRateDps;
            public bool InBelt;
            public double Crosswind, Airspeed, Height;
        }

        /// <summary>One run: its setup, its frames and its summary.</summary>
        public sealed class Run
        {
            public WindLevel Level;
            public ulong Seed;
            public string Pilot;
            public bool Belt, Steady;
            public double HeightRef, FromDeg, MeanSpeed, CourseDeg, Mass, StartSecond;

            /// <summary>The body's inertia about its roll, pitch and yaw axes, kg·m² (body z, x, y).</summary>
            public double RollInertia, PitchInertia, YawInertia;
            public Sample[] Samples;

            /// <summary>The belt's frames, first and last (0 when none).</summary>
            public int BeltFirst, BeltLast;

            /// <summary>Over the logged frames: the mean roll and pitch, degrees; the mean crosswind at the body from the
            /// left, m/s; the mean airspeed, m/s; the lean per m/s of crosswind into it, degrees; the mean height error
            /// of the 2 s loop, m.</summary>
            public double MeanRollDeg, MeanPitchDeg, MeanCrosswind, MeanAirspeed, LeanPerMs, MeanHeightError;

            /// <summary>wind_stats.py's --segment argument for the belt, or null.</summary>
            public string Segment => BeltLast > 0 ? BeltFirst + "-" + BeltLast : null;
        }

        /// <summary>
        /// p6-flight-core 1.5's own crosswind lean at the release speed (CrosswindScenario.CruiseLean, the nose on the track
        /// in a steady crosswind), per m/s of a crosswind, as the tracker's roll: 1.5 measures the body's up axis in the
        /// world's vertical plane across the track, which at a pitch θ is tan φ = tan(lean)·cos θ. Reporting only
        /// (System.Math): it never reaches a flight.
        /// </summary>
        public static double CruiseRollPerMs(DroneSpec spec, double crosswind)
        {
            double cos = System.Math.Cos(HarnessPilot.ClipPPitchDeg * DegToRad);
            double Roll(double c) => System.Math.Atan(System.Math.Tan(CrosswindScenario.CruiseLean(spec, ReleaseSpeed, c) * DegToRad) * cos) * RadToDeg;
            return (Roll(crosswind) - Roll(0)) / crosswind;
        }

        /// <summary>The harness's lean check: within `tolerance` of 1.5's own at the release speed (p7c-wind 1.6: the lean
        /// per m/s follows from the physics at the cruise and is graded by T0, not by K§6.5's band at 13 m/s).</summary>
        public static bool Reproduces(double perMs, double reference, double tolerance) =>
            perMs > 0 && System.Math.Abs(perMs / reference - 1) <= tolerance;

        /// <summary>
        /// wind-turbulence's "Clip P's pitch flies the cruise" (p7c-wind 1.6, PHY-17a): the nominal pilot holds clip P's
        /// pitch, a level heading north and 35 m in still air, released at `releaseSpeed`; the airspeed it settles at, the
        /// mean over the run's last `MeasuredSeconds`, and the mean pitch there (the tracker's, degrees). `hForce` ≥ 0
        /// replaces drone.json's rotor in-plane drag, N per m/s (a known-wrong model).
        /// </summary>
        public static double HeldPitchAirspeed(DroneSpec spec, double releaseSpeed, out double pitchDeg, double hForce = -1)
        {
            const double height = 35;
            FlightSetup setup = CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Still));
            if (hForce >= 0) setup.Propulsion.Props.HForce = hForce; // a known-wrong rotor drag
            setup.StartPosition = new Double3(0, height, 0);
            setup.StartVelocity = new Double3(0, 0, releaseSpeed);
            DetMath.SinCos(-HarnessPilot.ClipPPitchDeg * DegToRad / 2, out double sp, out double cp);
            setup.StartAttitude = new Quat(sp, 0, 0, cp);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var p = new HarnessPilot(core.Controller) { Course = 0, Height = height };
            long release = core.State.Step;
            int steps = (int)(Seconds * 1000), from = (int)((Seconds - MeasuredSeconds) * 1000), frame = 0, n = 0;
            double speed = 0;
            pitchDeg = 0;
            for (int k = 0; k < steps; k++)
            {
                if (k * HarnessPilot.CameraHz >= frame * 1000.0)
                {
                    HarnessPilot.Frame f = HarnessPilot.Take(core.State, k, 0);
                    p.See(f);
                    frame++;
                    if (k >= from)
                    {
                        speed += core.State.Velocity.Length();
                        pitchDeg += f.Pitch * RadToDeg;
                        n++;
                    }
                }
                core.Advance(p.Sticks(k, (release + k) * FlightCore.StepUs));
            }
            pitchDeg /= n;
            return speed / n;
        }

        /// <summary>The held-pitch flight's measured end, s.</summary>
        public const double MeasuredSeconds = 10;

        /// <summary>The height of run i of n, m: evenly over the band.</summary>
        public static double HeightOf(int i, int n) => LowHeight + (HighHeight - LowHeight) * (i + 0.5) / n;

        /// <summary>
        /// The start second of a run of `seconds` in a prevailing-direction history sampled at 1 Hz: the first where
        /// every sample through the run stays within 90° of the drawn direction, or −1 for none.
        /// </summary>
        public static int StartSecond(double[] fromDeg, double drawnDeg, double seconds)
        {
            int span = (int)seconds + 1;
            for (int start = 0; start + span <= fromDeg.Length; start++)
            {
                bool kept = true;
                for (int i = start; i < start + span && kept; i++)
                {
                    double off = ((fromDeg[i] - drawnDeg) % 360 + 540) % 360 - 180;
                    kept = off > -90 && off < 90;
                }
                if (kept) return start;
            }
            return -1;
        }

        /// <summary>
        /// The direction a seed's wind blows from at second t of its history, degrees: the preset's own wind state before
        /// turbulence and wakes (K§8.5's T0c history), 1.4's field's meander and shifts about its drawn direction.
        /// </summary>
        public static double PrevailingFromDeg(WindField wind, int t) => wind.FromDegAt(t);

        /// <summary>A seed's prevailing-direction history at 1 Hz, `minutes` long.</summary>
        public static double[] History(WindLevel level, ulong seed, double minutes, out double drawnDeg)
        {
            var wind = new WindField(WindSetup.Of(level), new FlightRandom(seed));
            drawnDeg = wind.FromDeg;
            var h = new double[(int)(minutes * 60) + 1];
            for (int i = 0; i < h.Length; i++) h[i] = PrevailingFromDeg(wind, i);
            return h;
        }

        /// <summary>
        /// The belt's trees and their grid for a course from the origin along `along` (a horizontal unit vector), the wind
        /// abeam from its left: the belt's centre line crosses the course BeltStartSeconds·ReleaseSpeed ahead and runs
        /// forward on the upwind side at BeltAngleDeg, from 4 spacings behind the crossing to where the course leaves the
        /// belt's 10 H, so its far wake beyond falls behind the drone (assumed). The grid covers the trees and the
        /// course's stretch with 40 m about them (the wakes need nothing upwind of the trees).
        /// </summary>
        public static WakeLayout BeltLayout(in Double3 along)
        {
            double h = BeltHeight, r = CrownRatio * h, spacing = SpacingRatio * h, gamma = BeltAngleDeg * DegToRad;
            var left = new Double3(-along.Z, 0, along.X);
            DetMath.SinCos(gamma, out double sg, out double cg);
            Double3 d = along * cg + left * sg, crossing = along * (BeltStartSeconds * ReleaseSpeed);
            double end = (BeltHeights * h + 2 * r / sg) / sg + spacing;
            var volumes = new List<WindVolume>();
            double x0 = double.MaxValue, x1 = double.MinValue, z0 = double.MaxValue, z1 = double.MinValue;
            void Cover(in Double3 q)
            {
                x0 = System.Math.Min(x0, q.X); x1 = System.Math.Max(x1, q.X);
                z0 = System.Math.Min(z0, q.Z); z1 = System.Math.Max(z1, q.Z);
            }
            for (double t = -4 * spacing; t <= end; t += spacing)
            {
                Double3 q = crossing + d * t;
                volumes.Add(new WindVolume
                {
                    Kind = WindVolumeKind.Cylinder, X = q.X, Z = q.Z, BaseM = BaseRatio * h, TopM = h, Radius = r, Porosity = BeltPorosity,
                });
                Cover(q);
            }
            Cover(crossing - along * (2 * r));
            Cover(crossing + along * (BeltSeconds * ReleaseSpeed + 2 * r));
            const double margin = 40;
            double ox = WakeObstacles.Cell * System.Math.Floor((x0 - margin) / WakeObstacles.Cell);
            double oz = WakeObstacles.Cell * System.Math.Floor((z0 - margin) / WakeObstacles.Cell);
            return new WakeLayout
            {
                OriginX = ox, OriginZ = oz, NX = (int)System.Math.Ceiling((x1 + margin - ox) / WakeObstacles.Cell),
                NZ = (int)System.Math.Ceiling((z1 + margin - oz) / WakeObstacles.Cell), Volumes = volumes.ToArray(),
            };
        }

        /// <summary>The belt's stretch of the course, m from the release: from its upwind edge to 10 H past its leeward
        /// edge, with the wind abeam.</summary>
        public static void BeltStretch(out double from, out double to)
        {
            double sg = System.Math.Sin(BeltAngleDeg * DegToRad), r = CrownRatio * BeltHeight;
            from = BeltStartSeconds * ReleaseSpeed - r / sg;
            to = BeltStartSeconds * ReleaseSpeed + r / sg + BeltHeights * BeltHeight;
        }

        /// <summary>The test flight's setup for a seed at a height: its course from the seed's drawn wind, and the belt.
        /// `steady` is the known-wrong fixed vector: the drawn strength from the drawn direction at every point and time,
        /// without turbulence, profile, direction changes or wakes.</summary>
        public static FlightSetup Setup(DroneSpec spec, WindLevel level, ulong seed, double height, bool belt, bool drag,
            out double courseDeg, out double fromDeg, int startSecond = 0, bool steady = false)
        {
            var drawn = new WindField(WindSetup.Of(level), new FlightRandom(seed));
            fromDeg = drawn.FromDeg;
            courseDeg = (fromDeg + 90) % 360; // the wind abeam from the left
            double course = courseDeg * DegToRad;
            DetMath.SinCos(course, out double sin, out double cos);
            var along = new Double3(sin, 0, cos);
            WindSetup wind = WindSetup.Of(level);
            wind.StartSecond = startSecond;
            if (belt) wind.Wakes = BeltLayout(along);
            if (steady) wind = WindSetup.SteadyFrom(fromDeg, drawn.MeanSpeed);
            FlightSetup setup = CrosswindScenario.Setup(spec, wind, seed, drag, drag);
            setup.StartPosition = new Double3(0, height, 0);
            setup.StartVelocity = along * ReleaseSpeed;
            // Yaw onto the course (about +Y, the heading grows), then the nose down (about body +x).
            DetMath.SinCos(course / 2, out double sy, out double cy);
            DetMath.SinCos(-HarnessPilot.ClipPPitchDeg * DegToRad / 2, out double sp, out double cp);
            setup.StartAttitude = new Quat(0, sy, 0, cy) * new Quat(sp, 0, 0, cp);
            return setup;
        }

        const double DegToRad = 0.017453292519943295, RadToDeg = 57.29577951308232;

        /// <summary>Flies run i of n for a seed with a pilot, and logs it.</summary>
        public static Run Fly(DroneSpec spec, WindLevel level, ulong seed, int i, int n, Pilot pilot, bool belt = true, bool drag = true,
            bool steady = false)
        {
            double height = HeightOf(i, n);
            double[] history = History(level, seed, 120, out double drawn);
            int start = StartSecond(history, drawn, Seconds);
            if (start < 0)
                throw new System.InvalidOperationException("seed " + seed + "'s 120 min hold no " + Seconds + " s within 90° of its drawn direction");
            FlightSetup setup = Setup(spec, level, seed, height, belt && !steady, drag, out double courseDeg, out double fromDeg,
                steady ? 0 : start, steady);

            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            double course = courseDeg * DegToRad;
            var p = new HarnessPilot(core.Controller)
            {
                Delay = pilot.Delay, TauV = pilot.TauV, Split = pilot.Split, Course = course, Height = height,
            };
            DetMath.SinCos(course, out double sin, out double cos);
            var along = new Double3(sin, 0, cos);
            var right = new Double3(cos, 0, -sin);
            BeltStretch(out double beltFrom, out double beltTo);

            var run = new Run
            {
                Level = level, Seed = seed, Pilot = pilot.Name, Belt = belt, Steady = steady, HeightRef = height, FromDeg = fromDeg,
                MeanSpeed = core.Wind.MeanSpeed, CourseDeg = courseDeg, Mass = core.Body.Mass, StartSecond = start,
                RollInertia = core.Body.Inertia.ZZ, PitchInertia = core.Body.Inertia.XX, YawInertia = core.Body.Inertia.YY,
            };
            var samples = new List<Sample>();
            long release = core.State.Step;
            int steps = (int)(Seconds * 1000), frame = 0;
            double lastHeading = 0;
            for (int k = 0; k < steps; k++)
            {
                if (k * HarnessPilot.CameraHz >= frame * 1000.0)
                {
                    ref readonly FlightState s = ref core.State;
                    HarnessPilot.Frame f = HarnessPilot.Take(s, k, course);
                    p.See(f);
                    double time = frame / HarnessPilot.CameraHz;
                    if (time >= DiscardSeconds)
                    {
                        Double3 wind = core.Wind.At(s.Position), air = s.Velocity - wind;
                        double x = Double3.Dot(s.Position - setup.StartPosition, along);
                        samples.Add(new Sample
                        {
                            Frame = samples.Count + 1, Time = samples.Count / HarnessPilot.CameraHz,
                            RollDeg = f.Roll * RadToDeg, PitchDeg = f.Pitch * RadToDeg,
                            YawRateDps = TrackerAngles.Wrap(f.Heading - lastHeading) * HarnessPilot.CameraHz * RadToDeg,
                            InBelt = x >= beltFrom && x <= beltTo, Crosswind = Double3.Dot(wind, right),
                            Airspeed = air.Length(), Height = s.Position.Y,
                        });
                    }
                    lastHeading = f.Heading;
                    frame++;
                }
                core.Advance(p.Sticks(k, (release + k) * FlightCore.StepUs));
            }
            core.Wind.Dispose();
            run.Samples = samples.ToArray();
            foreach (Sample s in run.Samples)
            {
                run.MeanRollDeg += s.RollDeg;
                run.MeanPitchDeg += s.PitchDeg;
                run.MeanCrosswind += s.Crosswind;
                run.MeanAirspeed += s.Airspeed;
                run.MeanHeightError += System.Math.Abs(s.Height - height);
                if (s.InBelt && run.BeltFirst == 0) run.BeltFirst = s.Frame;
                if (s.InBelt) run.BeltLast = s.Frame;
            }
            int count = run.Samples.Length;
            run.MeanRollDeg /= count;
            run.MeanPitchDeg /= count;
            run.MeanCrosswind /= count;
            run.MeanAirspeed /= count;
            run.MeanHeightError /= count;
            run.LeanPerMs = run.MeanCrosswind > 0 ? -run.MeanRollDeg / run.MeanCrosswind : 0;
            return run;
        }

        /// <summary>`track_attitude.py`'s columns, in its order (godot-final, COLUMNS).</summary>
        public static readonly string[] Columns =
        {
            "frame", "time_s", "roll_deg", "pitch_deg", "yaw_rate_dps", "conf_roll", "conf_pitch", "conf_yaw", "dup", "n_cols",
            "inlier_frac", "contrast", "span", "resid_px", "edge_px", "yaw_peak", "yaw_unique", "yaw_del_deg", "flag",
        };

        /// <summary>
        /// The run's frames as an attitude CSV in the tracker's columns (wind.md §6.2 step 4): every confidence 1, dup 0,
        /// flag ok, the tracker's own diagnostics empty (a simulation has none), "\n" line ends.
        /// </summary>
        public static string Csv(Run run)
        {
            var b = new StringBuilder();
            b.Append(string.Join(",", Columns)).Append('\n');
            foreach (Sample s in run.Samples)
            {
                b.Append(s.Frame.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(F(s.Time, "0.000000")).Append(',')
                    .Append(F(s.RollDeg, "0.0000")).Append(',')
                    .Append(F(s.PitchDeg, "0.0000")).Append(',')
                    .Append(F(s.YawRateDps, "0.000")).Append(',')
                    .Append("1,1,1,0,,,,,,,,,,ok\n");
            }
            return b.ToString();
        }

        /// <summary>A seed's T0c history as wind_stats.py's --wind-log reads it: time_s, wind_from_deg, prevailing_from_deg.</summary>
        public static string HistoryCsv(double[] fromDeg, double drawnDeg)
        {
            var b = new StringBuilder("time_s,wind_from_deg,prevailing_from_deg\n");
            for (int i = 0; i < fromDeg.Length; i++)
                b.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',').Append(F((fromDeg[i] % 360 + 360) % 360, "0.000")).Append(',')
                    .Append(F(drawnDeg, "0.000")).Append('\n');
            return b.ToString();
        }

        static string F(double x, string format) => x.ToString(format, CultureInfo.InvariantCulture);
    }
}
