using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The props in standing stems (w9-village-physics 2.2 and 2.12, design.md "## 2.2" and "## 2.12"): the spec's straw landing
    /// into Village's densest tall cell, the pilot's approach with a flare into it, a creep through it at a held height, a hover in
    /// it, each flown by SimPilot with surfaces.json on the farmland, optionally logged, with each step's body rates, rotor speeds,
    /// currents and front-disc cuts kept for the shake. Shared by StemTests and flightbench.
    /// </summary>
    public static class StemScenarios
    {
        /// <summary>The densest tall cell (2.1's golden cell; audit §1.12), and the hubs' height over the body origin, m.</summary>
        public static readonly XZ Dense = new XZ(768.07, 917.48);
        public const double HubOverOrigin = 0.035;

        /// <summary>A sweep's scales on the field's stems per m² and the stems' diameters (PropStrikes.StemDensityScale and
        /// StemDiameterScale), 1 for the world as it is.</summary>
        public struct Scales
        {
            public double Density, Diameter;
            public static Scales World => new Scales { Density = 1, Diameter = 1 };
        }

        /// <summary>What a stem flight did, per rotor (Corners' order) and in all, and its steps' traces.</summary>
        public sealed class Run
        {
            /// <summary>Per rotor: the stems cut (fresh, slices as cut equivalents) and their work, J; and up to the touch.</summary>
            public readonly double[] Fresh = new double[4], Sliced = new double[4], Work = new double[4];
            public readonly double[] FreshBefore = new double[4], SlicedBefore = new double[4], WorkBefore = new double[4];

            /// <summary>StemEpisode starts and ends, whole stems counted in BladeStrikes, the last step with any cut, the touch.</summary>
            public int Starts, Ends, Whole;
            public long LastCut = -1, Touch = -1, Steps;

            /// <summary>Bytes allocated after the first second, events refused, and the lowest disc height reached, m.</summary>
            public long Allocated, Refused;
            public double Lowest = double.PositiveInfinity;

            /// <summary>Per step (index = step − 1): the body rate (rad/s; X pitch, Y yaw, Z roll), the front discs' cuts and
            /// work (fr + fl), the rotors' speeds (rad/s), the motors' currents (A, the feed's field means), the front right hub's
            /// height over the ground (m) and the body's speed over the ground (m/s); over the four discs (2.13), the stems based
            /// under them, those of them leaning past 45°, the same for the tall ones (drawn 1 m or longer), those standing over
            /// their planes, and their mean pressure (Pa).</summary>
            public readonly double[] Pitch, Yaw, Roll, FrontCuts, FrontFresh, FrontWork, Height, Speed, Under, Folded, UnderTall, FoldedTall, Over, Load;
            public readonly double[][] Omega = new double[4][], Amps = new double[4][];

            /// <summary>Per rotor and step: its fresh cuts and their work (J), its hub's place (world x, z), its height over the
            /// ground (m) and its speed across its axis (m/s).</summary>
            public readonly double[][] Fresh1 = new double[4][], FreshWork1 = new double[4][], HubX = new double[4][], HubZ = new double[4][],
                HubH = new double[4][], HubV = new double[4][];

            public Run(int capacity)
            {
                Pitch = new double[capacity];
                Yaw = new double[capacity];
                Roll = new double[capacity];
                FrontCuts = new double[capacity];
                FrontFresh = new double[capacity];
                FrontWork = new double[capacity];
                Height = new double[capacity];
                Speed = new double[capacity];
                Under = new double[capacity];
                Folded = new double[capacity];
                UnderTall = new double[capacity];
                FoldedTall = new double[capacity];
                Over = new double[capacity];
                Load = new double[capacity];
                for (int c = 0; c < 4; c++)
                {
                    Omega[c] = new double[capacity];
                    Amps[c] = new double[capacity];
                    Fresh1[c] = new double[capacity];
                    FreshWork1[c] = new double[capacity];
                    HubX[c] = new double[capacity];
                    HubZ[c] = new double[capacity];
                    HubH[c] = new double[capacity];
                    HubV[c] = new double[capacity];
                }
            }
        }

        const int Capacity = 32000;

        static MapWorld Village => SlopeScenarios.Farmland();

        /// <summary>The ground's height at a point of Village, m.</summary>
        public static double Ground(XZ at)
        {
            var g = new GroundSample[1];
            Village.SampleGround(new[] { at }, g);
            return g[0].Height;
        }

        /// <summary>A flight from at (the body origin) on Village with surfaces.json, armed in its first 16 steps; the known-wrong
        /// folds no stem and cuts every stem met; log records it when given; stems false flies without them; scales sweep the
        /// field's density and diameters; column flies 2.12's undiminished column for the wash with distance (2.13's known-wrong,
        /// model 97's stems).</summary>
        public static FlightCore Start(Double3 at, bool knownWrong, Stream log, out FlightRecorder recorder, bool stems = true, Scales? scales = null,
            bool column = false)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var world = new FlightWorld(Village);
            FlightSetup setup = SoilScenarios.Setup(spec, world, at);
            setup.Contacts.Stems = stems;
            setup.StemColumn = column;
            var core = new FlightCore(setup);
            if (knownWrong)
            {
                core.Strikes.StemLean = false;
                core.Strikes.StemCritical = false;
            }
            Scales s = scales ?? Scales.World;
            core.Strikes.StemDensityScale = s.Density;
            core.Strikes.StemDiameterScale = s.Diameter;
            recorder = null;
            if (log != null)
            {
                LogHeader header = LogHeader.For(setup, core);
                header.WorldId = "farmland";
                header.SetDrone(spec, spec.Nominal);
                recorder = new FlightRecorder(log, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            }
            return core;
        }

        /// <summary>A step flown, recorded and counted: the stems' records, the episodes, the bytes after the first second, the trace.</summary>
        public static void Step(FlightCore core, StickSample stick, Run r, FlightRecorder recorder, bool beforeTouch, Action<FlightCore> stepped)
        {
            long before = AllocProbe.Before();
            core.Advance(stick);
            long k = core.State.Step;
            if (k > 1000) r.Allocated += AllocProbe.After(before);
            recorder?.RecordStep(core.State, stick, core.Trace, core.Events);
            r.Refused += core.Events.Lost;
            bool any = false;
            double front = 0, frontWork = 0, frontFresh = 0;
            for (int c = 0; c < 4; c++)
            {
                PropStrikes.StemRecord s = core.Strikes.StemLast[c];
                r.Fresh[c] += s.Fresh;
                r.Sliced[c] += s.Sliced;
                r.Work[c] += s.Work;
                if (beforeTouch)
                {
                    r.FreshBefore[c] += s.Fresh;
                    r.SlicedBefore[c] += s.Sliced;
                    r.WorkBefore[c] += s.Work;
                }
                if (s.Height > 0) r.Lowest = Math.Min(r.Lowest, s.Height);
                any |= s.Cuts > 0;
                if (c == 1 || c == 3)
                {
                    front += s.Cuts;
                    frontFresh += s.Fresh;
                    frontWork += s.Work;
                }
            }
            if (any) r.LastCut = k;
            for (int i = 0; i < BladeStrikes.Slots; i++)
                if (core.State.BladeStrikes[i].Kind == StrikeKind.Stem) r.Whole += core.State.BladeStrikes[i].Count;
            for (int i = 0; i < core.Events.Count; i++)
            {
                FlightEvent e = core.Events[i];
                if (e.Kind != EventKind.StemEpisode) continue;
                if ((e.Index & 0x80) == 0) r.Starts++;
                else r.Ends++;
            }
            r.Steps = k;
            int at = (int)k - 1;
            if (at >= 0 && at < r.Pitch.Length)
            {
                Double3 w = core.State.BodyRate, v = core.State.Velocity;
                r.Pitch[at] = w.X;
                r.Yaw[at] = w.Y;
                r.Roll[at] = w.Z;
                r.FrontCuts[at] = front;
                r.FrontFresh[at] = frontFresh;
                r.FrontWork[at] = frontWork;
                r.Height[at] = core.Strikes.StemLast[1].Height;
                r.Speed[at] = Math.Sqrt(v.X * v.X + v.Z * v.Z);
                double load = 0;
                for (int c = 0; c < 4; c++)
                {
                    PropStrikes.StemRecord s = core.Strikes.StemLast[c];
                    r.Under[at] += s.Under;
                    r.Folded[at] += s.Folded;
                    r.UnderTall[at] += s.UnderTall;
                    r.FoldedTall[at] += s.FoldedTall;
                    r.Over[at] += s.Over;
                    load += s.Load * s.Under;
                }
                r.Load[at] = r.Under[at] > 0 ? load / r.Under[at] : 0;
                core.GetSignals(out FeedSignals f);
                Double3 axis = core.State.Attitude.Rotate(new Double3(0, 1, 0));
                for (int c = 0; c < 4; c++)
                {
                    r.Omega[c][at] = core.State.RotorSpeed[c];
                    r.Amps[c][at] = f.MotorCurrentA[c];
                    PropStrikes.StemRecord s = core.Strikes.StemLast[c];
                    r.Fresh1[c][at] = s.Fresh;
                    r.FreshWork1[c][at] = s.FreshWork;
                    r.HubH[c][at] = s.Height;
                    ContactShape d = Array.Find(core.Contacts.Spec.Shapes, x => x.Role == ContactRole.Disc && x.Corner == c);
                    Double3 hub = core.State.Position + core.State.Attitude.Rotate(d.A);
                    Double3 hv = core.State.Velocity + core.State.Attitude.Rotate(Double3.Cross(core.State.BodyRate, d.A));
                    Double3 across = hv - axis * Double3.Dot(hv, axis);
                    r.HubX[c][at] = hub.X;
                    r.HubZ[c][at] = hub.Z;
                    r.HubV[c][at] = across.Length();
                }
            }
            stepped?.Invoke(core);
        }

        static void Arm(FlightCore core, Run r, FlightRecorder recorder, Action<FlightCore> stepped)
        {
            for (int k = 0; k < 16; k++)
                Step(core, new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }, r, recorder, true, stepped);
        }

        /// <summary>
        /// The spec's straw landing: from a hover with the hubs 1.9 m (from) over the densest tall cell (over its tallest tops, 1.77 m),
        /// the target comes down at descent m/s (and forward at forward m/s, heading +z), the sticks held when the skids are 0.1 m up,
        /// the throttle cut at the first touch, flown 2 s past it (SoilScenarios.Land's pilot).
        /// </summary>
        public static Run Landing(double descent = 0.5, double forward = 0, bool knownWrong = false, Stream log = null, Action<FlightCore> stepped = null,
            bool stems = true, Scales? scales = null, bool column = false, int refresh = 0, double from = 1.9)
        {
            double stand = SoilScenarios.Standing(RepositoryDrone.Spec()), ground = Ground(new XZ(Dense.X, Dense.Z - forward * 2.6));
            var foot = new Double3(Dense.X, ground, Dense.Z - forward * 2.6);
            double y0 = ground + from - HubOverOrigin;
            FlightCore core = Start(new Double3(foot.X, y0, foot.Z), knownWrong, log, out FlightRecorder recorder, stems, scales, column);
            if (refresh > 0) core.Strikes.StemAirRefresh = refresh;
            var r = new Run(Capacity);
            var pilot = new SimPilot(core.Controller, new Double3(foot.X, y0, foot.Z));
            Arm(core, r, recorder, stepped);
            for (int k = 0; k < 4000; k++) Step(core, pilot.Sticks(core, core.State.Step * FlightCore.StepUs), r, recorder, true, stepped);
            StickSample held = default;
            bool holding = false;
            for (int k = 0; k < 20000; k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick = held;
                if (!holding)
                {
                    double t = k * FlightCore.Dt;
                    pilot.Target = new Double3(foot.X, y0 - descent * t, foot.Z + forward * t);
                    if (forward > 0)
                    {
                        pilot.HoldVelocity = true;
                        pilot.TargetVelocity = new Double3(0, -descent, forward);
                    }
                    stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                    if (s.Position.Y - Ground(new XZ(s.Position.X, s.Position.Z)) - stand < 0.1 && s.Velocity.Y < 0)
                    {
                        holding = true;
                        held = stick;
                        held.Roll = held.Pitch = held.Yaw = 0;
                        stick = held;
                    }
                }
                if (r.Touch >= 0) stick.Throttle = 0;
                stick.HostUs = s.Step * FlightCore.StepUs;
                Step(core, stick, r, recorder, r.Touch < 0, stepped);
                Touched(core, r);
                if (r.Touch >= 0 && core.State.Step - r.Touch >= 2000) break;
            }
            recorder?.Close();
            return r;
        }

        static void Touched(FlightCore core, Run r)
        {
            for (int i = 0; i < core.Events.Count && r.Touch < 0; i++)
                if (core.Events[i].Kind == EventKind.ContactMade && core.Events[i].Object == WorldIds.Ground) r.Touch = core.State.Step;
        }

        /// <summary>
        /// The pilot's landing (design.md 2.12): 3 s of hover with the hubs 1.9 m over the ground south of the densest tall cell,
        /// then an approach north (+z, the nose) at speed m/s coming down at descent m/s; below flare m of the hubs over the ground
        /// the target's forward speed goes to 0 (the flare: the nose comes up), the descent held; the sticks held level when the
        /// skids are 0.1 m up, the throttle cut at the first touch, flown 2 s past it. It starts where that brings the touch to the
        /// cell's centre (the approach's run-in v·(1.9 − flare)/descent and the flare's v²/(2·3 m/s²), assumed).
        /// </summary>
        public static Run Approach(double speed = 2, double descent = 0.5, double flare = 1.1, Stream log = null, Action<FlightCore> stepped = null,
            bool stems = true, Scales? scales = null, bool column = false, int refresh = 0)
        {
            double stand = SoilScenarios.Standing(RepositoryDrone.Spec());
            double run = speed * (1.9 - flare) / descent + speed * speed / 6;
            var start = new XZ(Dense.X, Dense.Z - run);
            double y0 = Ground(start) + 1.9 - HubOverOrigin;
            FlightCore core = Start(new Double3(start.X, y0, start.Z), false, log, out FlightRecorder recorder, stems, scales, column);
            if (refresh > 0) core.Strikes.StemAirRefresh = refresh;
            var r = new Run(Capacity);
            var pilot = new SimPilot(core.Controller, new Double3(start.X, y0, start.Z));
            Arm(core, r, recorder, stepped);
            for (int k = 0; k < 3000; k++) Step(core, pilot.Sticks(core, core.State.Step * FlightCore.StepUs), r, recorder, true, stepped);
            pilot.HoldVelocity = true;
            StickSample held = default;
            bool holding = false, flared = false;
            for (int k = 0; k < 20000; k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick = held;
                if (!holding)
                {
                    double over = s.Position.Y + HubOverOrigin - Ground(new XZ(s.Position.X, s.Position.Z));
                    if (over < flare) flared = true;
                    pilot.TargetVelocity = new Double3(0, -descent, flared ? 0 : speed);
                    pilot.Target = pilot.Target + pilot.TargetVelocity * FlightCore.Dt;
                    stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                    if (s.Position.Y - Ground(new XZ(s.Position.X, s.Position.Z)) - stand < 0.1 && s.Velocity.Y < 0)
                    {
                        holding = true;
                        held = stick;
                        held.Roll = held.Pitch = held.Yaw = 0;
                        stick = held;
                    }
                }
                if (r.Touch >= 0) stick.Throttle = 0;
                stick.HostUs = s.Step * FlightCore.StepUs;
                Step(core, stick, r, recorder, r.Touch < 0, stepped);
                Touched(core, r);
                if (r.Touch >= 0 && core.State.Step - r.Touch >= 2000) break;
            }
            recorder?.Close();
            return r;
        }

        /// <summary>
        /// A creep: 4 s of hover with the hubs height m over the ground 1.3 m south of the densest tall cell's centre, then forward
        /// (+z, the nose) at speed m/s for seconds, the hubs' height over the ground held throughout.
        /// </summary>
        public static Run Creep(double height = 0.6, double speed = 0.3, double seconds = 6, Stream log = null, Action<FlightCore> stepped = null, bool stems = true,
            Scales? scales = null, bool column = false)
        {
            var start = new XZ(Dense.X, Dense.Z - 1.3);
            double y = Ground(start) + height - HubOverOrigin;
            FlightCore core = Start(new Double3(start.X, y, start.Z), false, log, out FlightRecorder recorder, stems, scales, column);
            var r = new Run(Capacity);
            var pilot = new SimPilot(core.Controller, new Double3(start.X, y, start.Z));
            Arm(core, r, recorder, stepped);
            int steps = (int)(seconds / FlightCore.Dt);
            for (int k = 0; k < 4000 + steps; k++)
            {
                if (k == 4000)
                {
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = new Double3(0, 0, speed);
                }
                // The height held over the ground under the hubs: the pilot's target trimmed by the error (an integral, 2 /s).
                ref readonly FlightState s = ref core.State;
                double over = s.Position.Y + HubOverOrigin - Ground(new XZ(s.Position.X, s.Position.Z));
                pilot.Target = new Double3(start.X, pilot.Target.Y + 2 * (height - over) * FlightCore.Dt, start.Z);
                Step(core, pilot.Sticks(core, s.Step * FlightCore.StepUs), r, recorder, true, stepped);
            }
            recorder?.Close();
            return r;
        }

        /// <summary>A hover with the hubs height m over the densest tall cell for seconds.</summary>
        public static Run Hover(double height = 0.5, double seconds = 10, Stream log = null, Action<FlightCore> stepped = null, bool column = false)
        {
            double y = Ground(Dense) + height - HubOverOrigin;
            var at = new Double3(Dense.X, y, Dense.Z);
            FlightCore core = Start(at, false, log, out FlightRecorder recorder, column: column);
            var r = new Run(Capacity);
            var pilot = new SimPilot(core.Controller, at);
            Arm(core, r, recorder, stepped);
            int steps = (int)(seconds / FlightCore.Dt);
            for (int k = 0; k < steps; k++) Step(core, pilot.Sticks(core, core.State.Step * FlightCore.StepUs), r, recorder, true, stepped);
            recorder?.Close();
            return r;
        }

        /// <summary>
        /// The range's straw (S04, the range golden's landing spot, RangeScenarios.Straw): 3 s of hover with the hubs at height m
        /// (world y, the straw's ground at 0) 1 m south of it, then forward (+z) at speed m/s for seconds through its stalks.
        /// </summary>
        public static Run RangeCreep(double height = 0.4, double speed = 0.3, double seconds = 4, Action<FlightCore> stepped = null, bool column = false)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Double3 foot = RangeScenarios.Straw;
            var at = new Double3(foot.X, height - HubOverOrigin, foot.Z - 1.0);
            FlightSetup setup = SoilScenarios.Setup(spec, new FlightWorld(RangeScenarios.World), at);
            setup.StemColumn = column;
            var core = new FlightCore(setup);
            var r = new Run(Capacity);
            var pilot = new SimPilot(core.Controller, at);
            Arm(core, r, null, stepped);
            int steps = (int)(seconds / FlightCore.Dt);
            for (int k = 0; k < 3000 + steps; k++)
            {
                if (k == 3000)
                {
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = new Double3(0, 0, speed);
                }
                ref readonly FlightState s = ref core.State;
                pilot.Target = new Double3(at.X, pilot.Target.Y + 2 * (height - s.Position.Y - HubOverOrigin) * FlightCore.Dt, at.Z);
                Step(core, pilot.Sticks(core, s.Step * FlightCore.StepUs), r, null, true, stepped);
            }
            return r;
        }

        /// <summary>The farmland's world for a replay of a stem flight's log.</summary>
        public static FlightWorld OpenFarmland(string id) => id == "farmland" ? new FlightWorld(Village) : null;
    }

    /// <summary>
    /// The shake as the pilot sees it (design.md 2.12), from a run's per-step body rates over a window of steps: the rate's RMS
    /// in a band (°/s) by a direct DFT of the window less its mean, the attitude's RMS in the band (°, each bin's rate over 2πf),
    /// and the strongest line in a band (its frequency and its amplitude, °/s).
    /// </summary>
    public static class Shake
    {
        const double Deg = 57.29577951308232;

        public static double RateRms(double[] x, int from, int n, double f0, double f1) => Band(x, from, n, f0, f1, false, out _, out _);

        public static double AngleRms(double[] x, int from, int n, double f0, double f1) => Band(x, from, n, f0, f1, true, out _, out _);

        public static double Line(double[] x, int from, int n, double f0, double f1, out double at)
        {
            Band(x, from, n, f0, f1, false, out at, out double peak);
            return peak;
        }

        static double Band(double[] x, int from, int n, double f0, double f1, bool angle, out double at, out double peak)
        {
            double mean = 0;
            for (int i = 0; i < n; i++) mean += x[from + i];
            mean /= n;
            double fs = 1 / FlightCore.Dt, sum = 0;
            at = 0;
            peak = 0;
            int k0 = (int)Math.Ceiling(f0 * n / fs), k1 = (int)Math.Floor(f1 * n / fs);
            for (int k = Math.Max(1, k0); k <= k1 && k < n / 2; k++)
            {
                double re = 0, im = 0, w = 2 * Math.PI * k / n;
                for (int i = 0; i < n; i++)
                {
                    double v = x[from + i] - mean;
                    re += v * Math.Cos(w * i);
                    im -= v * Math.Sin(w * i);
                }
                double amp = 2 * Math.Sqrt(re * re + im * im) / n * Deg, f = k * fs / n; // the bin's amplitude, °/s
                if (amp > peak)
                {
                    peak = amp;
                    at = f;
                }
                double a = angle ? amp / (2 * Math.PI * f) : amp;
                sum += 0.5 * a * a;
            }
            return Math.Sqrt(sum);
        }
    }
}
