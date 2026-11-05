using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// Props are parts at rest (p7f-damage 1.3b, design 16, D-082; prop-motor-damage's "A resting prop bears load" and "Arming
    /// with a prop on something moves the drone"): the repository's drone with its legs set down disarmed so that one prop's
    /// blade rests on something rigid, flown on its own seed. The suite asserts them (RestTests); tools/flightbench --damage
    /// prints them. A known-wrong flies each with ContactSpec.BladeRest off (model 50's: the prop passes through at rest).
    /// </summary>
    public static class RestScenarios
    {
        const double DegToRad = 3.141592653589793 / 180;

        /// <summary>Corners' index of the front-left rotor, whose blade 1 points out to the left at the flight's start (every
        /// rotor starts at angle 0: blade 0 along body +x, blade 1 along −x).</summary>
        public const int FrontLeft = 3;

        /// <summary>What a resting flight did: its last step's blade load (N) and deflection (m) on the front-left rotor's
        /// blade 1, the drone's height there (its front-left hub, m), its tilt (°), the touching pairs and the parts they are on,
        /// the strikes, jams and damage events, the blade's mass lost (kg) and least chord share, the body's travel and turn
        /// over the armed phase (m, °) and its fastest speed then (m/s), its crashes and tunnels.</summary>
        public struct Result
        {
            public double Load, Bend, PeakLoad, HubHeight, HubAtArming, Tilt, Lost, LeastChord, TipLeft, Travel, Turn, PeakSpeed, StrikeEnergy, Impulse;
            public int Corner, Touching, Strikes, Jams, Damaged, Notches, Broken, Bent, Crashes, Tunnels;
            public bool BladeTouches, SupportTouches;
            public string Parts, FirstStrikeOn;

            public string Describe() =>
                $"the blade's load {Load:F2} N (peak {PeakLoad:F2}), its deflection {Bend * 1e3:F3} mm; its hub {HubHeight:F4} m up ({HubAtArming:F4} at the arming), tilt {Tilt:F1}°, "
                + $"{Touching} touching pairs on {Parts}; {Strikes} strikes{(FirstStrikeOn != null ? " (first on " + FirstStrikeOn + ")" : "")}, {StrikeEnergy:F4} J, "
                + $"{Impulse:F4} N·s, {Jams} jams, {Damaged} damage events ({Notches} notches, {Broken} breaks, {Bent} sets), {Lost * 1e6:F2} mg lost, "
                + $"least chord share {LeastChord:F4}, tip {TipLeft * 127:F2} mm; moved {Travel * 1e3:F2} mm and turned {Turn:F2}° armed, "
                + $"fastest {PeakSpeed:F3} m/s; {Crashes} crashes, {Tunnels} tunnels";
        }

        // ---- the block: a drone set down on one prop ---------------------------------------------------------------------

        /// <summary>
        /// "A resting prop bears load": the drone on its legs on a rigid floor, a steel block under the front-left prop's blade 1
        /// (its top 7 mm over the undeflected blade's lowest edge, clear of the leg, the motor and the arm), set down disarmed from
        /// its rest height with the blade's edge on the block's top and left 3 s. With the rest on the drone rests tilted on the
        /// blade and three legs; without it (the known-wrong) the blade passes into the block and the drone settles level on all
        /// four, its front-left corner lower. drop lifts the start by that much more (m): a drone dropped onto the blade.
        /// </summary>
        public static Result Block(DroneSpec spec, bool rest = true, double seconds = 3, double drop = 0, Action<FlightSetup> edit = null,
            Action<FlightCore> stepped = null, ulong seed = 0x450F)
        {
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            double stand = SoilScenarios.Standing(spec);
            FlightSetup setup = ContactScenarios.Setup(spec, m, h, true, new Double3(0, stand + BlockLift + drop, 0), seed);
            setup.World = SoilScenarios.Floor("dirt_track");
            setup.RuntimeObjects = new[] { new RuntimeObject("block", new[] { BlockShape(setup.World) }) };
            setup.Contacts.BladeRest = rest;
            edit?.Invoke(setup);
            var core = new FlightCore(setup);
            var r = new Result { Corner = FrontLeft, LeastChord = 1, TipLeft = 1 };
            for (int k = 0; k < (int)(seconds / FlightCore.Dt); k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                stepped?.Invoke(core);
                Count(spec, core, ref r);
            }
            End(spec, core, ref r);
            return r;
        }

        /// <summary>The block's top, m over the floor: the undeflected blade's lowest edge (the disc at 0.288 m) plus 7 mm.</summary>
        public const double BlockTop = 0.295;

        /// <summary>The start's lift over the drone's rest height, m: the blade's trailing edge (its capsule's underside, 2.5 mm
        /// under the disc there) just over the block's top.</summary>
        public const double BlockLift = 0.0095;

        static ShapeGeometry BlockShape(FlightWorld world) => new ShapeGeometry
        {
            Kind = ShapeKind.Box, Center = new Double3(-0.265, BlockTop - 0.05, 0.16), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
            AxisZ = new Double3(0, 0, 1), HalfExtents = new Double3(0.04, 0.05, 0.03), Material = world.Query.MaterialId("steel"),
        };

        // ---- R01's roof rack: the pilot's case --------------------------------------------------------------------------

        /// <summary>The roof rack's side rail of the range's R01_A that the drone leans on: the steel tube along the car (its axis
        /// along world x) on the −z side; its object and shape, centre, radius and half-length.</summary>
        public static void Rail(out int obj, out int shape, out ShapeGeometry g)
        {
            MapWorld w = RangeScenarios.World;
            obj = RangeScenarios.Placed("R01_A");
            shape = -1;
            g = default;
            for (int s = 0; s < 1000 && w.Geometry(obj, s, out ShapeGeometry x); s++)
            {
                if (x.Kind != ShapeKind.Capsule || w.MaterialName(x.Material) != "steel" || !(x.Radius < 0.012) || !(DetMath.Abs(x.AxisY.X) > 0.99)) continue;
                if (shape < 0 || x.Center.Z < g.Center.Z)
                {
                    shape = s;
                    g = x;
                }
            }
            if (shape < 0) throw new InvalidOperationException("R01_A has no rack rail along the car (the map moved)");
        }

        /// <summary>How the drone is set down by the rail (D-082's case): turned so its left side faces the rail, 36° off it, rolled
        /// left onto its left skid on the roof, the rear-left prop's blade 1 over the rail RackRadius from its hub. Statics pick
        /// it (a scan of 60 set-downs, p7f-damage 1.3b's note): steel's μ_s 0.35 holds a blade across a round tube only below a
        /// 19° slope of its line, so at the roof's 36–53° leans a blade across the rail by its span slides down it; this pose rests
        /// on the skid, the legs and the pack with the blade touching the rail lightly, and arming turns it.</summary>
        public const double RackYaw = -90 - 36, RackRadius = 0.04;

        /// <summary>The rotor whose blade rests on the rail (Corners' order).</summary>
        public const int RackCorner = RearLeft;

        /// <summary>Corners' index of the rear-left rotor (counter-clockwise; its blade 1 points out to the left at the start).</summary>
        public const int RearLeft = 2;

        /// <summary>The start's clearances, m: the left skid over the roof and the blade's edge over the rail.</summary>
        public const double RackGap = 0.002;

        /// <summary>
        /// The pose that sets the drone down on R01_A's roof by the rail (RackYaw, RackRadius): its roll found by bisection so the
        /// left skid's lowest support and the rotor's blade 1 trailing edge (its capsule's underside at radius, from
        /// props.blade's chord and pitch there) clear the roof (raycast) and the rail's top by the same gap.
        /// </summary>
        public static void RackPose(DroneSpec spec, out Double3 position, out Quat attitude, out double roll, double yaw = RackYaw, double radius = RackRadius,
            int corner = RackCorner)
        {
            Rail(out _, out _, out ShapeGeometry rail);
            MapWorld w = RangeScenarios.World;
            ContactShape[] shapes = spec.Contacts.Shapes;
            PropBlade b = spec.Blade;
            double R = spec.Props.Diameter / 2, x = radius / R;
            int k = (int)((x - (b.Stations[0] - b.Span / 2)) / b.Span);
            double half = 0;
            double[] ends = ContactSpec.BladeSegmentEnds;
            int g = 0;
            while (g < ends.Length - 1 && !(x < ends[g])) g++;
            double from = g == 0 ? b.Stations[0] - b.Span / 2 : ends[g - 1];
            for (int j = 0; j < b.Stations.Length; j++)
                if (b.Stations[j] + b.Span / 2 > from && b.Stations[j] - b.Span / 2 < ends[g]) half = DetMath.Max(half, b.Thickness[j] / 2);
            double off = b.Chord[k] / 2 - half;
            DetMath.SinCos(b.SectionAngle[k], out double st, out double ct);
            Double3 hub = spec.Motors[corner].Position;
            // Blade 1 of a left rotor at angle 0 points along body −x; its motion there is +z for a clockwise rotor, −z for the
            // other, the trailing edge behind it and below the mid-chord.
            double ahead = spec.Motors[corner].Clockwise ? 1 : -1;
            Double3 edge = hub + new Double3(-radius, -off * st, -ahead * off * ct);
            double lo = 5 * DegToRad, hi = 80 * DegToRad;
            position = default;
            attitude = default;
            roll = 0;
            for (int it = 0; it < 60; it++)
            {
                roll = 0.5 * (lo + hi);
                attitude = Attitude(roll, yaw);
                Double3 e = attitude.Rotate(edge);
                double px = rail.Center.X - e.X, pz = rail.Center.Z - e.Z;
                double skid = double.PositiveInfinity, roof = double.NegativeInfinity;
                foreach (ContactShape s in shapes)
                {
                    if (s.Role != ContactRole.Support || s.Part != spec.PartIndex("skid_left")) continue;
                    Double3 a = attitude.Rotate(s.A);
                    skid = DetMath.Min(skid, a.Y - s.Radius);
                    roof = DetMath.Max(roof, Roof(w, px + a.X, pz + a.Z));
                }
                // Both at the same body height: the skid's gap over the roof equals the edge's over the rail.
                double bySkid = roof + RackGap - skid, byRail = rail.Center.Y + rail.Radius + half + RackGap - e.Y;
                position = new Double3(px, bySkid, pz);
                if (bySkid > byRail) lo = roll; // the edge over the rail by more than the skid over the roof: roll on
                else hi = roll;
            }
        }

        static Quat Attitude(double roll, double yaw)
        {
            DetMath.SinCos(yaw * DegToRad / 2, out double sy, out double cy);
            DetMath.SinCos(roll / 2, out double sr, out double cr);
            return (new Quat(0, sy, 0, cy) * new Quat(0, 0, sr, cr)).Normalized();
        }

        static readonly Ray[] _ray = new Ray[1];
        static readonly RayHit[] _hit = new RayHit[1];

        static double Roof(MapWorld w, double x, double z)
        {
            _ray[0] = new Ray(new Double3(x, 3, z), new Double3(0, -1, 0));
            w.Raycast(_ray, 3, _hit);
            return 3 - _hit[0].Distance;
        }

        /// <summary>
        /// D-082's case on R01_A's roof (prop-motor-damage's "Arming with a prop on something moves the drone"): set down by the
        /// rail (RackPose) disarmed, left settle s; then, with arm, armed with the sticks centred and the throttle low (Betaflight's
        /// motor idle, small_angle 100: it arms tilted) for armed s. The result's load is the blade's at the arming; its strikes,
        /// damage and the body's travel and turn the armed phase's. rest false is the known-wrong (the prop passes through at
        /// rest). started sees the core before the first step, stepped after each (a recorder's).
        /// </summary>
        public static Result Rack(DroneSpec spec, bool rest = true, bool arm = true, double settle = 3, double armed = 3, ulong seed = 0x450F,
            Action<FlightSetup, FlightCore> started = null, Action<FlightCore, StickSample> stepped = null, double yaw = RackYaw, double radius = RackRadius,
            int corner = RackCorner, Action<FlightSetup> edit = null)
        {
            RackPose(spec, out Double3 position, out Quat attitude, out _, yaw, radius, corner);
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            FlightSetup setup = ContactScenarios.Setup(spec, m, h, true, position, seed);
            setup.World = new FlightWorld(RangeScenarios.World);
            setup.StartAttitude = attitude;
            setup.Contacts.BladeRest = rest;
            edit?.Invoke(setup);
            var core = new FlightCore(setup);
            started?.Invoke(setup, core);
            var r = new Result { Corner = corner, LeastChord = 1, TipLeft = 1 };
            int steps = (int)(settle / FlightCore.Dt);
            for (int k = 0; k < steps; k++) Step(spec, core, new StickSample(), stepped, ref r, false);
            End(spec, core, ref r);
            r.HubAtArming = r.HubHeight;
            if (!arm) return r;
            Double3 at = core.State.Position;
            Quat was = core.State.Attitude;
            var result = r;
            result.Strikes = result.Jams = result.Damaged = result.Notches = result.Broken = result.Bent = 0;
            result.StrikeEnergy = result.Impulse = result.Lost = 0;
            for (int k = 0; k < (int)(armed / FlightCore.Dt); k++)
            {
                Step(spec, core, new StickSample { Arm = true }, stepped, ref result, true);
                Double3 d = core.State.Position - at;
                result.Travel = DetMath.Max(result.Travel, d.Length());
                Quat turn = was.Conjugate() * core.State.Attitude;
                double cw = DetMath.Min(1, DetMath.Abs(turn.W));
                result.Turn = DetMath.Max(result.Turn, 2 * DetMath.Acos(cw) / DegToRad);
                result.PeakSpeed = DetMath.Max(result.PeakSpeed, core.State.Velocity.Length());
            }
            double load = r.Load, bend = r.Bend;
            End(spec, core, ref result);
            result.Load = load;
            result.Bend = bend;
            result.HubAtArming = r.HubHeight;
            return result;
        }

        // ---- the two cases over their set-downs (model 60) ------------------------------------------------------------------

        /// <summary>The block's set-downs: the start's lift over BlockLift, m. No draw reaches the block (disarmed, no strength
        /// met), so its seed changes nothing; where the drone comes to rest turns on how it lands and slides there. From 0 to 5
        /// mm the same drone rests one of two ways: the blade near its root holding the front-left corner off the floor, or the
        /// drone slid across until the block meets the blade far out on its span, which bends 6–8 mm and lets the skid's corner
        /// down beside it (model 59, the cantilever legs, 2 of these 6 holding the corner over 3 mm; model 60, the frame, 1).</summary>
        public static readonly double[] BlockDrops = { 0, 0.0005, 0.001, 0.002, 0.003, 0.005 };

        /// <summary>The block over BlockDrops: on how many the blade bears the drone (in contact, over 2 N, bent, at a stiffness
        /// of 500–12 000 N/m, undamaged, no crash or tunnel), holds its corner off the floor (the front-left skid's support
        /// clear), and rests over the known-wrong's height there; on how many the known-wrong bears nothing and rests on its
        /// skid; the least and most height over the known-wrong (m).</summary>
        public struct BlockSweep
        {
            public int N, Bears, Holds, Over, WrongOnItsSkid;
            public double LeastOver, MostOver;
        }

        public static BlockSweep Blocks(DroneSpec spec, Action<FlightSetup> edit = null, Action<double, Result, Result> each = null)
        {
            var w = new BlockSweep { LeastOver = double.PositiveInfinity, MostOver = double.NegativeInfinity };
            foreach (double drop in BlockDrops)
            {
                Result r = Block(spec, drop: drop, edit: edit), wrong = Block(spec, rest: false, drop: drop, edit: edit);
                each?.Invoke(drop, r, wrong);
                w.N++;
                if (r.BladeTouches && r.Load > 2 && r.Bend > 0 && r.Load / r.Bend >= 500 && r.Load / r.Bend <= 12000 && r.Damaged == 0
                    && r.Crashes + r.Tunnels == 0) w.Bears++;
                if (r.BladeTouches && !r.SupportTouches) w.Holds++;
                double over = r.HubHeight - wrong.HubHeight;
                if (over > 0) w.Over++;
                w.LeastOver = DetMath.Min(w.LeastOver, over);
                w.MostOver = DetMath.Max(w.MostOver, over);
                if (wrong.Load == 0 && wrong.SupportTouches) w.WrongOnItsSkid++;
            }
            return w;
        }

        /// <summary>The rack's seeds: each flight's own draws (the motors' Kv, the ESCs, the blades' and legs' strengths), 1 to
        /// RackSeeds. Which surface an idle blade meets first, the rail's steel or the roof's sheet beside it, and whether its
        /// first blows crush its leading edge, turn on the pose the drone settles to within a millimetre (7 of 20 first on the
        /// rail on model 59 and on model 60; a crushed edge on 9 and 13 of 20).</summary>
        public const int RackSeeds = 20;

        /// <summary>The rack over its seeds: on how many the drone rests on the blade disarmed (over 1 N), armed the blade
        /// strikes, chips (a damage event and material lost) and crushes its leading edge (a notch), the drone moves (over 1 mm,
        /// no crash or tunnel), and it rests over the known-wrong's height (over 3 mm); the first strike on the rail's steel;
        /// on how many the known-wrong bears nothing; and the crashes (a drone the strikes drive off the roof falls 1.5 m) and
        /// the tunnels on all.</summary>
        public struct RackSweep
        {
            public int N, Rests, Strikes, Chips, Notches, Moves, Over, OnTheRail, WrongBearsNothing, Crashes, Tunnels;
        }

        public static RackSweep Racks(DroneSpec spec, Action<FlightSetup> edit = null, Action<ulong, Result, Result> each = null)
        {
            var w = new RackSweep();
            for (ulong seed = 1; seed <= RackSeeds; seed++)
            {
                Result r = Rack(spec, seed: seed, edit: edit), wrong = Rack(spec, rest: false, arm: false, seed: seed, edit: edit);
                each?.Invoke(seed, r, wrong);
                w.N++;
                if (r.Load > 1) w.Rests++;
                if (r.Strikes > 0) w.Strikes++;
                if (r.Damaged > 0 && r.Lost > 0) w.Chips++;
                if (r.Notches > 0) w.Notches++;
                if (r.Travel > 0.001) w.Moves++;
                if (r.HubAtArming - wrong.HubHeight > 0.003) w.Over++;
                if (r.FirstStrikeOn == "R01_A (steel)") w.OnTheRail++;
                if (wrong.Load == 0) w.WrongBearsNothing++;
                w.Crashes += r.Crashes + wrong.Crashes;
                w.Tunnels += r.Tunnels + wrong.Tunnels;
            }
            return w;
        }

        static void Step(DroneSpec spec, FlightCore core, StickSample stick, Action<FlightCore, StickSample> stepped, ref Result r, bool armed)
        {
            stick.HostUs = core.State.Step * FlightCore.StepUs;
            core.Advance(stick);
            stepped?.Invoke(core, stick);
            Count(spec, core, ref r);
        }

        static void Count(DroneSpec spec, FlightCore core, ref Result r)
        {
            r.PeakLoad = DetMath.Max(r.PeakLoad, core.Trace.BladeLoad1[r.Corner]);
            for (int i = 0; i < core.Events.Count; i++)
            {
                FlightEvent e = core.Events[i];
                switch (e.Kind)
                {
                    case EventKind.BladeStrike:
                        r.Strikes++;
                        r.StrikeEnergy += e.A;
                        if (r.FirstStrikeOn == null)
                            r.FirstStrikeOn = e.Object == WorldIds.Ground ? "the ground" : core.World.Query.ObjectName(e.Object) + " (" + core.World.Query.MaterialName(e.Material) + ")";
                        break;
                    case EventKind.RotorJammed: r.Jams++; break;
                    case EventKind.BladeDamaged:
                        r.Damaged++;
                        StrikeMode mode = (StrikeMode)(e.Index >> 4);
                        if (mode == StrikeMode.Break) r.Broken++;
                        else if (mode == StrikeMode.Bend) r.Bent++;
                        else if (mode == StrikeMode.Crush) r.Notches++;
                        r.Lost += e.B;
                        break;
                    case EventKind.Crash: r.Crashes++; break;
                    case EventKind.Tunnel: r.Tunnels++; break;
                }
            }
            for (int b = 0; b < BladeStrikes.Slots; b++) r.Impulse += core.State.BladeStrikes[b].Impulse;
        }

        // The last step's state: the blade's load and deflection, the hub's height and tilt, which parts touch.
        static void End(DroneSpec spec, FlightCore core, ref Result r)
        {
            ref readonly FlightState s = ref core.State;
            r.Load = core.Trace.BladeLoad1[r.Corner];
            r.Bend = core.Trace.BladeBend1[r.Corner];
            r.HubHeight = (s.Position + s.Attitude.Rotate(spec.Motors[r.Corner].Position)).Y;
            Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
            r.Tilt = DetMath.Acos(DetMath.Max(-1, DetMath.Min(1, up.Y))) / DegToRad;
            r.Touching = core.Trace.ContactsTouching;
            var parts = new System.Collections.Generic.SortedSet<string>();
            ContactModel c = core.Contacts;
            for (int i = 0; i < c.ShapeCount; i++)
            {
                if (c.ShapeOff(i) || c.Shape(i).Role == ContactRole.Disc) continue;
                if (Touches(c, i))
                {
                    parts.Add(spec.Parts[c.Shape(i).Part].Name);
                    if (c.Shape(i).Role == ContactRole.Blade && c.Shape(i).Corner == r.Corner) r.BladeTouches = true;
                    if (c.Shape(i).Role == ContactRole.Support && c.Shape(i).Corner == r.Corner) r.SupportTouches = true;
                }
            }
            r.Parts = string.Join(", ", parts);
            BladeState blade = core.Propulsion.Rotor(r.Corner).Blade(1);
            r.TipLeft = blade.TipRadius;
            for (int k = 0; k < blade.ChordShare.Length; k++) r.LeastChord = DetMath.Min(r.LeastChord, blade.ChordShare[k]);
        }

        static bool Touches(ContactModel c, int i) => c.Touching(i);
    }
}
