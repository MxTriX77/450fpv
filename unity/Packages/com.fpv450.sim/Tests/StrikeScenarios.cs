using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7f-damage 1.3's scenarios (prop-motor-damage's "Where a blade meets a shape", "Who yields", "Soft targets", "The strike
    /// in its step"): the repository's drone on the test range (MapWorld, every row of physics_materials.json), its rotors and
    /// contacts driven by a clamped rig or flown by SimPilot. The suite asserts them (StrikeTests); tools/flightbench --damage
    /// prints the first cells.
    /// </summary>
    public static class StrikeScenarios
    {
        public const double DegToRad = 3.141592653589793 / 180;

        /// <summary>Where the rig's runtime targets stand: 60 m over the range's origin, clear of every placed object.</summary>
        public static readonly Double3 Sky = new Double3(0, 60, 0);

        /// <summary>The rotors' speed the rig's scenarios strike at, rad/s: 9000 rpm, the drone's hover (design 4's reference).</summary>
        public const double HoverSpeed = 9000 * 2 * 3.141592653589793 / 60;

        /// <summary>
        /// A drone on a test stand (a clamped rig): FlightCore's propulsion at a fixed duty, its contacts and its strikes, with the
        /// body moved along a scripted pose instead of flown, so a strike's mechanics are read alone. Each Step runs the step's
        /// four propulsion sub-steps, puts the body where the script says (its velocity the script's, its rate 0), senses the
        /// contacts from the last pose and applies the strikes; the strikes' impulses on the body are recorded (DeltaV, DeltaRate)
        /// and then undone unless Free (a body held free in space, no gravity, no thrust on it).
        /// </summary>
        public sealed class Rig
        {
            public readonly FlightCore Core;
            public FlightState Now;
            public double Duty;
            public bool Free;
            public Quat Attitude;
            public readonly StepEvents Events = new StepEvents(256);
            public readonly List<FlightEvent> Log = new List<FlightEvent>();
            public Double3 DeltaV, DeltaRate;
            public Corners SpeedBefore;

            public Rig(FlightSetup setup, double duty)
            {
                Core = new FlightCore(setup);
                Now = Core.State;
                Attitude = setup.StartAttitude;
                Duty = duty;
                for (int i = 0; i < 4; i++)
                {
                    Now.EscCommand[i] = duty;
                    Now.RotorSpeed[i] = HoverSpeed;
                }
                Now.Armed = true;
            }

            /// <summary>One step with the body at position, moving at velocity (world).</summary>
            public void Step(Double3 position, Double3 velocity)
            {
                FlightState s = Now;
                s.BladeStrikes = default;
                Propulsion p = Core.Propulsion;
                p.BeginStep(s);
                var loads = new RotorLoads();
                var duty = new Corners { RearRight = Duty, FrontRight = Duty, RearLeft = Duty, FrontLeft = Duty };
                for (int j = 0; j < FlightController.LoopsPerStep; j++) p.SubStep(ref s, duty, true, FlightController.LoopDt, ref loads);
                s.Position = position;
                s.Velocity = velocity;
                s.Attitude = Attitude;
                s.BodyRate = default;
                s.Step++;
                Events.Clear();
                Core.Contacts.Sense(Now, ref s, Events);
                FlightState pre = s;
                SpeedBefore = s.RotorSpeed;
                Core.Strikes?.Apply(Now.RotorAngle, ref s, Core.Body, Events);
                DeltaV = s.Velocity - pre.Velocity;
                DeltaRate = s.BodyRate - pre.BodyRate;
                if (!Free)
                {
                    s.Velocity = pre.Velocity;
                    s.BodyRate = pre.BodyRate;
                }
                for (int i = 0; i < Events.Count; i++) Log.Add(Events[i]);
                Now = s;
            }

            /// <summary>Holds the body still at position for steps.</summary>
            public void Hold(Double3 position, int steps)
            {
                for (int k = 0; k < steps; k++) Step(position, default);
            }

            public int Count(EventKind kind)
            {
                int n = 0;
                foreach (FlightEvent e in Log)
                    if (e.Kind == kind) n++;
                return n;
            }
        }

        /// <summary>
        /// The lead disc (fl, corner 3) driven from 10 mm short of a face (through p, outward normal n) to depth past it at speed
        /// (m/s) along −n, then held there for hold steps, with the body at attitude and the hub at height y; every pass recorded
        /// (PropStrikes.Record). world, table and rain as Setup's.
        /// </summary>
        public static Rig Into(DroneSpec spec, RuntimeObject[] objects, Double3 p, Double3 n, Quat attitude, double depth, double y, double speed = 0.3,
            int hold = 300, ulong seed = 0x450F, bool strikes = true, bool free = false, FlightWorld world = null, SurfaceTable table = null,
            RainLevel rain = RainLevel.Sunny)
        {
            Double3 at = RimInto(spec, attitude, 3, p, n, depth, y), start = at + n * 0.01;
            var rig = new Rig(Setup(spec, start, attitude, objects, seed, strikes, world, table, rain), HoverDuty(spec)) { Free = free };
            if (rig.Core.Strikes != null) rig.Core.Strikes.Record = new List<PropStrikes.PassRecord>();
            int steps = (int)System.Math.Ceiling(0.01 / speed * 1000);
            Double3 v = (at - start) * (1000.0 / steps);
            for (int k = 1; k <= steps; k++) rig.Step(start + (at - start) * (k / (double)steps), v);
            rig.Hold(at, hold);
            return rig;
        }

        /// <summary>A pass's record in one line.</summary>
        public static string Line(in PropStrikes.PassRecord r)
        {
            const double R = 0.127;
            return $"{r.Mode}{(r.Jammed ? " (jammed)" : "")}: r_m {r.Radius * 1e3:F3} mm, tip {r.TipBefore * R * 1e3:F3} → {r.TipAfter * R * 1e3:F3} mm, "
                + $"work {r.Work:F4} J (used {r.Used:F4}, friction {r.Friction:F4}, fling {r.Fling:F4}), ω {r.SpeedBefore:F2} → {r.SpeedAfter:F2} rad/s, "
                + $"impulse {r.Impulse.Length():F5} N·s, F_crush {r.CrushForce:F1} N, F_bend {r.BendForce:F2} N, peak {r.PeakForce:F2} N, "
                + $"M {r.PeakMoment:F3} N·m ({r.Margin:F3} of its strength) at station {r.Station}, u_n {r.SpeedIn:F2} m/s, sin β {r.Tilt:F3}, wear {r.Wear * 1e9:F4} mm³";
        }

        /// <summary>
        /// The head-on strike on a seed (prop-motor-damage's "A head-on rod breaks the blade"): a 12 mm steel rod standing through
        /// the front-right disc, which crosses its rim in one step to 40 mm inside it at 9000 rpm, then 8 steps held. hit is the
        /// first pass that did work, lost the mass its BladeDamaged events lost (kg), jBefore the rotor's J before.
        /// </summary>
        public static void HeadOn(DroneSpec spec, ulong seed, out Rig rig, out PropStrikes.PassRecord hit, out double lost, out double jBefore)
        {
            const double R = 0.127;
            Double3 hub = spec.Motors[1].Position, origin = Sky;
            Double3 rodAt = origin + hub + new Double3(R - 0.040 + 0.006, 0, 0);
            rig = new Rig(Setup(spec, origin + new Double3(-0.05, 0, 0), Quat.Identity, One(Rod("steel", rodAt, 0.006, 0.3)), seed), HoverDuty(spec));
            rig.Core.Strikes.Record = new List<PropStrikes.PassRecord>();
            jBefore = rig.Core.Propulsion.Inertia(1);
            rig.Step(origin, default);
            rig.Hold(origin, 8);
            hit = rig.Core.Strikes.Record.Find(r => r.Mode != StrikeMode.Glance);
            lost = 0;
            int part = spec.PartIndex("prop_fr");
            foreach (FlightEvent e in rig.Log)
                if (e.Kind == EventKind.BladeDamaged && e.Part == part) lost += e.B;
        }

        /// <summary>
        /// One scripted chip on a seed: the drone pitched 20° toward H01_D's south wall drifts at 0.3 m/s until the front rims are
        /// 0.5 mm in (the rig); the first pass that does work, a slap whose peak force is the tip's own dynamics, not its crush, so
        /// the blade's drawn strength moves the first station it breaks and, past its margin, keeps the blade whole (a crush's
        /// break does not move with it: its force, σ_c·A, and its strength scale alike); and that blade's strength factor.
        /// </summary>
        public static PropStrikes.PassRecord WallChip(DroneSpec spec, ulong seed, out double strength, double pitchDeg = 20, double depth = 0.0005)
        {
            SouthWall(out Double3 face, out Double3 n, out double lo, out double hi);
            Rig rig = Into(spec, null, face, n, Heading(0, pitchDeg * DegToRad), depth, System.Math.Min(hi - 0.5, lo + 1.5), 0.3, 10, seed);
            PropStrikes.PassRecord hit = rig.Core.Strikes.Record.Find(r => r.Mode != StrikeMode.Glance);
            strength = rig.Core.Strikes.Strength(hit.Corner, hit.Blade);
            return hit;
        }

        /// <summary>A flown graze's outcome (prop-motor-damage's "The drone flies on after a wall touch").</summary>
        public struct GrazeResult
        {
            public double Speed, Depth;
            public int Passes, Damaged, Jams, Crashes, Tunnels;
            public double TipA, TipB, EndSpeed, EndHeight, TouchSpeed, HoverCommand, HoverAmps, Band;
            public string CrashBy, TunnelBy;

            public string Describe() =>
                $"graze at {Speed} m/s, the rim {Depth * 1e3} mm in: {Passes} passes, {Damaged} damage events, {Jams} jams, {Crashes} crashes{(CrashBy != null ? " (" + CrashBy + ")" : "")}, "
                + $"{Tunnels} tunnels{(TunnelBy != null ? " (" + TunnelBy + ")" : "")}; fl's blades {TipA * 127:F2} / {TipB * 127:F2} mm; at the end {EndSpeed:F3} m/s, {EndHeight:F2} m up, fl's command {HoverCommand:F4}, "
                + $"{HoverAmps:F2} A, the camera's 50–200 Hz band {Band:F4} m/s²; the first touch at {TouchSpeed:F2} m/s along its normal";
        }

        /// <summary>
        /// SimPilot flies the drone along H01_D's south wall (heading 45°, so the front-left disc leads toward the wall) at speed:
        /// it hovers 2 s 80 mm off the face, 2 m before the face's middle, moves along the wall at speed and, from 1 m before the
        /// middle, closes on it gently (the normal offset held by its own loop, 2/s, at most 0.1 m/s) to where the lead rim, as
        /// new, would be depth into it, holds that until seconds after its first blade strike there, backs off at 1 m/s for 0.5 s
        /// and hovers for hover seconds. The strikes push it as they do and cut the blades back; whatever the pilot asked, the
        /// contact is the physics'. The range's ground is surfaces.json's soil (since model 43; a drone that falls to it carves it).
        /// </summary>
        public static GrazeResult Graze(DroneSpec spec, double speed, double depth, double seconds, double hover, ulong seed, bool strikes = true,
            System.Action<FlightSetup> edit = null, System.Action<FlightSetup, FlightCore> started = null, System.Action<FlightCore, StickSample> stepped = null)
        {
            SouthWall(out Double3 face, out Double3 n, out double lo, out double hi);
            Quat att = Heading(LeadHeading);
            double y = System.Math.Min(hi - 0.5, lo + 1.5);
            Double3 along = new Double3(-n.Z, 0, n.X);
            Double3 inside = RimInto(spec, att, 3, face, n, depth, y);
            Double3 start = inside + n * 0.08 - along * 2.0;
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong mh);
            FlightSetup setup = ContactScenarios.Setup(spec, m, mh, true, start, seed);
            setup.Contacts = spec.BuildContacts(spec.Nominal, m, mh, SoilScenarios.Table()); // the range's soil (p7f-damage 1.3's soil carve)
            setup.World = new FlightWorld(RangeScenarios.World);
            setup.StartAttitude = att;
            setup.Contacts.BladeStrikes = strikes;
            edit?.Invoke(setup);
            var core = new FlightCore(setup);
            started?.Invoke(setup, core);
            for (int k = 0; k < 16; k++) // armed in the air, as CrosswindScenario.Arm
            {
                var arm = new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 };
                core.Advance(arm);
                stepped?.Invoke(core, arm);
            }
            var pilot = new SimPilot(core.Controller, start) { Heading = new Double3(0.7071067811865476, 0, 0.7071067811865476) };
            var r = new GrazeResult { Speed = speed, Depth = depth, TouchSpeed = double.NaN };
            int part = spec.PartIndex("prop_fl");
            const int t0 = 2000, never = int.MaxValue / 2;
            int touched = -1, t2 = never, end = int.MaxValue;
            double commands = 0, amps = 0, band = 0;
            int measured = 0, window = (int)(System.Math.Min(hover, 5) * 1000);
            for (int k = 0; k < end; k++)
            {
                ref readonly FlightState s = ref core.State;
                double offset = Double3.Dot(s.Position - inside, n), past = Double3.Dot(s.Position - inside, along); // 0 at the depth asked, + off the wall
                if (k >= t0 && k < t2 + 500)
                {
                    pilot.HoldVelocity = true;
                    double want = past < -1 ? 0.08 : 0, close = 2 * (want - offset);
                    close = close > 0.1 ? 0.1 : close < -0.1 ? -0.1 : close;
                    pilot.TargetVelocity = k < t2 ? along * speed + n * close : n * 1.0;
                    pilot.Target = new Double3(s.Position.X, start.Y, s.Position.Z);
                    if (k >= t0 + (int)(2.0 / speed * 1000) + 3000 && t2 == never) t2 = k; // never touched: give up
                }
                else if (k == t2 + 500)
                {
                    pilot.HoldVelocity = false;
                    pilot.Target = new Double3(s.Position.X, start.Y, s.Position.Z) + n * 0.3;
                    end = k + (int)(hover * 1000);
                }
                StickSample stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                core.Advance(stick);
                stepped?.Invoke(core, stick);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.BladeStrike && e.Part == part)
                    {
                        r.Passes++;
                        if (touched < 0)
                        {
                            touched = k;
                            t2 = k + (int)(seconds * 1000);
                        }
                    }
                    if (e.Kind == EventKind.BladeDamaged) r.Damaged++;
                    if (e.Kind == EventKind.RotorJammed) r.Jams++;
                    if (e.Kind == EventKind.Tunnel && r.Tunnels++ == 0)
                        r.TunnelBy = spec.Parts[e.Part].Name + " through " + RangeScenarios.World.ObjectName(e.Object) + " " + RangeScenarios.World.MaterialName(e.Material)
                            + $" at {e.B:F2} m/s, {e.A * 1e3:F1} mm past, step {k}";
                    if (e.Kind == EventKind.PropStrike && double.IsNaN(r.TouchSpeed)) r.TouchSpeed = e.A;
                    if (e.Kind == EventKind.Crash && r.Crashes++ == 0) r.CrashBy = spec.Parts[e.Part].Name + " on " + RangeScenarios.World.ObjectName(e.Object) + $" at {e.A:F2} m/s, step {k}";
                }
                if (k >= end - window)
                {
                    commands += core.State.EscCommand[3];
                    amps += core.Trace.PackAmps0 + core.Trace.PackAmps1;
                    if (core.Vibration != null) band += core.Vibration.Last.Y * core.Vibration.Last.Y;
                    measured++;
                }
            }
            r.TipA = core.Propulsion.Rotor(3).Blade(0).TipRadius;
            r.TipB = core.Propulsion.Rotor(3).Blade(1).TipRadius;
            r.EndSpeed = core.State.Velocity.Length();
            r.EndHeight = core.State.Position.Y;
            r.HoverCommand = commands / measured;
            r.HoverAmps = amps / measured;
            r.Band = System.Math.Sqrt(band / measured);
            return r;
        }

        /// <summary>A tipped landing's blades: the passes that did work and the glances, those of them in the soil (the ground or
        /// a soil shape: carves, and a jammed pass short of a break), the work the passes took (J), the jams, the damage events
        /// (wear, breaks) and the mass lost (kg), the shortest blade left (r/R), the stones the passes met (p7f-damage 1.3's tip
        /// slide); and the landing's largest and last tilt (°), its speed at the end (m/s), how far it slid along the ground from
        /// the touch (m), its crashes and tunnels.</summary>
        public struct TipResult
        {
            public int Passes, Glances, InSoil, Jams, Damaged, Worn, Broken, Stones, Crashes, Tunnels;
            public double Work, Lost, ShortestTip, MaxTilt, EndTilt, EndSpeed, Slide;
            public bool Over;
            public string TunnelBy, CrashBy;

            public string Describe() =>
                $"{(Over ? "over" : "upright")} (tilt {MaxTilt:F1}°, {EndTilt:F1}° at the end): {Passes} passes that did work ({InSoil} in the soil), "
                + $"{Glances} glances, {Work:F2} J; {Stones} stones met; {Jams} jams; {Damaged} damage events ({Worn} wear, {Broken} breaks), {Lost * 1e6:F1} mg lost, "
                + $"the shortest blade {ShortestTip * 127:F2} mm; slid {Slide:F2} m, {EndSpeed:F2} m/s at the end; {Crashes} crashes{(CrashBy != null ? " (" + CrashBy + ")" : "")}, "
                + $"{Tunnels} tunnels{(TunnelBy != null ? " (" + TunnelBy + ")" : "")}";
        }

        /// <summary>
        /// A landing the pilot tips over (prop-motor-damage's "Props that touch the ground bog down": the dirt track's and S04's
        /// soil when the drone tips over on landing). Armed in the air 1 m over at (a wet soil holds a resting drone past the
        /// scripted pilot's throttle: p7a-ground 1.13), SimPilot hovers there for 5 s, then comes down
        /// at 0.5 m/s moving sideways (world +x, across the skids) at lateral m/s, holds the sticks centred with the skids 0.1 m
        /// up (as SoilScenarios.Land) and at the first touch on the ground cuts the throttle (still armed: airmode's idle and its
        /// authority) or disarms; flown 3 s past the touch. The skid catches and the drone rolls onto its lower props near the
        /// hover's speed. In rain, on surfaces.json's soil (table; null flies the rigid ground, where the blades glance: the
        /// known-wrong). started sees the setup and the core before the first step, stepped the core and its stick after each (a
        /// recorder's). tipSlide false flies model 43's carve (ContactSpec.TipSlide: the blade's own speed, cot θ of the whole
        /// impulse, no stones, no immersed disc, no plough; the slide p7f-damage 1.3's integration note found, the known-wrong).
        /// </summary>
        public static TipResult TippedLanding(DroneSpec spec, FlightWorld world, Double3 at, RainLevel rain, SurfaceTable table, bool disarm,
            double lateral = 2.5, System.Action<FlightSetup, FlightCore> started = null, System.Action<FlightCore, StickSample> stepped = null,
            bool tipSlide = true)
        {
            double stand = SoilScenarios.Standing(spec);
            FlightSetup setup = SoilScenarios.Setup(spec, world, at + new Double3(0, stand + 1, 0), rain, table: table, rigid: table == null);
            setup.Contacts.TipSlide = tipSlide;
            var core = new FlightCore(setup);
            started?.Invoke(setup, core);
            var r = new TipResult { ShortestTip = 1 };
            long touch = -1;
            bool descending = false;
            Double3 touchedAt = default;
            void Step(StickSample stick)
            {
                stick.HostUs = core.State.Step * FlightCore.StepUs;
                core.Advance(stick);
                stepped?.Invoke(core, stick);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (descending && touch < 0 && e.Kind == EventKind.ContactMade && e.Object == WorldIds.Ground)
                    {
                        touch = core.State.Step;
                        touchedAt = core.State.Position;
                    }
                    Count(spec, core, e, ref r);
                }
                Double3 up = core.State.Attitude.Rotate(new Double3(0, 1, 0));
                double tilt = System.Math.Acos(System.Math.Max(-1, System.Math.Min(1, up.Y))) * 180 / System.Math.PI;
                if (touch < 0) return;
                r.MaxTilt = System.Math.Max(r.MaxTilt, tilt);
                r.EndTilt = tilt;
            }
            for (int k = 0; k < 16; k++) Step(new StickSample { Arm = k >= 8 });
            var pilot = new SimPilot(core.Controller, at + new Double3(0, stand + 1, 0));
            for (int k = 0; k < 5000; k++) Step(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            StickSample held = default;
            bool holding = false;
            double y0 = at.Y + stand + 1;
            descending = true;
            for (int k = 0; k < 20000 && (touch < 0 || core.State.Step - touch < 3000); k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick = held;
                if (!holding)
                {
                    double t = k * FlightCore.Dt;
                    pilot.Target = at + new Double3(lateral * t, y0 - at.Y - 0.5 * t, 0);
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = new Double3(lateral, -0.5, 0);
                    stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                    if (s.Position.Y - at.Y - stand < 0.1 && s.Velocity.Y < 0)
                    {
                        holding = true;
                        held = stick;
                        held.Roll = held.Pitch = held.Yaw = 0;
                        stick = held;
                    }
                }
                if (touch >= 0)
                {
                    stick.Throttle = 0;
                    if (disarm) stick.Arm = false;
                }
                Step(stick);
            }
            for (int c = 0; c < 4; c++)
                for (int b = 0; b < spec.Props.Blades; b++)
                    r.ShortestTip = System.Math.Min(r.ShortestTip, core.Propulsion.Rotor(c).Blade(b).TipRadius);
            r.Over = r.MaxTilt > 60;
            r.EndSpeed = core.State.Velocity.Length();
            Double3 slid = core.State.Position - touchedAt;
            r.Slide = System.Math.Sqrt(slid.X * slid.X + slid.Z * slid.Z);
            return r;
        }

        /// <summary>The tipped landing stops (p7f-damage 1.3's tip slide): 3 s after the touch the drone is at rest on the ground,
        /// under StopSpeed. Model 43's carve slid on at 13.0–13.6 m/s (the known-wrong).</summary>
        public static bool TipStops(in TipResult r) => r.EndSpeed < StopSpeed;

        /// <summary>The speed a stopped landing is under 3 s after its touch, m/s: 0.5 (assumed: a body at rest on soil, its
        /// residual a contact's rocking, far below the slide's 13 m/s).</summary>
        public const double StopSpeed = 0.5;

        static void Count(DroneSpec spec, FlightCore core, in FlightEvent e, ref TipResult r)
        {
            var mode = (StrikeMode)(e.Index >> 4);
            if (e.Kind == EventKind.BladeStrike)
            {
                if (mode == StrikeMode.Glance) r.Glances++;
                else r.Passes++;
                if (mode != StrikeMode.Glance && (e.Object == WorldIds.Ground || e.Surface != WorldIds.NoSurface)) r.InSoil++;
                r.Work += e.A;
            }
            if (e.Kind == EventKind.BladeDamaged)
            {
                r.Damaged++;
                if (mode == StrikeMode.Wear) r.Worn++;
                if (mode == StrikeMode.Break) r.Broken++;
                r.Lost += e.B;
            }
            if (e.Kind == EventKind.StoneStrike)
            {
                r.Stones++;
                r.Work += e.A;
            }
            if (e.Kind == EventKind.RotorJammed) r.Jams++;
            if (e.Kind == EventKind.Crash && r.Crashes++ == 0)
                r.CrashBy = spec.Parts[e.Part].Name + " on " + (e.Object == WorldIds.Ground ? "the ground" : core.World.Query.ObjectName(e.Object))
                    + $" at {e.A:F2} m/s, {e.C:F0} m/s², step {core.State.Step}";
            if (e.Kind == EventKind.Tunnel && r.Tunnels++ == 0)
                r.TunnelBy = spec.Parts[e.Part].Name + " through " + (e.Object == WorldIds.Ground ? "the ground" : core.World.Query.ObjectName(e.Object))
                    + $" (material {e.Material}, surface {e.Surface}) at {e.B:F2} m/s, {e.A * 1e3:F1} mm past, step {core.State.Step}";
        }

        /// <summary>The tipped landing's two sites: the dirt track (a stub floor of S13 at its origin) and S04's bare soil (the
        /// range's plane at its "track" start).</summary>
        public static FlightWorld TrackWorld(out Double3 at)
        {
            at = default;
            return SoilScenarios.Floor("dirt_track");
        }

        public static FlightWorld BeltWorld(out Double3 at)
        {
            var world = new FlightWorld(RangeScenarios.World);
            at = SoilScenarios.TrackStart(world);
            return world;
        }

        /// <summary>A pilot's flight's effects, as DamageScenario's hover flies them (the motors' asymmetry, the vortex rings, each
        /// rotor's own wake, the flapping, the modes; the camera's band needs the modes).</summary>
        public static System.Action<FlightSetup> Airframe(DroneSpec spec) => s =>
        {
            s.Asymmetry = spec.BuildAsymmetry();
            s.VortexRing = spec.BuildVortexRing();
            s.RotorWake = spec.BuildRotorWake();
            s.Flapping = spec.BuildFlapping();
            s.Modes = spec.BuildModes();
            s.CameraPosition = spec.CameraPosition;
        };

        /// <summary>A runtime object of one shape.</summary>
        public static RuntimeObject[] One(ShapeGeometry g) => new[] { new RuntimeObject("target", new[] { g }) };

        /// <summary>The rig's setup: the legs-on drone on the range (or world) at position and attitude, with its contacts and
        /// runtime objects, the strikes on (or off), on a seed; with table, surfaces.json's soil at the rain's moisture (without
        /// it the ground and the soil glance).</summary>
        public static FlightSetup Setup(DroneSpec spec, Double3 position, Quat attitude, RuntimeObject[] objects, ulong seed = 0x450F, bool strikes = true,
            FlightWorld world = null, SurfaceTable table = null, RainLevel rain = RainLevel.Sunny)
        {
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            FlightSetup setup = ContactScenarios.Setup(spec, m, h, true, position, seed);
            if (table != null) setup.Contacts = spec.BuildContacts(spec.Nominal, m, h, table);
            setup.Rain = rain;
            setup.World = world ?? new FlightWorld(RangeScenarios.World);
            setup.StartAttitude = attitude;
            setup.RuntimeObjects = objects ?? new RuntimeObject[0];
            setup.Contacts.BladeStrikes = strikes;
            return setup;
        }

        static double _duty = double.NaN;

        /// <summary>The duty that holds a free rotor at HoverSpeed on the rig's packs (found once, by bisection on 400 steps).</summary>
        public static double HoverDuty(DroneSpec spec)
        {
            if (!double.IsNaN(_duty)) return _duty;
            double lo = 0.2, hi = 0.9;
            for (int k = 0; k < 30; k++)
            {
                double d = 0.5 * (lo + hi);
                var rig = new Rig(Setup(spec, Sky, Quat.Identity, null), d);
                rig.Hold(Sky, 400);
                if (rig.Now.RotorSpeed[0] > HoverSpeed) hi = d;
                else lo = d;
            }
            return _duty = 0.5 * (lo + hi);
        }

        /// <summary>A box of a material (by name) on the range's world: centre, half extents, its axes the world's.</summary>
        public static ShapeGeometry Box(string material, Double3 centre, Double3 half) => new ShapeGeometry
        {
            Kind = ShapeKind.Box, Center = centre, AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1),
            HalfExtents = half, Material = RangeScenarios.World.MaterialId(material),
        };

        /// <summary>A vertical rod (a capsule) of a material: its axis through centre, radius, half its core.</summary>
        public static ShapeGeometry Rod(string material, Double3 centre, double radius, double halfLength) => new ShapeGeometry
        {
            Kind = ShapeKind.Capsule, Center = centre, AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1),
            Radius = radius, HalfLength = halfLength, Material = RangeScenarios.World.MaterialId(material),
        };

        /// <summary>The heading (rad) that points one prop, fl, straight along the nose's bearing turned 45° left: with it a
        /// heading of 45° puts fl due north of the body's origin, 0.226 m out.</summary>
        public const double LeadHeading = 45 * DegToRad;

        public static Quat Heading(double yaw, double pitchDown = 0)
        {
            DetMath.SinCos(yaw / 2, out double sy, out double cy);
            var q = new Quat(0, sy, 0, cy);
            if (pitchDown == 0) return q;
            DetMath.SinCos(pitchDown / 2, out double sp, out double cp);
            return (q * new Quat(sp, 0, 0, cp)).Normalized();
        }

        /// <summary>
        /// The body origin that puts disc `corner`'s farthest rim point `depth` past a face's plane (through p, outward normal n),
        /// with the body at attitude and the hub at height y (world). The rim's reach along −n is the hub's plus R times the
        /// in-plane part of −n (a tilted disc reaches less far along a horizontal normal).
        /// </summary>
        public static Double3 RimInto(DroneSpec spec, Quat attitude, int corner, Double3 p, Double3 n, double depth, double y)
        {
            Double3 hub = attitude.Rotate(spec.Motors[corner].Position); // the hub: the disc's centre (drone.json's contacts)
            Double3 axis = attitude.Rotate(new Double3(0, 1, 0)), inward = n * -1;
            Double3 inPlane = inward - axis * Double3.Dot(inward, axis);
            double along = Double3.Dot(hub, inward) + spec.Props.Diameter / 2 * inPlane.Length();
            // Along n the offset is fixed; across it the body sits at p's, the hub at y on a vertical face.
            Double3 o = p + inward * (depth - along);
            if (DetMath.Abs(n.Y) > 0.5) return o; // a horizontal face: the height is the face's
            return new Double3(o.X, y - hub.Y, o.Z);
        }

        /// <summary>A placed variant's thickest round shape of a material (its trunk or pole, standing): its south side at height y
        /// over the ground (a point on its surface facing −z and that normal) and its radius.</summary>
        public static void Trunk(string variant, string material, double y, out Double3 point, out Double3 normal, out double radius)
        {
            MapWorld w = RangeScenarios.World;
            int obj = RangeScenarios.Placed(variant);
            ushort id = w.MaterialId(material);
            ShapeGeometry best = default;
            for (int s = 0; w.Geometry(obj, s, out ShapeGeometry g); s++)
                if ((g.Kind == ShapeKind.Capsule || g.Kind == ShapeKind.Cylinder) && g.Material == id && g.AxisY.Y > 0.95 && g.Radius > best.Radius
                    && g.Center.Y - g.HalfLength <= y && g.Center.Y + g.HalfLength >= y) best = g;
            radius = best.Radius;
            normal = new Double3(0, 0, -1);
            Double3 a = best.Center + best.AxisY * ((y - best.Center.Y) / best.AxisY.Y);
            point = new Double3(a.X, y, a.Z - best.Radius);
        }

        /// <summary>H01_D's south wall (masonry): a point on its south face at the face's middle and the outward normal (about
        /// world −z), from the map's own shapes: the face of a masonry box of H01_D at least 2 m tall facing south, the
        /// southernmost (not its plinth).</summary>
        public static void SouthWall(out Double3 point, out Double3 normal, out double yLo, out double yHi)
        {
            MapWorld w = RangeScenarios.World;
            int obj = RangeScenarios.Placed("H01_D");
            ushort masonry = w.MaterialId("masonry");
            point = default;
            normal = new Double3(0, 0, -1);
            yLo = yHi = 0;
            double south = double.PositiveInfinity, best = 0;
            for (int s = 0; w.Geometry(obj, s, out ShapeGeometry g); s++)
            {
                if (g.Kind != ShapeKind.Box || g.Material != masonry || g.HalfExtents.Y < 1) continue; // a wall, not a plinth or a sill
                foreach (int sign in new[] { -1, 1 })
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Double3 a = axis == 0 ? g.AxisX : axis == 1 ? g.AxisY : g.AxisZ;
                        double e = axis == 0 ? g.HalfExtents.X : axis == 1 ? g.HalfExtents.Y : g.HalfExtents.Z;
                        Double3 out_ = a * sign;
                        if (out_.Z > -0.999) continue;
                        Double3 c = g.Center + out_ * e;
                        double width = axis == 0 ? g.HalfExtents.Z : g.HalfExtents.X;
                        if (c.Z < south - 1e-6 || DetMath.Abs(c.Z - south) <= 1e-6 && g.HalfExtents.Y * width > best)
                        {
                            south = c.Z;
                            best = g.HalfExtents.Y * width;
                            point = c;
                            normal = out_;
                            yLo = g.Center.Y - g.HalfExtents.Y;
                            yHi = g.Center.Y + g.HalfExtents.Y;
                        }
                    }
                }
            }
        }
    }
}
