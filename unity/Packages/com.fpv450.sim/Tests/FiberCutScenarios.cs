using System;
using System.Collections.Generic;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7d-tether 1.8's scenarios (design 7, 8, 14; the fiber-tether spec's "Cut risk, never certainty", "The link margin", "The
    /// break loses the picture and the flight controller fails safe", "The link's signals reach the feed", "The fiber's
    /// randomness"), and the pilot's four causes as flights against their known-wrong models (PHY-19: "overly sharp reverse,
    /// snagging on trees, very sharp lift off, or sudden momentum backwards after lift off"), with a normal liftoff that must
    /// not cut. Without NUnit, so a probe can print them.
    /// </summary>
    public static class FiberCutScenarios
    {
        const double Dt = FlightCore.Dt, Pi = 3.141592653589793;

        /// <summary>FIBER_DRONE=model63 flies the tests' scenarios on model 63's drone (the old coil, Model63Drone; TetherScenarios
        /// gives it model 63's exit, GoldenFlights.Model63Tether), for the cause table's rows beside model 64's canister.</summary>
        public static readonly bool OnTheOldCoil = System.Environment.GetEnvironmentVariable("FIBER_DRONE") == "model63";

        static TetherTable Table => TetherScenarios.Table();

        /// <summary>What a flight did to its line.</summary>
        public struct Outcome
        {
            public bool Broken;
            public TetherMechanism Mechanism;
            public FiberMember Place;
            public int Object, Shape, Part;
            public double Time, Stress, Strength, Turn, Tension, Radius, Material;
            /// <summary>The line's least distance to a rotor's rim over the flight, m; the most exit tension, N; the most lip angle,
            /// rad; the least margin, dB; the dips; the least bend radius, m.</summary>
            public double Rim, ExitMost, LipMost, MarginLeast, BendLeast;
            public int Dips;
            public long Wedges;
            public bool Crashed;
            public double Height;
            public double PeelEnergy, SpanStrength;
            /// <summary>After a break: the near piece's nodes then, whether it still hangs at the end, the far piece's points moved
            /// since (the most, m), the line's force on the drone against the near piece's exit tension along its segment (the worst,
            /// N), whether the link was Broken in the break's step, the steps flown after it.</summary>
            public int NearNodes;
            public bool Attached, BrokenInStep;
            public double FarMoved, ForceError;
            public long After;
            /// <summary>The break's point in the body's axes, m (x right, y up, z forward).</summary>
            public Double3 BodyPoint;

            public override string ToString() => Broken
                ? $"cut {Mechanism} at {Place} (obj {Object} shape {Shape} part {Part}) t {Time:0.000} s, σ {Stress / 1e9:0.000} GPa vs {Strength / 1e9:0.000}, θ {Turn:0.000}, T {Tension:0.000} N, R {Radius * 1e3:0.000} mm, at body ({BodyPoint.X:0.000}, {BodyPoint.Y:0.000}, {BodyPoint.Z:0.000}) m; rim {Rim * 1e3:0.0} mm, exit {ExitMost:0.000} N, lip {LipMost * 180 / Pi:0} deg, margin {MarginLeast:0.00} dB"
                : $"whole: rim {Rim * 1e3:0.0} mm, exit {ExitMost:0.000} N, lip {LipMost * 180 / Pi:0} deg, margin {MarginLeast:0.00} dB, dips {Dips}, bend {BendLeast * 1e3:0.00} mm, wedges {Wedges}{(Crashed ? ", crashed" : "")}";
        }

        /// <summary>Watches a flight's line each step: the break, the rims, the exit's tension, the lip, the margin.</summary>
        public sealed class Watch
        {
            public Outcome O = new Outcome { Rim = double.PositiveInfinity, MarginLeast = double.PositiveInfinity, BendLeast = double.PositiveInfinity };
            readonly FlightSetup _setup;
            readonly double _radius;
            Double3[] _far;
            int _farFrom;

            public Watch(FlightSetup setup)
            {
                _setup = setup;
                _radius = setup.Propulsion.Props.Diameter / 2;
            }

            public void Step(FlightCore core)
            {
                FiberLine f = core.Tether;
                ref readonly FlightState s = ref core.State;
                O.Crashed |= s.Crashed;
                O.Height = s.Position.Y;
                O.PeelEnergy = f.PeelEnergy;
                if (f.Cuts != null) O.SpanStrength = f.Cuts.SpanStrength;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    ref readonly FlightEvent e = ref core.Events[i];
                    if (e.Kind == EventKind.TetherWedge) O.Wedges++;
                    if (e.Kind == EventKind.FiberSection && (e.Index & 0x80) != 0)
                    {
                        O.Stress = e.C;
                        O.Strength = e.B;
                    }
                    if (e.Kind != EventKind.TetherBreak) continue;
                    O.Broken = true;
                    O.Mechanism = (TetherMechanism)(e.Index & 0x0F);
                    O.Place = (FiberMember)(e.Index >> 4);
                    O.Object = e.Object;
                    O.Shape = e.Shape;
                    O.Part = e.Part;
                    O.Time = s.Step * Dt;
                    O.Turn = e.A;
                    O.Tension = e.B;
                    O.Radius = e.C;
                    O.Material = f.LastBreak.Material;
                    O.BodyPoint = s.Attitude.InverseRotate(e.Point - s.Position);
                    if (O.Mechanism == TetherMechanism.Prop || O.Mechanism == TetherMechanism.Wedge)
                    {
                        O.Stress = f.LastBreak.Stress;
                        O.Strength = f.LastBreak.Strength;
                    }
                }
                if (f.Broken)
                {
                    if (_far == null)
                    {
                        _farFrom = f.TrailBeforeBreak;
                        _far = new Double3[f.TrailCount - _farFrom];
                        for (int i = 0; i < _far.Length; i++) _far[i] = f.Trail[_farFrom + i];
                        O.NearNodes = f.Attached ? f.Chain.Count : 0;
                        O.BrokenInStep = f.BrokeThisStep && f.Link.State == LinkState.Broken && double.IsNegativeInfinity(f.Link.Margin);
                    }
                    else O.After++;
                    for (int i = 0; i < _far.Length; i++) O.FarMoved = Math.Max(O.FarMoved, (f.Trail[_farFrom + i] - _far[i]).Length());
                    O.Attached = f.Attached;
                    if (f.Attached && !f.BrokeThisStep)
                    {
                        CableChain near = f.Chain;
                        int n = near.Count;
                        Double3 outward = near[n - 2].Position - near[n - 1].Position;
                        double sdot = near.SpoolSpeed, pull = near.SpoolExitTension - (sdot > 0 ? f.MassPerMetre * sdot * sdot : 0);
                        O.ForceError = Math.Max(O.ForceError, (f.Force - outward * (pull / outward.Length())).Length());
                    }
                    return;
                }
                if (!f.Attached) return;
                if (f.ExitTension > O.ExitMost) O.ExitMost = f.ExitTension;
                if (f.Link != null)
                {
                    if (f.Link.Margin < O.MarginLeast) O.MarginLeast = f.Link.Margin;
                    O.Dips = f.Link.Dips;
                    double lip = f.Cuts.Bend(f.Cuts.BendCount - 1).Turn;
                    if (lip > O.LipMost) O.LipMost = lip;
                    if (f.MinBendRadius < O.BendLeast) O.BendLeast = f.MinBendRadius;
                }
                CableChain line = f.Chain;
                Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
                for (int i = 0; i < line.Count - 1; i++)
                    for (int m = 0; m < 4; m++)
                    {
                        Double3 q = line[i].Position - (s.Position + s.Attitude.Rotate(_setup.Propulsion.Motors[m].Position));
                        double z = Double3.Dot(q, up);
                        double lateral = (q - up * z).Length();
                        double rim = DetMath.Sqrt((lateral - _radius) * (lateral - _radius) + z * z);
                        if (lateral < _radius) rim = DetMath.Abs(z); // over the disc: its height off the disc
                        if (rim < O.Rim) O.Rim = rim;
                    }
            }
        }

        /// <summary>The setup's wind level for a "Calm" or "Windy" flight.</summary>
        /// <summary>FIBER_BODY=through flies every scenario with the fountain through the drone's body (FlightSetup.
        /// TetherFountainThroughBody, 61's known-wrong): with FIBER_DRONE=model63, model 63's line, for the cause table's row.</summary>
        public static readonly bool ThroughTheBody = System.Environment.GetEnvironmentVariable("FIBER_BODY") == "through";

        /// <summary>FIBER_HOLD=zero flies every scenario with the known-wrong 0-force hold (FlightSetup.TetherZeroForceHold, 62's
        /// known-wrong: model 64's chain, but for 62's shapes met by their own faces, which no switch keeps), for the cause table's
        /// rows beside model 65's: every flight is model 64's but the tree snag's with the fiber's contacts (the faces) and its seed 8
        /// without them (the same cause, by a disc, 0.7 s earlier; not traced).</summary>
        public static readonly bool ZeroForceHold = System.Environment.GetEnvironmentVariable("FIBER_HOLD") == "zero";

        /// <summary>FIBER_CHAIN=linear flies every scenario with the known-wrong linear step (FlightSetup.TetherLinearStep, model 74's
        /// chain: the line's step on its start's linearisation alone, 1.8's finding 2).</summary>
        public static readonly bool LinearStep = System.Environment.GetEnvironmentVariable("FIBER_CHAIN") == "linear";

        /// <summary>FIBER_LIP=flared flies every scenario over model 64–73's 4 mm flared lip (the known-wrong for the hole's edge);
        /// FIBER_LIPWEAR=severe with the severe Archard k on the hole's edge (a bare grooved plastic hole: the known-wrong for its
        /// wear); FIBER_PEEL=old with model 56–73's peel reference rate, 1 m/s (the known-wrong for the cruise's peel).</summary>
        public static readonly bool FlaredLip = System.Environment.GetEnvironmentVariable("FIBER_LIP") == "flared",
            SevereLip = System.Environment.GetEnvironmentVariable("FIBER_LIPWEAR") == "severe",
            OldPeel = System.Environment.GetEnvironmentVariable("FIBER_PEEL") == "old";

        /// <summary>FIBER_BLADE=edgewise flies every scenario with the flapping's blade (RotorFlappingSpec, as the session's drone flies)
        /// and so the edgewise flow's thrust and torque (EdgewiseThrust, model 76); FIBER_BLADE=axial with the blade but the props'
        /// axial tables alone (model 75's and 78's rotor). Unset, the scenarios fly without the blade, as they always have: the
        /// edgewise thrust then never acts (model 79's integration, p7d-tether 1.8).</summary>
        public static readonly string Blade = System.Environment.GetEnvironmentVariable("FIBER_BLADE");

        /// <summary>Whether the scenarios fly the session's airframe (SessionAirframe), set by the D-087 grid and the cause table
        /// (p7d-tether 1.11) for their run: off, they fly TetherScenarios.Setup's bare drone, as every other scenario does.</summary>
        public static bool SessionDrone;

        /// <summary>FIBER_AIRFRAME=bare flies the grid and the cause table on the bare drone (no flapping's blade, ground effect,
        /// vortex ring, interference, asymmetry, build tolerance, wake unsteadiness or modes), as they flew to model 79: the audits'.</summary>
        public static readonly bool BareAirframe = System.Environment.GetEnvironmentVariable("FIBER_AIRFRAME") == "bare";

        /// <summary>The airframe's effects as SessionSetup.Build gives the player (the powered golden's and the build tolerance):
        /// the motors' asymmetry, the build tolerance, the vortex rings, the ground and ceiling effect, each rotor's wake
        /// unsteadiness, the flapping's blade with its edgewise thrust, the rotors' interference and the airframe's modes.</summary>
        public static void SessionAirframe(DroneSpec spec, FlightSetup s)
        {
            s.Asymmetry = spec.BuildAsymmetry();
            s.Placement = spec.BuildPlacement();
            s.VortexRing = spec.BuildVortexRing();
            s.GroundEffect = spec.BuildGroundEffect();
            s.RotorWake = spec.BuildRotorWake();
            s.Flapping = spec.BuildFlapping();
            s.Interference = spec.BuildInterference();
            s.Modes = spec.BuildModes();
        }

        /// <summary>tether.json with one row's value replaced (a known-wrong's table).</summary>
        public static TetherTable With(TetherTable table, string name, double value)
        {
            var rows = (TetherRow[])table.Rows.Clone();
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].Name == name) rows[i].Values = new[] { value };
            return new TetherTable(rows, table.ContentHash);
        }

        static FlightSetup Setup(DroneSpec spec, WindLevel wind, ulong seed, Action<FlightSetup> edit)
        {
            FlightSetup setup = TetherScenarios.Setup(spec, wind, seed: seed, cuts: true);
            if (FlaredLip) setup.Tether = With(setup.Tether, "spool.lip_radius_m", 0.004);
            if (SevereLip) setup.Tether = With(setup.Tether, "spool.lip_archard_wear_coefficient", setup.Tether.Number("fiber.archard_wear_coefficient"));
            if (OldPeel) setup.Tether = With(setup.Tether, "spool.peel_reference_rate_m_s", 1);
            if (ThroughTheBody) setup.TetherFountainThroughBody = true;
            if (ZeroForceHold) setup.TetherZeroForceHold = true;
            if (LinearStep) setup.TetherLinearStep = true;
            if (SessionDrone) SessionAirframe(spec, setup);
            if (Blade == "edgewise" || Blade == "axial")
            {
                setup.Flapping = spec.BuildFlapping();
                setup.Flapping.EdgewiseThrust = Blade == "edgewise";
            }
            edit?.Invoke(setup);
            return setup;
        }

        /// <summary>Flies setup for seconds under plan (TetherScenarios.Fly's arming), the watch on every step.</summary>
        static Outcome Run(FlightSetup setup, double seconds, TetherScenarios.Plan plan, Action<FlightCore, SimPilot> init = null)
        {
            var w = new Watch(setup);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            init?.Invoke(core, pilot);
            int steps = (int)(seconds * 1000);
            for (int k = 0; k < steps; k++)
            {
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    plan(k * Dt, core, pilot);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                core.Advance(stick);
                w.Step(core);
                Each?.Invoke(core);
                if (w.O.Broken && StopAtBreak) break;
            }
            return w.O;
        }

        // ---- the pilot's four causes (PHY-19) ----

        /// <summary>
        /// "Overly sharp reverse" (design 16's course): up to 10 m, along +z to 15 m/s, then at 11 s a full reverse to 8 m/s back
        /// along the path (the pilot's tilt up to 60° for it, so within 2 s), on to 16 s. The known-wrong: the two-way spool.
        /// </summary>
        public static Outcome SharpReverse(DroneSpec spec, WindLevel wind, ulong seed, Action<FlightSetup> edit = null, double speed = 15)
        {
            FlightSetup setup = Setup(spec, wind, seed, edit);
            double stand = TetherScenarios.Stand(setup);
            // Past 15 m/s the cruise needs the pilot's 60° (the drone's drag at 20–28 m/s); at 15 and under, design 16's 35°.
            double cruiseTilt = speed > 15 ? 0.866 : 0.5736;
            return Run(setup, 16, (t, core, pilot) =>
            {
                double y = stand + DetMath.Min(10, 2 * t);
                if (t < 5)
                {
                    pilot.HoldVelocity = false;
                    pilot.Target = new Double3(0, y, 0);
                    return;
                }
                pilot.HoldVelocity = true;
                pilot.MaxTiltSin = t < 11 ? cruiseTilt : 0.866;
                pilot.TargetVelocity = new Double3(0, 0, t < 11 ? speed : -8);
                pilot.Target = new Double3(0, y, core.State.Position.Z);
            });
        }

        /// <summary>A turn at speed, what the turn did and the line's outcome.</summary>
        public struct Turned
        {
            public Outcome O;
            /// <summary>The speed and the nose's pitch (down +) in the cruise just before the turn, m/s and rad; the most rate of the
            /// nose's heading and of the path's in the turn, rad/s; the most horizontal acceleration, m/s².</summary>
            public double Speed, Pitch, NoseRate, PathRate, Lateral;
            /// <summary>In the turn and after it, while the line is whole: the most lip wrap, rad, and tension at the lip, N; the most wear of
            /// the hole's edge on a section, a share of the coating; the sections in FiberCuts' table at the end, and the WorldBugs.</summary>
            public double LipTurn, ExitTurn, LipWear;
            public int Sections, Bugs;
        }

        /// <summary>
        /// A turn at speed (D-087's grid): up to 10 m, along +z to speed (m/s) at the pilot's 60° tilt, then at 11 s the nose and
        /// the path turned right through degrees, the heading moving at rate (°/s, the yaw fed forward; 0 a straight cruise), the
        /// velocity held along the heading at speed, on to 16 s. What the drone makes of it (its nose's and path's rates, its
        /// acceleration) is the physics', printed beside the line's outcome.
        /// </summary>
        public static Turned Turn(DroneSpec spec, WindLevel wind, ulong seed, double speed, double rate, double degrees = 90,
            Action<FlightSetup> edit = null)
        {
            FlightSetup setup = Setup(spec, wind, seed, edit);
            double stand = TetherScenarios.Stand(setup);
            double w = rate * Pi / 180, total = degrees * Pi / 180;
            var r = new Turned();
            double lastNose = double.NaN, lastPath = double.NaN;
            Double3 lastV = default;
            r.O = Run(setup, 16, (t, core, pilot) =>
            {
                ref readonly FlightState s = ref core.State;
                double y = stand + DetMath.Min(10, 2 * t);
                if (t < 5)
                {
                    pilot.HoldVelocity = false;
                    pilot.Target = new Double3(0, y, 0);
                    return;
                }
                pilot.HoldVelocity = true;
                pilot.MaxTiltSin = 0.866;
                double psi = t < 11 ? 0 : DetMath.Min(total, w * (t - 11));
                DetMath.SinCos(psi, out double sin, out double cos);
                pilot.Heading = new Double3(sin, 0, cos);
                pilot.YawRate = t >= 11 && w * (t - 11) < total ? w : 0;
                pilot.TargetVelocity = new Double3(speed * sin, 0, speed * cos);
                pilot.Target = new Double3(s.Position.X, y, s.Position.Z);
                Double3 nose = s.Attitude.Rotate(new Double3(0, 0, 1));
                double noseYaw = DetMath.Atan2(nose.X, nose.Z), pathYaw = DetMath.Atan2(s.Velocity.X, s.Velocity.Z);
                if (t < 11)
                {
                    r.Speed = DetMath.Sqrt(s.Velocity.X * s.Velocity.X + s.Velocity.Z * s.Velocity.Z);
                    r.Pitch = -DetMath.Atan2(nose.Y, DetMath.Sqrt(nose.X * nose.X + nose.Z * nose.Z));
                }
                else if (!core.Tether.Broken)
                {
                    // Rates over the pilot's 4-step period's samples would alias: each step's, unwrapped.
                    if (!double.IsNaN(lastNose))
                    {
                        double dn = Wrap(noseYaw - lastNose) / Dt, dp = Wrap(pathYaw - lastPath) / Dt;
                        if (DetMath.Abs(dn) > r.NoseRate) r.NoseRate = DetMath.Abs(dn);
                        if (DetMath.Abs(dp) > r.PathRate && r.Speed > 1) r.PathRate = DetMath.Abs(dp);
                        Double3 a = (s.Velocity - lastV) * (1 / Dt);
                        double lat = DetMath.Sqrt(a.X * a.X + a.Z * a.Z);
                        if (lat > r.Lateral) r.Lateral = lat;
                    }
                }
                lastNose = noseYaw;
                lastPath = pathYaw;
                lastV = s.Velocity;
                FiberLine f = core.Tether;
                for (int i = 0; i < core.Events.Count; i++) if (core.Events[i].Kind == EventKind.WorldBug) r.Bugs++;
                if (f.Cuts != null) r.Sections = f.Cuts.Sections;
                if (f.Broken || !f.Attached) return;
                if (f.LipWear > r.LipWear) r.LipWear = f.LipWear;
                if (t < 11 || f.Cuts == null) return;
                ref readonly FiberBend lipBend = ref f.Cuts.Bend(f.Cuts.BendCount - 1);
                if (lipBend.Turn > r.LipTurn) r.LipTurn = lipBend.Turn;
                if (lipBend.Tension > r.ExitTurn) r.ExitTurn = lipBend.Tension;
            });
            return r;
        }

        static double Wrap(double a) => a > Pi ? a - 2 * Pi : a < -Pi ? a + 2 * Pi : a;

        /// <summary>
        /// "Snagging on trees" (design 16's course): V01_L on the test range, the line laid west to east under it, the drone over
        /// the crown at a height through its limbs, west across it and back east across it at 4 m/s, so the line is dragged
        /// through the crown and turned back over it. The known-wrong: the line without its contacts.
        /// </summary>
        public static Outcome TreeSnag(DroneSpec spec, WindLevel wind, ulong seed, Action<FlightSetup> edit = null, double height = 9)
        {
            MapWorld map = RangeScenarios.World;
            int tree = RangeScenarios.Placed("V01_L");
            map.Geometry(tree, 0, out ShapeGeometry trunk);
            Double3 centre = new Double3(trunk.Center.X, 0, trunk.Center.Z);
            FlightSetup setup = TetherScenarios.Setup(spec, wind, seed: seed, cuts: true);
            if (SessionDrone) SessionAirframe(spec, setup);
            setup.World = new FlightWorld(map);
            double stand = TetherScenarios.Stand(setup);
            setup.StartPosition = new Double3(centre.X + 10, stand, centre.Z - 1);
            var layout = new List<Double3>();
            for (double d = -10; d < 9.5; d += 1) layout.Add(new Double3(centre.X + d, 0, centre.Z - 1));
            setup.TetherLayout = layout.ToArray();
            edit?.Invoke(setup);
            return Run(setup, 24, (t, core, pilot) =>
            {
                pilot.HoldVelocity = false;
                double y = stand + DetMath.Min(height, 2 * t), u = t < 6 ? 0 : 4 * (t - 6), x;
                // From 10 m east to 10 m west (20 m), then back east to 10 m and west again.
                double w = (u - 20) % 40;
                x = u < 20 ? 10 - u : w < 20 ? -10 + w : 10 - (w - 20);
                pilot.Target = new Double3(centre.X + x, y, centre.Z);
            });
        }

        /// <summary>
        /// "Very sharp lift off" (design 16): legs on from the floor's start with the line laid from 3 m behind, full throttle from
        /// rest in 0.1 s (sharp), held 1 s, then the pilot catches it 5 m above where it is; or the pilot's own gentle liftoff to 1.5 m
        /// (normal). The known-wrong: design 4's air (no fountain or ground vortex).
        /// </summary>
        public static Outcome Liftoff(DroneSpec spec, bool sharp, ulong seed, Action<FlightSetup> edit = null, WindLevel wind = WindLevel.Calm)
        {
            FlightSetup setup = Setup(spec, wind, seed, edit);
            double stand = TetherScenarios.Stand(setup);
            var w = new Watch(setup);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition + new Double3(0, 1.5, 0));
            double hold = double.NaN;
            for (int k = 0; k < 5000; k++)
            {
                StickSample stick;
                if (k < 100) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else if (sharp && k < 1200)
                    stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = true, Throttle = DetMath.Min(1, (k - 100) / 100.0) };
                else
                {
                    // The pilot then catches the climb above where it is (never back down onto its own line).
                    if (sharp && double.IsNaN(hold)) pilot.Target = new Double3(0, hold = core.State.Position.Y + 5, 0);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                core.Advance(stick);
                w.Step(core);
                Each?.Invoke(core);
                if (w.O.Broken && StopAtBreak) break;
            }
            return w.O;
        }

        /// <summary>A probe called after each step of the liftoff (forensics; null in the suite), and whether to stop at a break.</summary>
        public static Action<FlightCore> Each;
        public static bool StopAtBreak;

        /// <summary>
        /// "Sudden momentum backwards after lift off" (design 16): the pilot's liftoff to 1.5 m, then at 3 s 3 m/s backward (−z)
        /// over the launch point and the line rising from it for 3 s, then a hover. The known-wrong: design 4's air.
        /// </summary>
        public static Outcome Backward(DroneSpec spec, ulong seed, Action<FlightSetup> edit = null, double speed = 3)
        {
            FlightSetup setup = Setup(spec, WindLevel.Calm, seed, edit);
            double stand = TetherScenarios.Stand(setup);
            return Run(setup, 8, (t, core, pilot) =>
            {
                pilot.Target = new Double3(0, stand + 1.5, t < 3 ? 0 : core.State.Position.Z);
                pilot.HoldVelocity = t >= 3;
                pilot.TargetVelocity = new Double3(0, 0, t < 6 ? -speed : 0);
            });
        }

        // ---- "The draws cover their bounds" ----

        public struct Draws
        {
            /// <summary>Per band (span, peel, irregularity, intact, flaw, abraded): the least and greatest draw, and how many fell in
            /// the band's lowest and highest 1 %.</summary>
            public double[] Least, Most;
            public int[] Low, High, Count;
            /// <summary>Draws outside their band (must be 0), and section reads that changed with the order of use (must be 0).</summary>
            public int Outside, OrderChanged;
            /// <summary>Flawed sections found and the sections scanned for them.</summary>
            public long Flaws, Scanned;
            /// <summary>FiberCuts' span strength against its stream's own draw over the first seeds (must be 0).</summary>
            public int SpanMismatch;
        }

        public static readonly string[] DrawBands = { "span", "peel", "irregularity", "intact", "flaw", "abraded" };

        /// <summary>
        /// Each stream over seeds flights (fiber.span_strength, spool.peel: one draw each) and over sections of one flight
        /// (fiber.section_strength's intact, flaw and abraded bands, spool.irregularity), against the table's bands; the flaws
        /// found by scanning sections until flaws of them; each section read again in reverse order and from a second FiberCuts
        /// that read a different set first.
        /// </summary>
        public static Draws DrawTheStreams(DroneSpec spec, int seeds, int sections, int flaws)
        {
            TetherTable t = Table;
            var bands = new[]
            {
                new[] { t.Row("strength.span_pa").Values[1], t.Row("strength.span_pa").Values[3] },
                t.Row("spool.peel_energy_j_m").Values,
                t.Row("spool.irregularity").Values,
                new[] { t.Row("strength.intact_pa").Values[1], t.Row("strength.intact_pa").Values[3] },
                new[] { t.Row("strength.flaw_pa").Values[1], t.Row("strength.flaw_pa").Values[3] },
                new[] { t.Row("strength.abraded_pa").Values[1], t.Row("strength.abraded_pa").Values[3] },
            };
            var r = new Draws
            {
                Least = new double[6], Most = new double[6], Low = new int[6], High = new int[6], Count = new int[6],
            };
            for (int b = 0; b < 6; b++)
            {
                r.Least[b] = double.PositiveInfinity;
                r.Most[b] = double.NegativeInfinity;
            }
            void Add(int b, double v)
            {
                double lo = bands[b][0], hi = bands[b][1], w = hi - lo;
                if (!(v >= lo && v <= hi)) r.Outside++;
                r.Least[b] = Math.Min(r.Least[b], v);
                r.Most[b] = Math.Max(r.Most[b], v);
                if (v <= lo + 0.01 * w) r.Low[b]++;
                if (v >= hi - 0.01 * w) r.High[b]++;
                r.Count[b]++;
            }
            double[] span = t.Row("strength.span_pa").Values;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                var random = new FlightRandom(seed);
                SeedStream s = random.Open("fiber.span_strength");
                Add(0, random.DrawQuantile(ref s, "span_strength_pa", span));
                SeedStream p = random.Open("spool.peel");
                Add(1, random.Draw(ref p, "peel_energy_j_m", bands[1][0], bands[1][1]));
                if (seed <= 20)
                {
                    var check = new FlightRandom(seed);
                    SeedStream c = check.Open("fiber.span_strength");
                    if (new FiberCuts(t, new FlightRandom(seed)).SpanStrength != check.DrawQuantile(ref c, "x", span)) r.SpanMismatch++;
                }
            }
            // One flight's sections: its FiberLine's irregularity, its FiberCuts' strengths.
            FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Still, cuts: true);
            var core = new FlightCore(setup);
            FiberCuts cuts = core.Tether.Cuts;
            double length = cuts.SectionLength;
            var first = new double[sections];
            for (int k = 0; k < sections; k++)
            {
                Add(2, core.Tether.Irregularity((k + 0.5) * length));
                double v = cuts.Drawn(k, out SectionBand band);
                first[k] = v;
                if (band == SectionBand.Intact) Add(3, v);
                Add(5, cuts.AbradedDraw(k));
            }
            for (long k = 0; r.Flaws < flaws; k++)
            {
                double v = cuts.Drawn(k, out SectionBand band);
                r.Scanned++;
                if (band != SectionBand.Flaw) continue;
                Add(4, v);
                r.Flaws++;
            }
            // The same indices in reverse, and from a second flight's cuts that first read other sections: the same values.
            var again = new FiberCuts(t, new FlightRandom(setup.Seed));
            for (int k = 0; k < 1000; k++) again.Drawn(sections + 7 * k, out _);
            for (int k = sections - 1; k >= 0; k--)
            {
                if (cuts.Drawn(k, out _) != first[k]) r.OrderChanged++;
                if (again.Drawn(k, out _) != first[k]) r.OrderChanged++;
            }
            return r;
        }

        // ---- "The bend loss follows the law" ----

        /// <summary>
        /// A line at tension N wrapped one full turn round a rod of radius m (a helix of 72 capstan nodes, its pitch the line's
        /// width and a little, so the turn is 2π·cos α), its ends led in and out along the tangent; FiberCuts.Collect's one wrap:
        /// returns its macrobend loss, dB, and its turn, rad, and the bends found.
        /// </summary>
        public static (double Loss, double Turn, double Radius, int Bends) WrapRod(double radius, double tension)
        {
            TetherTable t = Table;
            double ea = t.Number("fiber.axial_stiffness_n"), line = t.Number("fiber.coating_diameter_m") / 2;
            const int around = 72;
            double rho = radius + line, pitch = 0.0003;
            int n = around + 3;
            var x = new Double3[n];
            for (int i = 0; i <= around; i++)
            {
                double phi = 2 * Pi * i / around;
                DetMath.SinCos(phi, out double s, out double c);
                x[i + 1] = new Double3(rho * c, rho * s, pitch * i / around);
            }
            // In and out along the helix's tangent at its two ends (φ 0 and 2π: the same direction).
            Double3 tIn = new Double3(0, rho * 2 * Pi, pitch), tOut = tIn;
            x[0] = x[1] - tIn * (1 / tIn.Length()) * 0.05;
            x[n - 1] = x[n - 2] + tOut * (1 / tOut.Length()) * 0.05;
            var m = new double[n];
            for (int i = 1; i < n; i++) m[i] = m[i - 1] + (x[i] - x[i - 1]).Length() / (1 + tension / ea);
            var chain = new CableChain(n, t.Number("fiber.mass_per_m_kg"), ea);
            chain.Reset(x, m, n);
            for (int i = 1; i <= around + 1; i++)
            {
                Double3 p = chain[i].Position, nrm = new Double3(p.X, p.Y, 0);
                nrm = nrm * (1 / nrm.Length());
                CableContact c = CableContact.On(p - nrm * line, nrm, default, 1e4, 0, 1e4, 0.35, 0.28, true);
                c.MemberKind = (byte)FiberMember.Shape;
                c.Member = 1;
                c.Edge = radius;
                c.NormalForce = tension * 2 * Pi / around;
                chain[i].Contact0 = c;
            }
            var cuts = new FiberCuts(t, new FlightRandom(1));
            cuts.Collect(chain, 0, 0, chain[n - 1].Position);
            ref readonly FiberBend b = ref cuts.Bend(0);
            return (b.Macro, b.Turn, b.Radius, cuts.BendCount);
        }

        /// <summary>The law's loss for a full turn at the mandrel's radius r (the table's rows written out apart from FiberCuts).</summary>
        public static double LawLoss(double r)
        {
            TetherTable t = Table;
            double a = t.Number("link.macrobend_loss_a_db"), ra = t.Number("link.macrobend_radius_a_m");
            return a * Math.Exp(-t.Number("link.macrobend_decay_per_m") * (r - ra));
        }

        // ---- "Each mechanism on its own": the bend and the rub over a fixed arris ----

        public struct Arris
        {
            public bool Broken;
            public TetherMechanism Mechanism;
            public double Time, Stress, Strength, Turn, Tension, Radius, Slid;
            /// <summary>The wear's depth at the break's section when it broke and the step it wore through (−1: never), m and s.</summary>
            public double Depth, WornAt;
            public int Sections;
            public long Wedges;
        }

        /// <summary>
        /// A line over one arris (a capstan node on a fixed member: a snag-like shape of edge m, μ 0.30/0.25, turning by degrees),
        /// its low end pulled by a dead force low (N), its high end by a 100 N/m spring whose end walks away at speed (m/s) and
        /// oscillates by amplitude (m) at hz (sawing), for seconds or to a break; the cuts (FiberCuts) each step with the contacts
        /// as the log's names.
        /// </summary>
        public static Arris OverAnArris(double edge, double degrees, double low, double speed, double amplitude, double hz, double seconds, ulong seed)
        {
            TetherTable t = Table;
            double theta = degrees * Pi / 180;
            DetMath.SinCos(theta, out double sin, out double cos);
            Double3 dIn = new Double3(1, 0, 0), dOut = new Double3(cos, -sin, 0);
            Double3 normal = dIn - dOut;
            normal = normal * (1 / normal.Length());
            double ea = t.Number("fiber.axial_stiffness_n");
            var chain = new CableChain(3, t.Number("fiber.mass_per_m_kg"), ea);
            chain.Reset(new[] { dIn * -5, default, dOut * 5 }, new[] { 0.0, 5.0, 10.0 }, 3);
            chain[0].Force = dIn * -low;
            CableContact c = CableContact.On(default, normal, default, 1e4, 0, 1e4, 0.30, 0.25, true);
            c.MemberKind = (byte)FiberMember.Shape;
            c.Member = (7UL << 32) | 3;
            c.Edge = edge;
            chain[1].Contact0 = c;
            chain[2].AnchorStiffness = 100;
            Double3 load0 = dOut * (5 + low * Math.Exp(0.3 * theta) / 100);
            var contacts = new FiberContacts(t, new FlightSetup { Tether = t }, FlightWorld.Floor(), null, t.Number("fiber.coating_diameter_m") / 2,
                t.Number("fiber.mass_per_m_kg"), 100, 1, Dt);
            var cuts = new FiberCuts(t, new FlightRandom(seed));
            var events = new StepEvents(64);
            var r = new Arris { WornAt = -1 };
            int steps = (int)(seconds / Dt);
            for (int k = 1; k <= steps; k++)
            {
                events.Clear();
                double time = k * Dt;
                DetMath.SinCos(2 * Pi * hz * time, out double ws, out double wc);
                chain[2].AnchorTarget = load0 + dOut * (speed * time + amplitude * ws);
                chain[2].AnchorVelocity = dOut * (speed + amplitude * 2 * Pi * hz * wc);
                ref CableContact slot = ref chain[1].Contact0;
                slot.Through = chain[1].Material;
                chain.Step(Dt);
                cuts.Collect(chain, 0, 0, chain[2].Position);
                if (cuts.Wear(chain, Dt, contacts, events) && r.WornAt < 0) r.WornAt = time;
                if (cuts.Check(chain, false, 0, 0, 0, contacts, events, out FiberBreak b))
                {
                    r.Broken = true;
                    r.Mechanism = b.Mechanism;
                    r.Time = time;
                    r.Stress = b.Stress;
                    r.Strength = b.Strength;
                    r.Turn = b.Turn;
                    r.Tension = b.Tension;
                    r.Radius = b.Radius;
                    r.Depth = cuts.Wear(b.Section);
                    break;
                }
            }
            r.Slid = chain[1].Material - 5;
            r.Sections = cuts.Sections;
            r.Wedges = chain.Wedges;
            return r;
        }

        // ---- "Each mechanism on its own": the line held on the drone, the drone climbing ----

        /// <summary>The coil with metres of line left (so it runs out and the line holds the drone at the exit), its line laid
        /// 100 m straight back along the ground; the drone up to 2 m, then flying on along +z at 2 m/s for seconds, its line straight
        /// behind it (the lip barely turned): what broke the line.</summary>
        public static Outcome HeldClimb(DroneSpec spec, ulong seed, double left = 2, double seconds = 12)
        {
            FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Still, coilLeft: left, seed: seed, cuts: true,
                layout: new[] { new Double3(0, 0, -100), new Double3(0, 0, -1) });
            double stand = TetherScenarios.Stand(setup);
            return Run(setup, seconds, (t, core, pilot) =>
            {
                pilot.HoldVelocity = t >= 2;
                pilot.TargetVelocity = new Double3(0, 0, 2);
                pilot.Target = new Double3(0, stand + DetMath.Min(2, t), t < 2 ? 0 : core.State.Position.Z);
            });
        }

        // ---- "From the break to the disarm" and "Every link signal is live": a disc cuts the line in a hover ----

        public struct Disarm
        {
            public long BreakStep, BrokenStep, LostStep, Stage1Step, Stage2Step, DisarmStep, FeedLossStep, FieldOfBreak;
            public TetherMechanism Mechanism;
            public int Corner;
            /// <summary>Steps after the break the controller's commands moved with the pilot's sticks before stage 1 (must be 0),
            /// and those whose sticks were not centred in stage 1 (must be 0).</summary>
            public int SticksReached, Stage1NotCentred;
            public bool DisarmedAtEnd;
            /// <summary>The field signals against the steps' own means, maxima and least: the worst relative error, the fields
            /// checked; the break's and the dips' events found in their fields.</summary>
            public double FieldError;
            public int Fields, BreakEventsInField;
            public double HeightAtBreak;
            /// <summary>The near piece: the line's force on the drone after the break against the exit's tension along its segment
            /// (the worst error, N), and the far piece's points moved after the break (the most, m).</summary>
            public double ForceError, FarMoved;
            public int NearNodes;
            public bool Attached;
        }

        /// <summary>
        /// Flies the sharp reverse 20 m up (up at 2 m/s, +z to 15 m/s from 10 s, a full reverse to 8 m/s back at 16 s), so the
        /// slack ahead of the drone is drawn into a disc and cut there (a hand placing a node in a disc instead stretches the stiff
        /// glass to tens of newtons and breaks it by tension first): the break, the failsafe's stages, the disarm and the feed's
        /// signals field by field, for 4 s after the break.
        /// </summary>
        public static Disarm CutByADisc(DroneSpec spec, ulong seed = 1, Action<FlightSetup> edit = null)
        {
            FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Calm, seed: seed, cuts: true);
            edit?.Invoke(setup);
            double stand = TetherScenarios.Stand(setup);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            var r = new Disarm { BreakStep = -1, BrokenStep = -1, LostStep = -1, Stage1Step = -1, Stage2Step = -1, DisarmStep = -1, FeedLossStep = -1, FieldOfBreak = -1 };
            double sumT = 0, maxT = 0, leastB = double.PositiveInfinity, sumM = 0, leastM = double.PositiveInfinity;
            int count = 0;
            Double3[] far = null;
            int farFrom = 0;
            Axes3 lastCommand = default;
            for (int k = 0; k < 26000 && (r.BreakStep < 0 || core.State.Step < r.BreakStep + 4000); k++)
            {
                StickSample stick;
                double t = k * Dt;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    // The sharp reverse 20 m up (SharpReverse's course): the slack the spool cannot take back drawn into a disc.
                    double y = stand + DetMath.Min(20, 2 * t);
                    pilot.HoldVelocity = t >= 10;
                    pilot.MaxTiltSin = t < 16 ? 0.5736 : 0.866;
                    pilot.TargetVelocity = new Double3(0, 0, t < 16 ? 15 : -8);
                    pilot.Target = new Double3(0, y, t < 10 ? 0 : core.State.Position.Z);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                    // The pilot keeps flying after the break: a slow stick sweep the controller must not see.
                    if (r.BreakStep >= 0) stick.Roll = 0.3 * Math.Sin((k - r.BreakStep) * 0.01);
                }
                FiberLine f = core.Tether;
                core.Advance(stick);
                ref readonly FlightState s = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    ref readonly FlightEvent e = ref core.Events[i];
                    if (e.Kind == EventKind.TetherBreak)
                    {
                        r.BreakStep = s.Step;
                        r.Mechanism = (TetherMechanism)(e.Index & 0x0F);
                        r.HeightAtBreak = s.Position.Y;
                        r.Corner = (int)f.LastBreak.Member;
                    }
                    if (e.Kind != EventKind.Failsafe) continue;
                    if (e.Index == 0) r.LostStep = s.Step;
                    if (e.Index == 1) r.Stage1Step = s.Step;
                    if (e.Index == 2) r.Stage2Step = s.Step;
                    if (e.Index == 3) r.DisarmStep = s.Step;
                }
                if (f.Broken && r.BrokenStep < 0 && f.Link.State == LinkState.Broken) r.BrokenStep = s.Step;
                // The controller's view after the break: its commands frozen at the last good frame's until stage 1.
                if (r.BreakStep >= 0 && s.Step == r.BreakStep) lastCommand = s.Fc.RcCommand;
                if (r.BreakStep >= 0 && s.Step > r.BreakStep && r.Stage1Step < 0)
                    if (s.Fc.RcCommand.Roll != lastCommand.Roll || s.Fc.RcCommand.Pitch != lastCommand.Pitch || s.Fc.RcCommand.Yaw != lastCommand.Yaw) r.SticksReached++;
                if (r.Stage1Step >= 0 && r.Stage2Step < 0 && (s.Fc.RcCommand.Roll != 0 || s.Fc.RcCommand.Pitch != 0 || s.Fc.RcCommand.Yaw != 0)
                    && s.Step > r.Stage1Step + FlightController.LoopsPerRcFrame / FlightController.LoopsPerStep) r.Stage1NotCentred++;
                // The near and far pieces.
                if (f.Broken && far == null)
                {
                    farFrom = f.TrailBeforeBreak;
                    far = new Double3[f.TrailCount - farFrom];
                    for (int i = 0; i < far.Length; i++) far[i] = f.Trail[farFrom + i];
                    r.NearNodes = f.Attached ? f.Chain.Count : 0;
                }
                if (far != null)
                {
                    for (int i = 0; i < far.Length; i++) r.FarMoved = Math.Max(r.FarMoved, (f.Trail[farFrom + i] - far[i]).Length());
                    if (f.Attached && s.Step > r.BreakStep)
                    {
                        CableChain line = f.Chain;
                        int n = line.Count;
                        Double3 outward = line[n - 2].Position - line[n - 1].Position;
                        double sdot = line.SpoolSpeed, pull = line.SpoolExitTension - (sdot > 0 ? f.MassPerMetre * sdot * sdot : 0);
                        Double3 expected = outward * (pull / outward.Length());
                        r.ForceError = Math.Max(r.ForceError, (f.Force - expected).Length());
                    }
                    r.Attached = f.Attached;
                }
                // The field window, written out apart from FlightCore's, and the published signals at each field's end.
                sumT += f.ExitTension;
                maxT = Math.Max(maxT, f.ExitTension);
                leastB = Math.Min(leastB, f.MinBendRadius);
                sumM += f.Link.Margin;
                leastM = Math.Min(leastM, f.Link.Margin);
                bool breakHere = f.BrokeThisStep;
                if (breakHere) r.FieldOfBreak = s.Step;
                if (++count == FlightCore.FieldSteps)
                {
                    core.GetSignals(out FeedSignals sig);
                    double Rel(double a, double b) => a == b ? 0 : Math.Abs(a - b) / Math.Max(Math.Abs(b), 1e-300);
                    if (!double.IsInfinity(leastM))
                    {
                        r.FieldError = Math.Max(r.FieldError, Rel(sig.FiberTensionN, sumT / count));
                        r.FieldError = Math.Max(r.FieldError, Rel(sig.FiberTensionMaxN, maxT));
                        r.FieldError = Math.Max(r.FieldError, Rel(sig.LinkMarginDb, sumM / count));
                        r.FieldError = Math.Max(r.FieldError, Rel(sig.LinkMarginLeastDb, leastM));
                        if (!double.IsInfinity(leastB)) r.FieldError = Math.Max(r.FieldError, Rel(sig.FiberMinBendRadiusM, leastB));
                        r.Fields++;
                    }
                    if (r.FeedLossStep < 0 && sig.LinkState == LinkState.Broken && double.IsNegativeInfinity(sig.LinkMarginLeastDb))
                    {
                        r.FeedLossStep = s.Step;
                        if (sig.TetherBreakStep == r.BreakStep) r.BreakEventsInField++;
                    }
                    if ((sig.Live & (FeedSignalSet.FiberTension | FeedSignalSet.FiberMinBendRadius | FeedSignalSet.LinkState | FeedSignalSet.LinkMargin))
                        != (FeedSignalSet.FiberTension | FeedSignalSet.FiberMinBendRadius | FeedSignalSet.LinkState | FeedSignalSet.LinkMargin)) r.FieldError = double.NaN;
                    sumT = maxT = sumM = 0;
                    leastB = leastM = double.PositiveInfinity;
                    count = 0;
                }
            }
            r.DisarmedAtEnd = !core.State.Armed;
            return r;
        }

        // ---- "A line draped over a coil run holds" and "A reverse round a snagged barb cuts" ----

        /// <summary>The stub's floor with a set of synthetic snag records SnagsNear returns (instance 9): the concertina coil's
        /// barbed tape as the fortifications reading gives it (edge_radius_m 0.00025, barb_pitch_m 0.035).</summary>
        public sealed class SnagWorld : IWorldQuery
        {
            readonly FloorWorld _floor = new FloorWorld(0, "belt_bare", "steel", "timber", "razor_wire");
            public readonly SnagSegment[] Segments;

            public SnagWorld(SnagSegment[] segments) => Segments = segments;

            public ulong ContentHash => _floor.ContentHash ^ 0x7D18;
            public int QueryVersion => _floor.QueryVersion;
            public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results) => _floor.SampleGround(points, results);
            public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results, QueryFilter filter = QueryFilter.None) =>
                _floor.StaticContacts(previous, current, margin, results, filter);
            public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits, QueryFilter filter = QueryFilter.None) =>
                _floor.Raycast(rays, maxDistance, hits, filter);
            public int WiresNear(Double3 centre, double radius, Span<Conductor> results) => _floor.WiresNear(centre, radius, results);
            public int MicroDetailNear(Double3 centre, double radius, MicroKind kinds, Span<MicroElement> results) => 0;
            public bool Geometry(int obj, int shape, out ShapeGeometry geometry) => _floor.Geometry(obj, shape, out geometry);
            public bool Feature(int obj, int shape, out GroundFeature feature) => _floor.Feature(obj, shape, out feature);
            public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes) => _floor.AddObject(name, shapes);
            public void ResetRuntimeObjects() => _floor.ResetRuntimeObjects();
            public string ObjectName(int obj) => _floor.ObjectName(obj);
            public string MaterialName(ushort material) => _floor.MaterialName(material);
            public ushort MaterialId(string name) => _floor.MaterialId(name);
            public string SurfaceName(byte surface) => _floor.SurfaceName(surface);

            public int SnagsNear(Double3 centre, double radius, Span<SnagSegment> results)
            {
                int count = 0;
                foreach (SnagSegment g in Segments)
                {
                    double reach = radius + g.Radius;
                    if (!(FiberContacts.PointSegment(centre, g.A, g.B) <= reach * reach)) continue;
                    if (count < results.Length) results[count] = g;
                    count++;
                }
                return count;
            }
        }

        /// <summary>A concertina run along z from z0 (the flight's way, so a line drawn along it lies over every loop's top), its
        /// coil 0.9 m across, 0.15 m a loop, as 12 records of 24 segments (one a loop), each loop's tape barbed every 35 mm (the
        /// fortifications reading: edge 0.25 mm), of material. (Across a single helix a line in one plane meets one point a loop
        /// and falls through between them.)</summary>
        public static SnagSegment[] Concertina(double z0, ushort material)
        {
            var list = new List<SnagSegment>();
            const double r = 0.45, loop = 0.15;
            const int per = 24;
            for (int rec = 0; rec < 12; rec++)
                for (int i = 0; i < per; i++)
                {
                    double a0 = 2 * Pi * i / per, a1 = 2 * Pi * (i + 1) / per;
                    double s0 = z0 + loop * (rec + (double)i / per), s1 = z0 + loop * (rec + (double)(i + 1) / per);
                    list.Add(new SnagSegment
                    {
                        Object = 9, Record = rec, Segment = i, Kind = SnagKind.Edge, Radius = 0.0125, EdgeRadius = 0.00025, BarbPitch = 0.035,
                        Material = material,
                        A = new Double3(r * Math.Sin(a0), r + r * Math.Cos(a0), s0),
                        B = new Double3(r * Math.Sin(a1), r + r * Math.Cos(a1), s1),
                    });
                }
            return list.ToArray();
        }

        public static bool CoilProbe;

        public struct Coil
        {
            public Outcome O;
            /// <summary>The most a barb's contact point moved along its record from its first touch, m; the steps the line touched
            /// the coil; the largest stress at a barb and the least strength of a section at a barb, Pa; the most turn at a barb.</summary>
            public double MostMoved, MostStress, LeastStrength, MostTurn;
            public long Touching;
        }

        /// <summary>
        /// The drone climbs to 1.2 m from the floor with its line laid 3 m behind, flies +z along a concertina run (z 3.1–4.9) to z
        /// 6, comes down to 0.5 m there so its line from the anchor (near straight under the spool's pull: 13 m sag only some 0.15 m
        /// at 0.1 N) lies over the loops, and drifts on at 0.3 m/s; or (reverse) climbs to 1.6 m and flies back over the coil to z −2,
        /// so the line snagged on the barbs is turned round them.
        /// </summary>
        public static Coil OverTheCoil(DroneSpec spec, ulong seed, bool reverse, double seconds = 16)
        {
            FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Calm, seed: seed, cuts: true);
            var floorish = new SnagWorld(null);
            ushort steel = floorish.MaterialId("steel");
            var world = new SnagWorld(Concertina(3.1, steel));
            setup.World = new FlightWorld(world);
            double stand = TetherScenarios.Stand(setup);
            var res = new Coil { LeastStrength = double.PositiveInfinity };
            var w = new Watch(setup);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            int steps = (int)(seconds * 1000);
            for (int k = 0; k < steps; k++)
            {
                double t = k * Dt;
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    // Up to 1.2 m, along +z over the coil to z 6, down to 0.5 m there (the line from its anchor, near straight under the
                    // spool's pull, then lies over the loops), drifting on at 0.3 m/s; or (reverse) up to 1.6 m and back over the coil to z −2.
                    double y = stand + (t < 6.5 ? DetMath.Min(1.2, 1.0 * t) : t < 8.5 ? 1.2 - 0.35 * (t - 6.5) : !reverse || t < 10 ? 0.5 : DetMath.Min(1.6, 0.5 + (t - 10)));
                    double z = t < 2 ? 0 : t < 6.5 ? 6 * (t - 2) / 4.5 : t < 10 ? 6 : reverse ? (t < 11.2 ? 6 : Math.Max(-2, 6 - 3 * (t - 11.2))) : Math.Min(8, 6 + 0.3 * (t - 10));
                    pilot.HoldVelocity = false;
                    pilot.Target = new Double3(0, y, z);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                core.Advance(stick);
                w.Step(core);
                FiberLine f = core.Tether;
                if (f.Broken) break;
                CableChain line = f.Chain;
                if (CoilProbe && k % 250 == 0)
                {
                    double least = double.PositiveInfinity;
                    int at = -1;
                    for (int i = 0; i < line.Count; i++)
                        foreach (SnagSegment g in world.Segments)
                        {
                            double d = Math.Sqrt(FiberContacts.PointSegment(line[i].Position, g.A, g.B));
                            if (d < least) { least = d; at = i; }
                        }
                    Console.WriteLine($"t {t:F2} drone {core.State.Position.Y:F2} z {core.State.Position.Z:F2}; nodes {line.Count}, trail {f.TrailCount}, paid {f.PaidOut:F2}; the line's nearest node {at} "
                        + $"({(at >= 0 ? line[at].Position.Y.ToString("F3") + ", " + line[at].Position.Z.ToString("F3") : "")}) {least * 1e3:F1} mm from a loop; caches' snags {f.Contacts.Cache(0).Snags}/{f.Contacts.Cache(1).Snags}/{f.Contacts.Cache(2).Snags}");
                }
                bool touch = false;
                for (int i = 0; i < line.Count; i++)
                {
                    ref CableNode a = ref line[i];
                    foreach (CableContact c in new[] { a.Contact0, a.Contact1 })
                    {
                        if (!c.Active || c.MemberKind != (byte)FiberMember.Snag || !(c.NormalForce > 0)) continue;
                        touch = true;
                        // The touch's place along the record: its hold (the chain's), against where it first touched (its Origin, the
                        // member's reach a line's radius under the hold: that radius taken off).
                        if (c.Engaged) res.MostMoved = Math.Max(res.MostMoved, (c.Hold - c.Origin).Length() - Table.Number("fiber.coating_diameter_m") / 2);
                        double strength = f.Cuts.Strength(f.Cuts.Section(a.Material), out _);
                        res.LeastStrength = Math.Min(res.LeastStrength, strength);
                    }
                }
                if (touch) res.Touching++;
                for (int b = 0; b < f.Cuts.BendCount; b++)
                {
                    ref readonly FiberBend p = ref f.Cuts.Bend(b);
                    if (p.Kind != (byte)FiberMember.Snag) continue;
                    res.MostStress = Math.Max(res.MostStress, p.Stress);
                    res.MostTurn = Math.Max(res.MostTurn, p.Turn);
                }
            }
            res.O = w.O;
            return res;
        }

        // ---- "A snag dips the link" ----

        public struct Dip
        {
            public Outcome O;
            /// <summary>The margin before the wrap, the least, the most bend loss at the twig, dB; the margin's spread while wrapped
            /// (its standard deviation, dB); the dips and those naming the twig; the steps wrapped.</summary>
            public double Before, Least, TwigLoss, Spread, MarginVsLaw;
            public int Dips, DipsNamingTwig, DipEnds;
            public long Wrapped;
        }

        /// <summary>
        /// A standing synthetic twig (2 mm, 1.6 m tall, an element of instance 7) at (0, 0, 4) on the stub's floor; the drone at
        /// 1.4 m (1.2 until model 64) flies past it and orbits it once at 1 m radius, so its line from 3 m behind wraps the twig a turn and more, then
        /// hovers beside it in wind for the rest of seconds.
        /// </summary>
        public static Dip AroundATwig(DroneSpec spec, ulong seed, WindLevel wind, double seconds = 30)
        {
            var twig = new MicroElement
            {
                Id = (7UL << 32) | 42, Kind = MicroKind.Twig, Anchored = true, PointCount = 2,
                P0 = new Double3(0, 0, 4), P1 = new Double3(0, 1.6, 4), P2 = new Double3(0, 1.6, 4), Diameter = 0.002, Parent = -1, ParentShape = -1,
            };
            FlightSetup setup = TetherScenarios.Setup(spec, wind, seed: seed, cuts: true);
            setup.World = new FlightWorld(new ElementWorld(new[] { twig }));
            double stand = TetherScenarios.Stand(setup);
            setup.StartPosition = new Double3(1, stand, 0);
            setup.TetherLayout = new[] { new Double3(1, 0, -3), new Double3(1, 0, -0.5) };
            var res = new Dip { Before = double.NaN, Least = double.PositiveInfinity };
            var w = new Watch(setup);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            double sum = 0, sum2 = 0;
            int steps = (int)(seconds * 1000);
            for (int k = 0; k < steps; k++)
            {
                double t = k * Dt;
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    // Since model 64 at 1.4 m (1.2 before): the canister's exit hangs 0.117 m lower than the old coil's, and at 1.2 m
                    // the line caught on the twig at 1.10 m slides down it to its foot within a turn (the pull from the exit, less
                    // steep, no longer holds it). The wrap is sensitive to the height (swept 1.0–1.5 m on seed 5: it holds at 1.1,
                    // 1.4 and 1.5, slides off at 1.0, 1.2 and 1.3), so this is a brittle snag (a fork would be the sturdier one).
                    double y = stand + DetMath.Min(1.4, 1.0 * t);
                    Double3 target;
                    if (t < 2) target = new Double3(1, y, 0);
                    else if (t < 6) target = new Double3(1, y, (t - 2)); // past the twig on its +x side to z 4
                    else
                    {
                        double phi = DetMath.Min(2.25 * Pi, 0.5 * (t - 6)); // once round and a quarter, then held
                        target = new Double3(Math.Cos(phi), y, 4 + Math.Sin(phi));
                    }
                    pilot.HoldVelocity = false;
                    pilot.Target = target;
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                core.Advance(stick);
                w.Step(core);
                Each?.Invoke(core);
                FiberLine f = core.Tether;
                if (f.Broken) break;
                if (t >= 1.9 && t < 2 && double.IsNaN(res.Before)) res.Before = f.Link.Margin;
                double twigLoss = 0;
                for (int b = 0; b < f.Cuts.BendCount; b++)
                {
                    ref readonly FiberBend p = ref f.Cuts.Bend(b);
                    if (p.Kind == (byte)FiberMember.Twig && p.Member == twig.Id) twigLoss += p.Macro + p.Micro;
                }
                // The margin's law written out: the launch less the length's and every bend's loss.
                double law = Table.Number("link.launch_margin_db") - Table.Number("link.attenuation_db_per_m") * f.Chain[f.Chain.Count - 1].Material;
                for (int b = 0; b < f.Cuts.BendCount; b++) law -= f.Cuts.Bend(b).Macro + f.Cuts.Bend(b).Micro;
                res.MarginVsLaw = Math.Max(res.MarginVsLaw, Math.Abs(law - f.Link.Margin));
                if (twigLoss > 0)
                {
                    res.Wrapped++;
                    sum += f.Link.Margin;
                    sum2 += f.Link.Margin * f.Link.Margin;
                }
                res.TwigLoss = Math.Max(res.TwigLoss, twigLoss);
                res.Least = Math.Min(res.Least, f.Link.Margin);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    ref readonly FlightEvent e = ref core.Events[i];
                    if (e.Kind != EventKind.LinkDip) continue;
                    bool end = (e.Index & 0x80) != 0;
                    if (!end) res.Dips++;
                    else res.DipEnds++;
                    if ((e.Index & 0x7F) == (byte)FiberMember.Twig && (((ulong)(uint)e.Object << 32) | (uint)e.Shape) == twig.Id) res.DipsNamingTwig++;
                }
            }
            if (res.Wrapped > 1)
            {
                double mean = sum / res.Wrapped;
                res.Spread = Math.Sqrt(Math.Max(0, sum2 / res.Wrapped - mean * mean));
            }
            res.O = w.O;
            return res;
        }

        // ---- "A free line costs only its length and its lip" ----

        public struct FreeLine
        {
            /// <summary>The worst gap between the margin and the budget less the length's loss and the lip's (dB), over the steps the
            /// line pressed nothing; those steps and the others; the dips; the worst gap against a line at rest with the same lip
            /// angle and tension (dB); the most body rate, °/s; the length's loss, dB.</summary>
            public double Worst, AtRest, MostRate, LengthLoss, LipLoss;
            public long Free, Touching;
            public int Dips;
            public bool Broken;
            public Outcome O;
            /// <summary>The most body rate before the break, °/s.</summary>
            public double RateBeforeBreak;
        }

        /// <summary>
        /// 2 km of line laid out behind (a layout from 2000 m), the drone 30 m up in Calm, rolling and flipping at 600 °/s (the rates
        /// raised for it) for 30 s: a 0.6 s roll or flip each 2.5 s, the pilot recovering between; then each step's margin against
        /// the budget less the length's attenuation and the lip's bend loss at the logged lip angle and exit tension, and against a
        /// line at rest given that angle and tension.
        /// </summary>
        public static FreeLine Flips(DroneSpec spec, ulong seed = 11)
        {
            FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Calm, seed: seed, cuts: true,
                layout: new[] { new Double3(0, 0, -2000), new Double3(0, 0, -1) });
            FcSettings fc = setup.Settings;
            fc.roll_srate = fc.pitch_srate = 60;
            setup.Settings = fc;
            double stand = TetherScenarios.Stand(setup);
            TetherTable t = Table;
            double launch = t.Number("link.launch_margin_db"), alpha = t.Number("link.attenuation_db_per_m");
            var rest = new FiberCuts(t, new FlightRandom(seed));
            var restChain = new CableChain(2, t.Number("fiber.mass_per_m_kg"), t.Number("fiber.axial_stiffness_n"));
            restChain.Reset(new[] { new Double3(0, 0, -1), default }, new[] { 0.0, 1.0 }, 2);
            var link = new FiberLink(t);
            var res = new FreeLine();
            var w = new Watch(setup);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            for (int k = 0; k < 45000; k++)
            {
                double time = k * Dt;
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    pilot.HoldVelocity = false;
                    pilot.Target = new Double3(0, stand + DetMath.Min(30, 3 * time), 0);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                    double u = time - 15;
                    if (u >= 0 && u < 30 && u % 2.5 < 0.6)
                    {
                        // Rolls, left and right in turn, and every third a flip (pitch).
                        int m = (int)(u / 2.5);
                        bool flip = m % 3 == 2;
                        stick.Roll = flip ? 0 : m % 2 == 0 ? 1 : -1;
                        stick.Pitch = flip ? 1 : 0;
                        stick.Yaw = 0;
                        stick.Throttle = 0.55;
                    }
                }
                core.Advance(stick);
                w.Step(core);
                res.O = w.O;
                if (time < 10) continue;
                FiberLine f = core.Tether;
                if (f.Broken)
                {
                    res.Broken = true;
                    break;
                }
                res.MostRate = Math.Max(res.MostRate, core.State.BodyRate.Length() * 180 / Pi);
                FiberCuts c = f.Cuts;
                ref readonly FiberBend lip = ref c.Bend(c.BendCount - 1);
                double s = f.Chain[f.Chain.Count - 1].Material;
                res.LengthLoss = alpha * s;
                res.LipLoss = Math.Max(res.LipLoss, lip.Macro);
                res.Dips = f.Link.Dips;
                bool touching = false;
                for (int b = 0; b < c.BendCount - 1; b++)
                    touching |= c.Bend(b).Kind != (byte)FiberMember.Ground && c.Bend(b).Kind != (byte)FiberMember.Layer;
                if (touching)
                {
                    res.Touching++;
                    continue;
                }
                res.Free++;
                res.Worst = Math.Max(res.Worst, Math.Abs(f.Link.Margin - (launch - alpha * s - lip.Macro)));
                // The same lip angle and tension on a line at rest: its own bends and its own link.
                rest.Collect(restChain, lip.Turn, lip.Tension, default);
                link.Update(s, rest, 0);
                res.AtRest = Math.Max(res.AtRest, Math.Abs(f.Link.Margin - link.Margin));
            }
            return res;
        }
    }
}
