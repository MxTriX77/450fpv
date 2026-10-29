using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// K§8.5's acceptance harness (wind.md §6.2; p7c-wind 1.2, design 2): HarnessPilot flies the test flight and the
    /// camera's frames are logged in `track_attitude.py`'s columns at 29.917 Hz, the input `wind_stats.py` reads.
    /// - **The flight:** the repository's drone at its nominal loadout, released 16 steps after arming at
    ///   `ReleaseSpeed` along the course, nose down at the pilot's θ_ref, level, at the run's height. The course is
    ///   laid with the seed's drawn wind abeam from the left (bearing = the wind's + 90°), straight, `Seconds` long:
    ///   `BeltSeconds` from the belt's upwind edge to 10 belt heights downwind, the rest over open field.
    /// - **The belt:** until p7c-wind 1.3's wake grid, the stand-in's labelled synthetic belt (SyntheticBelt) across the
    ///   course `BeltStartSeconds` of flight past the release, 10 heights long with H = BeltSeconds·ReleaseSpeed/10.
    ///   The segment is where the camera's frames are inside that region, from their positions.
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

        /// <summary>The release speed along the course, m/s (derived: p6-flight-core 1.5 trims clip P's 22.7° of pitch at
        /// 13 m/s in still air; p7c-wind 1.6 moves it to the published cruise).</summary>
        public const double ReleaseSpeed = 13;

        /// <summary>The runs' heights spread evenly over this band, m above the floor (assumed, wind.md §6.2).</summary>
        public const double LowHeight = 20, HighHeight = 50;

        /// <summary>The synthetic belt's height H, m (derived: 10 H in BeltSeconds at ReleaseSpeed, 9.1 m, a field
        /// shelterbelt's).</summary>
        public const double BeltHeight = BeltSeconds * ReleaseSpeed / SyntheticBelt.RegionHeights;

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
            public bool Belt;
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

        /// <summary>The harness's lean check: K§6.5's band, 0.95–2.6° per m/s, and within `tolerance` of 1.5's own.</summary>
        public static bool Reproduces(double perMs, double reference, double tolerance) =>
            perMs >= 0.95 && perMs <= 2.6 && System.Math.Abs(perMs / reference - 1) <= tolerance;

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
        /// The prevailing direction a seed's wind blows from at time t, degrees: the preset's own wind state before
        /// turbulence and wakes (K§8.5's T0c history). The stand-in's is its draw, fixed; p7c-wind 1.4's field moves it.
        /// </summary>
        public static double PrevailingFromDeg(StandInWind wind, double t) => wind.FromDeg;

        /// <summary>A seed's prevailing-direction history at 1 Hz, `minutes` long.</summary>
        public static double[] History(WindLevel level, ulong seed, double minutes, out double drawnDeg)
        {
            var wind = new StandInWind(WindSetup.Of(level), new FlightRandom(seed));
            drawnDeg = wind.FromDeg;
            var h = new double[(int)(minutes * 60) + 1];
            for (int i = 0; i < h.Length; i++) h[i] = PrevailingFromDeg(wind, i);
            return h;
        }

        /// <summary>The test flight's setup for a seed at a height: its course from the seed's drawn wind, and the belt.</summary>
        public static FlightSetup Setup(DroneSpec spec, WindLevel level, ulong seed, double height, bool belt, bool drag,
            out double courseDeg, out double fromDeg)
        {
            fromDeg = new StandInWind(WindSetup.Of(level), new FlightRandom(seed)).FromDeg;
            courseDeg = (fromDeg + 90) % 360; // the wind abeam from the left
            double course = courseDeg * DegToRad;
            DetMath.SinCos(course, out double sin, out double cos);
            var along = new Double3(sin, 0, cos);
            WindSetup wind = WindSetup.Of(level);
            if (belt)
                wind.Belt = new SyntheticBelt
                {
                    Edge = along * (BeltStartSeconds * ReleaseSpeed), Across = along, Height = BeltHeight,
                };
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
        public static Run Fly(DroneSpec spec, WindLevel level, ulong seed, int i, int n, Pilot pilot, bool belt = true, bool drag = true)
        {
            double height = HeightOf(i, n);
            FlightSetup setup = Setup(spec, level, seed, height, belt, drag, out double courseDeg, out double fromDeg);
            double[] history = History(level, seed, Seconds / 60, out double drawn);
            int start = StartSecond(history, drawn, Seconds);
            if (start != 0)
                throw new System.InvalidOperationException("the stand-in's field cannot start a run late (p7c-wind 1.4's field takes the start)");

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
            double beltFrom = BeltStartSeconds * ReleaseSpeed, beltTo = beltFrom + SyntheticBelt.RegionHeights * BeltHeight;

            var run = new Run
            {
                Level = level, Seed = seed, Pilot = pilot.Name, Belt = belt, HeightRef = height, FromDeg = fromDeg,
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
                        Double3 wind = core.Wind.At(s.Gust, s.Position), air = s.Velocity - wind;
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
                b.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',').Append(F(fromDeg[i], "0.000")).Append(',')
                    .Append(F(drawnDeg, "0.000")).Append('\n');
            return b.ToString();
        }

        static string F(double x, string format) => x.ToString(format, CultureInfo.InvariantCulture);
    }
}
