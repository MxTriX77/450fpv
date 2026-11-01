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
    /// - **The flight:** the repository's drone at its nominal loadout with its whole airframe (p7b-airframe: the motors'
    ///   asymmetry on the seed's draws, the vortex rings, the ground effect, the rotors' flapping and the airframe's modes,
    ///   as a pilot's flight has them), released 16 steps after arming at
    ///   `ReleaseSpeed` along the course, nose down at the pilot's θ_ref, level, at the run's height. The course is
    ///   laid with the seed's drawn wind abeam from the left (bearing = the wind's + 90°), straight, `Seconds` long:
    ///   the belt stretch's `BeltSeconds` through a wooded strip, the rest over open field.
    /// - **The belt** (p7c-wind 1.5, belt-geometry-p.md: clip P crossed a wide wooded strip, the wind along it, at
    ///   0.45–1.0 H inside its canopy layer, not a belt's wake at 20–50 m): a canopy strip (Strip, WindField's canopy
    ///   layer) across the course, its long axis along the drawn wind, until the test range's world lands (D-046). The
    ///   stretch is P's set B: from ApproachHeights before the strip's near edge, StretchHeights of ground along the
    ///   course, at the run's belt height (BeltHeightOf, 0.5–1.0 H). The segment is where the camera's frames are on that
    ///   stretch, from their positions.
    /// - **The heights:** released at the belt height, the drone flies the field before the strip and the stretch there;
    ///   past the stretch the height to hold climbs at ClimbRate to the run's field height (HeightOf, K§8.5's 20–50 m),
    ///   and the climb's frames, until SettleSeconds after it ends, are logged unusable (confidence 0, as the tracker's
    ///   lost frames): they belong to neither stretch. The run is that much longer than `Seconds`.
    /// - **The measurement:** the frames from `DiscardSeconds` on, numbered from 1 with time_s = (frame − 1)/29.917
    ///   (the tracker's convention), roll and pitch from TrackerAngles, the yaw rate the frame-to-frame change of the
    ///   heading × 29.917, every confidence 1, dup 0, flag ok. Each logged frame shows the camera at the recorder's
    ///   field pick (FieldStep, D-060), as clip P's recording does, not at the tick's own instant; the pilot sees the
    ///   tick's (K§8.5).
    /// - **The start:** each run starts where the seed's prevailing wind stays within 90° of its drawn direction for the
    ///   whole run (StartSecond).
    /// </summary>
    public static class WindHarness
    {
        /// <summary>The run, the discarded start and the belt, s (K§8.5: 32 s of field and 7 s of belt, the first 5 s
        /// discarded; the belt stretch begins 4 s into the measured part, as P's set B began at f121, assumed), without
        /// the climb between the stretches (ClimbSecondsOf).</summary>
        public const double Seconds = 39, DiscardSeconds = 5, BeltStartSeconds = 9, BeltSeconds = 7;

        /// <summary>The release speed along the course, m/s (derived, p7c-wind 1.6: the airspeed clip P's 22.7° of pitch
        /// settles at in still air, HeldPitchAirspeed: 20.57 m/s on D-050's silhouette, 18.82 on D-043's; PHY-17a's published
        /// cruise is 17–22).</summary>
        public const double ReleaseSpeed = 20.6;

        /// <summary>The runs' field heights spread evenly over this band, m above the floor (assumed, wind.md §6.2;
        /// belt-geometry-p.md did not measure P's field stretch).</summary>
        public const double LowHeight = 20, HighHeight = 50;

        /// <summary>The runs' belt heights spread evenly over this band, in strip heights (derived, belt-geometry-p.md: P
        /// flew set B at 0.45 H over the strip's edge rising to 1.0 H, inside its canopy layer).</summary>
        public const double LowBelt = 0.5, HighBelt = 1.0;

        /// <summary>The course's ground speed, m/s (derived: the cruise through the air with Severe's centre abeam,
        /// √(V² − U²) at the release speed V and Severe's centre U; belt-geometry-p.md assumed 17, 12–21).</summary>
        public static readonly double GroundSpeed = System.Math.Sqrt(ReleaseSpeed * ReleaseSpeed - WindPreset.SevereCentre * WindPreset.SevereCentre);

        /// <summary>The belt stretch's ground and its part before the strip's near edge, in strip heights (derived,
        /// belt-geometry-p.md: set B covers about 7.3 H, from 2.2 H before the near edge; 2 H here, the note's course).</summary>
        public const double StretchHeights = 7.3, ApproachHeights = 2;

        /// <summary>The strip's height H, m (derived: P's set B covers StretchHeights in BeltSeconds, so H = 7 s · GroundSpeed
        /// / 7.3 at the harness's ground speed; belt-geometry-p.md's 15 m, 11–21, at its assumed 17 m/s).</summary>
        public static readonly double StripHeight = BeltSeconds * GroundSpeed / StretchHeights;

        /// <summary>The strip's width along the course and its length along the wind, in strip heights (the first derived,
        /// belt-geometry-p.md: about 8.5 H from the field's edge to the tall-tree line; the second assumed: long, P's
        /// strip runs out of the frame both ways).</summary>
        public const double StripWidthHeights = 8.5, StripLengthHeights = 60;

        /// <summary>The strip's frontal area index λ (assumed: loose scrub with scattered tall trees, at Raupach 1994's
        /// u*/U(h) plateau of 0.3, reached from λ = 0.29).</summary>
        public const double StripAreaIndex = 0.3;

        /// <summary>The climb from the belt height to the field height, m/s, and the height loop's settling after it, s
        /// (assumed: a gentle cruise climb; twice HarnessPilot's 2 s height loop).</summary>
        public const double ClimbRate = 3, SettleSeconds = 4;

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
            public bool InBelt, Usable;
            public double Crosswind, Airspeed, Height, Held;
        }

        /// <summary>One run: its setup, its frames and its summary.</summary>
        public sealed class Run
        {
            public WindLevel Level;
            public ulong Seed;
            public string Pilot;
            public bool Belt, Steady;
            public double HeightRef, BeltHeightRef, ClimbSeconds, FromDeg, MeanSpeed, CourseDeg, Mass, StartSecond;

            /// <summary>The body's inertia about its roll, pitch and yaw axes, kg·m² (body z, x, y).</summary>
            public double RollInertia, PitchInertia, YawInertia;
            public Sample[] Samples;

            /// <summary>The belt's frames, first and last (0 when none).</summary>
            public int BeltFirst, BeltLast;

            /// <summary>Over the usable frames: the mean roll and pitch, degrees; the mean crosswind at the body from the
            /// left, m/s; the mean airspeed, m/s; the lean per m/s of crosswind into it, degrees; the mean height error
            /// of the 2 s loop from the height it holds, m.</summary>
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
            Airframe(spec, setup);
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

        /// <summary>The PAL field's period, ms, and its horizon row's instant after the field's start, ms (published, PAL
        /// timing: (25 + 60) lines × 64 µs, the horizon in the picture's top fifth, wind.md §1.1).</summary>
        public const double FieldMs = 20, HorizonMs = 5.44;

        /// <summary>
        /// The step (from the release) whose camera a logged frame shows: the recorder writes the newest whole field at each
        /// of its 29.917 Hz ticks (video-feed.md §0, hover-micro-motion.md §2.3), so frame f, ticked at f/29.917 s, shows
        /// field ⌊t/20 ms⌋ − 1 at its horizon row. Its instant lags the tick by 14.6–34.6 ms, uniform over a field (σ 5.8 ms,
        /// derived): the timing jitter the tracker's uniform clock reads as motion (0.018 / 0.012 / 0.020° over 4–15 Hz in
        /// P's roll, pitch and heading, derived). Before the first whole field, the release.
        /// </summary>
        public static long FieldStep(int frame)
        {
            long field = (long)(frame * 1000 / HarnessPilot.CameraHz / FieldMs) - 1;
            return field < 0 ? 0 : (long)(field * FieldMs + HorizonMs);
        }

        /// <summary>The field height of run i of n, m: evenly over the band.</summary>
        public static double HeightOf(int i, int n) => LowHeight + (HighHeight - LowHeight) * (i + 0.5) / n;

        /// <summary>The belt height of run i of n, m: evenly over 0.5–1.0 H.</summary>
        public static double BeltHeightOf(int i, int n) => StripHeight * (LowBelt + (HighBelt - LowBelt) * (i + 0.5) / n);

        /// <summary>The climb's unmeasured seconds in run i of n: to the field height at ClimbRate, then SettleSeconds.</summary>
        public static double ClimbSecondsOf(int i, int n) => (HeightOf(i, n) - BeltHeightOf(i, n)) / ClimbRate + SettleSeconds;

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
        /// The wooded strip for a course from the origin on the bearing `courseDeg` (degrees), the wind abeam from its
        /// left: its long axis across the course, along the drawn wind; its near edge ApproachHeights past the belt
        /// stretch's start, its far edge StripWidthHeights beyond (belt-geometry-p.md's strip).
        /// </summary>
        public static CanopyStrip Strip(double courseDeg)
        {
            double h = StripHeight, width = StripWidthHeights * h;
            BeltStretch(out double from, out _);
            DetMath.SinCos(courseDeg * DegToRad, out double sin, out double cos);
            double centre = from + ApproachHeights * h + width / 2;
            return new CanopyStrip
            {
                CentreX = sin * centre, CentreZ = cos * centre, AxisDeg = (courseDeg + 90) % 360, Length = StripLengthHeights * h,
                Width = width, Height = h, FrontalAreaIndex = StripAreaIndex,
            };
        }

        /// <summary>The belt stretch of the course, m from the release: P's set B, from BeltStartSeconds at the ground
        /// speed for StretchHeights.</summary>
        public static void BeltStretch(out double from, out double to)
        {
            from = BeltStartSeconds * GroundSpeed;
            to = from + StretchHeights * StripHeight;
        }

        /// <summary>The test flight's setup for a seed, released at a height: its course from the seed's drawn wind, and
        /// the belt's strip. `steady` is the known-wrong fixed vector: the drawn strength from the drawn direction at every
        /// point and time, without turbulence, profile, direction changes or the strip.</summary>
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
            if (belt) wind.Canopy = Strip(courseDeg);
            if (steady) wind = WindSetup.SteadyFrom(fromDeg, drawn.MeanSpeed);
            FlightSetup setup = CrosswindScenario.Setup(spec, wind, seed, drag, drag);
            Airframe(spec, setup);
            if (level == WindLevel.Calm) setup.Asymmetry = null; // PHY-14, K§8.6: Calm is flown with the asymmetry off
            setup.StartPosition = new Double3(0, height, 0);
            setup.StartVelocity = along * ReleaseSpeed;
            // Yaw onto the course (about +Y, the heading grows), then the nose down (about body +x).
            DetMath.SinCos(course / 2, out double sy, out double cy);
            DetMath.SinCos(-HarnessPilot.ClipPPitchDeg * DegToRad / 2, out double sp, out double cp);
            setup.StartAttitude = new Quat(0, sy, 0, cy) * new Quat(sp, 0, 0, cp);
            return setup;
        }

        // The whole airframe a pilot's flight has (p7b-airframe): the motors' asymmetry on the seed's draws, the vortex rings,
        // the ground effect, the rotors' flapping and the airframe's modes.
        static void Airframe(DroneSpec spec, FlightSetup setup)
        {
            setup.Asymmetry = spec.BuildAsymmetry();
            setup.VortexRing = spec.BuildVortexRing();
            setup.GroundEffect = spec.BuildGroundEffect();
            setup.Flapping = spec.BuildFlapping();
            setup.Modes = spec.BuildModes();
        }

        const double DegToRad = 0.017453292519943295, RadToDeg = 57.29577951308232;

        /// <summary>Flies run i of n for a seed with a pilot, and logs it.</summary>
        public static Run Fly(DroneSpec spec, WindLevel level, ulong seed, int i, int n, Pilot pilot, bool belt = true, bool drag = true,
            bool steady = false)
        {
            double height = HeightOf(i, n), low = BeltHeightOf(i, n), climb = ClimbSecondsOf(i, n), seconds = Seconds + climb;
            double[] history = History(level, seed, 120, out double drawn);
            int start = StartSecond(history, drawn, seconds);
            if (start < 0)
                throw new System.InvalidOperationException("seed " + seed + "'s 120 min hold no " + seconds + " s within 90° of its drawn direction");
            FlightSetup setup = Setup(spec, level, seed, low, belt && !steady, drag, out double courseDeg, out double fromDeg,
                steady ? 0 : start, steady);

            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            double course = courseDeg * DegToRad;
            var p = new HarnessPilot(core.Controller)
            {
                Delay = pilot.Delay, TauV = pilot.TauV, Split = pilot.Split, Course = course, Height = low,
            };
            DetMath.SinCos(course, out double sin, out double cos);
            var along = new Double3(sin, 0, cos);
            var right = new Double3(cos, 0, -sin);
            BeltStretch(out double beltFrom, out double beltTo);

            var run = new Run
            {
                Level = level, Seed = seed, Pilot = pilot.Name, Belt = belt, Steady = steady, HeightRef = height, BeltHeightRef = low,
                ClimbSeconds = climb, FromDeg = fromDeg,
                MeanSpeed = core.Wind.MeanSpeed, CourseDeg = courseDeg, Mass = core.Body.Mass, StartSecond = start,
                RollInertia = core.Body.Inertia.ZZ, PitchInertia = core.Body.Inertia.XX, YawInertia = core.Body.Inertia.YY,
            };
            var samples = new List<Sample>();
            long release = core.State.Step;
            int steps = (int)(seconds * 1000), frame = 0;
            long climbFrom = -1; // the step the drone passed the stretch's end
            double lastHeading = 0;
            var fields = new HarnessPilot.Frame[4]; // the camera at the last fields' horizon rows, by field number
            for (int k = 0; k < steps; k++)
            {
                if (climbFrom < 0 && Double3.Dot(core.State.Position - setup.StartPosition, along) > beltTo) climbFrom = k;
                if (climbFrom >= 0) p.Height = System.Math.Min(height, low + ClimbRate * (k - climbFrom) * 0.001);
                if ((k - (long)HorizonMs) % (long)FieldMs == 0 || k == 0)
                    fields[(k / (long)FieldMs) % fields.Length] = HarnessPilot.Take(core.State, k, course);
                if (k * HarnessPilot.CameraHz >= frame * 1000.0)
                {
                    ref readonly FlightState s = ref core.State;
                    p.See(HarnessPilot.Take(s, k, course));
                    long pick = FieldStep(frame);
                    HarnessPilot.Frame f = fields[(pick / (long)FieldMs) % fields.Length];
                    if (f.Step != pick) throw new System.InvalidOperationException("the field pick lost its field");
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
                            InBelt = x >= beltFrom && x <= beltTo, Usable = climbFrom < 0 || k >= climbFrom + climb * 1000,
                            Crosswind = Double3.Dot(wind, right), Held = p.Height,
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
            int count = 0;
            foreach (Sample s in run.Samples)
            {
                if (!s.Usable) continue;
                count++;
                run.MeanRollDeg += s.RollDeg;
                run.MeanPitchDeg += s.PitchDeg;
                run.MeanCrosswind += s.Crosswind;
                run.MeanAirspeed += s.Airspeed;
                run.MeanHeightError += System.Math.Abs(s.Height - s.Held);
                if (s.InBelt && run.BeltFirst == 0) run.BeltFirst = s.Frame;
                if (s.InBelt) run.BeltLast = s.Frame;
            }
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
        /// The run's frames as an attitude CSV in the tracker's columns (wind.md §6.2 step 4): every confidence 1 (0 for the
        /// climb's unusable frames), dup 0, flag ok, the tracker's own diagnostics empty (a simulation has none), "\n" line
        /// ends.
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
                    .Append(s.Usable ? "1,1,1,0,,,,,,,,,,ok\n" : "0,0,0,0,,,,,,,,,,ok\n");
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
