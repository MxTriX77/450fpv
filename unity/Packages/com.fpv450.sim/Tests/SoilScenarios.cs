using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// ground-contact's soil scenarios (p7a-ground 1.10): the repository's drone with surfaces.json on a stub floor of one
    /// surface (until the range's zones land, 1.3) or on the test range, in still air with the motors identical unless a
    /// scenario draws them, flown by the closed-loop pilot (SimPilot): the rest, the liftoff with design 13's held-leg rule,
    /// the landing with the throttle cut, the glide landing and the re-landing on a print. The suite asserts them
    /// (SoilTests); tools/flightbench --contacts prints them.
    /// </summary>
    public static class SoilScenarios
    {
        const double G = 9.80665;
        const double RadToDeg = 57.29577951308232;

        static SurfaceTable _table;

        /// <summary>The repository's assets-src/surfaces.json, read once.</summary>
        public static SurfaceTable Table() =>
            _table ??= SurfaceTable.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(SurfaceTable.RelativePath)));

        /// <summary>The table with one surface's sets changed by edit (a known-wrong model's soil).</summary>
        public static SurfaceTable With(string surface, Func<SoilSet, SoilSet> edit)
        {
            SurfaceTable t = Table().Clone();
            for (int i = 0; i < t.Surfaces.Length; i++)
            {
                if (t.Surfaces[i].Name != surface) continue;
                t.Surfaces[i].Dry = edit(t.Surfaces[i].Dry);
                t.Surfaces[i].Wet = edit(t.Surfaces[i].Wet);
                t.Surfaces[i].Saturated = edit(t.Surfaces[i].Saturated);
            }
            return t;
        }

        static MapWorld _range;

        /// <summary>The test range (maps/test-range), loaded once; a flight resets its runtime objects.</summary>
        public static MapWorld Range()
        {
            if (_range != null) return _range;
            string placements = ContactScenarios.RepositoryFile("maps/test-range/placements.json");
            string root = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(placements)));
            return _range = MapWorld.Load(root, "test-range");
        }

        /// <summary>A stub floor of one surface at y = 0, with the materials a runtime object may use.</summary>
        public static FlightWorld Floor(string surface) => new FlightWorld(new FloorWorld(0, surface, "steel", "timber"));

        /// <summary>The legs-on drone with surfaces.json (or table) at rest on world at start, still air, in rain; rigid flies
        /// without the table, the known-wrong rigid ground with plain friction (μ 0.5/0.4 on every surface, p6-flight-core 1.6).</summary>
        public static FlightSetup Setup(DroneSpec spec, FlightWorld world, Double3 start, RainLevel rain = RainLevel.Sunny, ulong seed = 0x450F,
            bool asymmetry = false, SurfaceTable table = null, bool rigid = false)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            return new FlightSetup
            {
                Seed = seed, World = world, Mass = spec.BuildMass(spec.Nominal), Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(),
                Asymmetry = asymmetry ? spec.BuildAsymmetry() : null, Rain = rain,
                Contacts = spec.BuildContacts(spec.Nominal, materials, hash, rigid ? null : table ?? Table()), StartPosition = start, StartPackCharge = 0.9,
                CameraPosition = spec.CameraPosition,
            };
        }

        /// <summary>The body origin's height with the skids' lowest point on y = 0, the legs unloaded.</summary>
        public static double Standing(DroneSpec spec)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            return ContactScenarios.Standing(spec.BuildContacts(spec.Nominal, materials, hash));
        }

        // ---- The rest ---------------------------------------------------------------------------------------------------

        public struct Rest
        {
            /// <summary>Each corner's half-skid as the last step left it, and the load its leg carries, N.</summary>
            public ContactModel.SkidSoil[] Skids;
            public double[] Load;

            /// <summary>Contact events in the last second, and in all.</summary>
            public int LateEvents, Events;
        }

        /// <summary>"A resting drone sinks as the rows say": the drone set on the ground disarmed, 3 s.</summary>
        public static Rest Resting(DroneSpec spec, FlightWorld world, Double3 at, RainLevel rain = RainLevel.Sunny, SurfaceTable table = null, double seconds = 3,
            bool rigid = false)
        {
            var core = new FlightCore(Setup(spec, world, at + new Double3(0, Standing(spec), 0), rain, table: table, rigid: rigid));
            var r = new Rest { Skids = new ContactModel.SkidSoil[4], Load = new double[4] };
            int steps = (int)(seconds / FlightCore.Dt + 0.5);
            for (int k = 0; k < steps; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                for (int i = 0; i < core.Events.Count; i++)
                {
                    EventKind e = core.Events[i].Kind;
                    if (e != EventKind.ContactMade && e != EventKind.ContactBroken) continue;
                    r.Events++;
                    if (k >= steps - 1000) r.LateEvents++;
                }
            }
            for (int c = 0; c < 4; c++)
            {
                r.Skids[c] = core.Contacts.SoilOf(c);
                r.Load[c] = core.Trace.LegLoad[c];
            }
            return r;
        }

        /// <summary>The level half-skid's sink under a load F (N) by the law alone: bisection on Soil's section over L.</summary>
        public static double LevelSink(in SoilSet set, double radius, double length, double load, double firm)
        {
            double k = Soil.Modulus(set, Soil.Width(SoilPart.Tube, radius)), lo = 0, hi = 1;
            for (int i = 0; i < 200; i++)
            {
                double z = 0.5 * (lo + hi);
                if (Soil.Force(SoilPart.Tube, radius, set, k, firm, z, 0, out _) * length > load) hi = z;
                else lo = z;
            }
            return 0.5 * (lo + hi);
        }

        /// <summary>The range's "track" start point (the plane's belt_straw until the dirt track's zone lands, 1.3).</summary>
        public static Double3 TrackStart(FlightWorld world)
        {
            foreach (StartPoint p in ((MapWorld)world.Query).StartPoints)
                if (p.Id == "track") return p.Position;
            throw new InvalidOperationException("the test range has no track start");
        }

        /// <summary>The middle of the range's first S04 patch at the ground's level: its soil box's centre, over which its
        /// straw mat lies 0.125 m deep on the box.</summary>
        public static Double3 PatchCentre(FlightWorld world)
        {
            IWorldQuery q = world.Query;
            for (int o = 0; q.ObjectName(o) != null; o++)
            {
                if (!q.ObjectName(o).StartsWith("S04_patch", StringComparison.Ordinal) || !q.Geometry(o, 0, out ShapeGeometry box)) continue;
                return new Double3(box.Center.X, 0, box.Center.Z);
            }
            throw new InvalidOperationException("the test range has no S04 patch");
        }

        // ---- The re-landing on its own prints --------------------------------------------------------------------------

        public struct Relanding
        {
            /// <summary>Per landing (on the spot, again on it, 0.3 m aside): the touch's speed (m/s), whether its half-skids
            /// started from a print, the groove's floor they met (the print's set, m; 0 on virgin soil) and how far the soil
            /// yielded under them past it (the deepest sink less the floor, m), the corners' mean.</summary>
            public double[] Touch, Floor, Yield;
            public bool[] Print;
        }

        /// <summary>How near the landing aside's yield is the first's ("A re-landing finds its prints"), a share (derived): the
        /// throttle's cut reaches the motors at the RC link's next 250 Hz frame, 0–4 ms after the touch, and one frame of the
        /// cut moves a 0.3 m/s landing's yield on crater_spoil by 4.6 % (3.370 against 3.216 mm on D-066's drone, 3.455
        /// against 3.297 on model 22's); with the cut held 0–7 ms past the touch the landing aside stays within −3.6 to
        /// +2.3 % of the first. A landing that finds a print yields about 70 % less.</summary>
        public const double AsideShare = 0.05;

        /// <summary>
        /// "A re-landing finds its prints": from a start 1 m over the spot (never on it), the pilot's landing on the spot
        /// (down at 0.3 m/s, the sticks held at 0.1 m and the throttle cut at the touch), 1 s at rest, up to 1 m again and the
        /// same landing on the spot, then up, 0.3 m aside across the skids, and the same landing there.
        /// </summary>
        public static Relanding Relandings(DroneSpec spec, FlightWorld world, Double3 at, bool rigid = false)
        {
            double stand = Standing(spec);
            var core = new FlightCore(Setup(spec, world, at + new Double3(0, stand + 1, 0), rigid: rigid));
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, at + new Double3(0, stand + 1, 0));
            var r = new Relanding { Touch = new double[3], Floor = new double[3], Yield = new double[3], Print = new bool[3] };
            for (int k = 0; k < 5000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            for (int n = 0; n < 3; n++)
            {
                Double3 spot = at + new Double3(n == 2 ? 0.3 : 0, stand, 0);
                const double from = 1;
                if (n > 0)
                {
                    // Up to the start's height over the spot, 5 s to settle there as before the first.
                    pilot.Target = spot + new Double3(0, from, 0);
                    for (int k = 0; k < 5000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                }
                StickSample held = default;
                bool holding = false;
                long touch = -1;
                var prior = new double[4];
                var first = new bool[4];
                var deepest = new double[4];
                double startY = core.State.Position.Y;
                for (int k = 0; k < 20000; k++)
                {
                    ref readonly FlightState s = ref core.State;
                    StickSample stick = held;
                    if (!holding)
                    {
                        pilot.Target = new Double3(spot.X, startY - 0.3 * k * FlightCore.Dt, spot.Z);
                        stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                        if (s.Position.Y - at.Y - stand < 0.1 && s.Velocity.Y < 0)
                        {
                            holding = true;
                            held = stick;
                            held.Roll = held.Pitch = held.Yaw = 0;
                            stick = held;
                        }
                    }
                    if (touch >= 0) stick.Throttle = 0;
                    stick.HostUs = s.Step * FlightCore.StepUs;
                    core.Advance(stick);
                    for (int i = 0; i < core.Events.Count; i++)
                        if (touch < 0 && core.Events[i].Kind == EventKind.ContactMade && core.Events[i].Object == WorldIds.Ground)
                        {
                            touch = core.State.Step;
                            r.Touch[n] = core.Events[i].A;
                        }
                    for (int c = 0; c < 4; c++)
                    {
                        ContactModel.SkidSoil sk = core.Contacts.SoilOf(c);
                        if (!sk.Engaged) continue;
                        double max = DetMath.Max(sk.Max0, DetMath.Max(sk.Max1, sk.Max2)), now = DetMath.Max(sk.Sink0, DetMath.Max(sk.Sink1, sk.Sink2));
                        if (!first[c])
                        {
                            first[c] = true;
                            prior[c] = max > 2 * now ? max : 0; // a print's sink under a fresh touch
                        }
                        deepest[c] = max;
                    }
                    if (touch >= 0 && core.State.Step - touch >= 1000) break;
                }
                SoilSet set = rigid ? default : SetOf(world, core.Contacts.Spec.Surfaces, core.Contacts.SoilOf(0).Surface, RainLevel.Sunny);
                for (int c = 0; c < 4; c++)
                {
                    double floor = prior[c] > 0 ? Soil.Set(set, prior[c]) : 0;
                    r.Print[n] |= prior[c] > 0;
                    r.Floor[n] += floor / 4;
                    r.Yield[n] += (deepest[c] - floor) / 4;
                }
                // Up off the ground again, the pilot's target over the spot.
                pilot.Target = spot + new Double3(0, from, 0);
            }
            return r;
        }

        // ---- The push: a sunk skid dragged slowly (a kinematic pose) --------------------------------------------------

        public struct Push
        {
            /// <summary>The drone's contacts' normal force (N), the largest tangential force they held before any half-skid
            /// slid (N), the force they slid at afterwards (N), and what design 3 derives for the hold and the slide from the
            /// half-skids' own state at the breakaway: Σ μs·Fn + c_a·S_m + P(θ), Σ μk·Fn + c_a·S_m + P(θ).</summary>
            public double Normal, Breakaway, Sliding, DerivedHold, DerivedSlide;

            /// <summary>The deepest node's sink at the start, m.</summary>
            public double Sink;
        }

        /// <summary>
        /// "A sunk skid resists sideways more than along": the legs-on drone held level with its skids' cores depth below
        /// the floor's surface (body y), its contacts settled for 0.5 s at rest, then moved 30 mm along direction (world,
        /// horizontal: +x across the skids, +z along them) over 1 s with its velocity zero (quasi-static). Reads the
        /// contacts' force each step through ContactModel alone.
        /// </summary>
        public static Push Drag(DroneSpec spec, FlightWorld world, double depth, Double3 direction, RainLevel rain = RainLevel.Sunny, bool rigid = false)
        {
            FlightSetup setup = Setup(spec, world, default, rain, rigid: rigid);
            MassModel mass = setup.Mass;
            world.Query.ResetRuntimeObjects();
            var model = new ContactModel(setup.Contacts, world, mass.Properties.Mass, SurfaceTable.MoistureOf(rain));
            var events = new StepEvents();
            var s = new FlightState { Attitude = Quat.Identity, Position = new Double3(0, Standing(spec) - depth, 0) };
            model.Sense(s, ref s, events);
            var r = new Push();
            bool slid = false;
            for (int k = 0; k < 1500; k++)
            {
                FlightState was = s;
                if (k >= 500) s.Position = s.Position + direction * (0.03 * FlightCore.Dt);
                s.Step++;
                events.Clear();
                model.Sense(was, ref s, events);
                Double3 f = default, m = default;
                var trace = new StepTrace();
                model.Forces(ref s, mass.Properties, ref f, ref m, ref trace, events);
                double normal = f.Y, tangential = DetMath.Abs(Double3.Dot(f, direction));
                bool sliding = false;
                double hold = 0, slide = 0;
                for (int c = 0; c < 4; c++)
                {
                    ContactModel.SkidSoil sk = model.SoilOf(c);
                    sliding |= sk.Sliding;
                    if (!sk.Engaged) continue;
                    SoilSet set = SetOf(world, setup.Contacts.Surfaces, sk.Surface, rain);
                    double kl = Soil.Modulus(set, 0.01), r2 = 0.005;
                    double side = 0, end = 0;
                    Plough(spec, sk, kl, r2, out side, out end);
                    double along = DetMath.Abs(direction.Z); // the skids run along body z
                    double plough = side * DetMath.Sqrt(1 - along * along) + end * along;
                    hold += set.StaticFriction * sk.Force + set.InterfaceAdhesion * sk.Wetted + plough;
                    slide += set.KineticFriction * sk.Force + set.InterfaceAdhesion * sk.Wetted + plough;
                }
                if (k == 499)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        ContactModel.SkidSoil sk = model.SoilOf(c);
                        r.Sink = DetMath.Max(r.Sink, DetMath.Max(sk.Sink0, DetMath.Max(sk.Sink1, sk.Sink2)));
                    }
                }
                if (k < 500) continue;
                if (rigid)
                {
                    // The rigid floor's pairs: the trace's slide is the corner's, found from the force's fall.
                    if (!slid && tangential < r.Breakaway) slid = true;
                    sliding = slid;
                    hold = 0.5 * normal;
                    slide = 0.4 * normal;
                }
                if (!slid && !sliding)
                {
                    r.Breakaway = DetMath.Max(r.Breakaway, tangential);
                    r.Normal = normal;
                    r.DerivedHold = hold;
                    r.DerivedSlide = slide;
                }
                if (sliding) slid = true;
                if (slid) r.Sliding = tangential;
            }
            return r;
        }

        /// <summary>
        /// The breakaway force exactly: the drone held as in Drag, settled 0.5 s, then moved by d along direction in one
        /// step; the largest d whose step holds without any anchor sliding, by bisection to a picometre, and the contacts'
        /// tangential force there (N), with their normal force (N). On the rigid floor (rigid) the move is a creep instead
        /// (RigidCreepSteps): the plain pairs' stick spring stretches with the touching material's slip, its point velocity
        /// over the step (p6-flight-core 1.6c, D-067), so a pose moved with no velocity stretches nothing.
        /// </summary>
        public static double Breakaway(DroneSpec spec, FlightWorld world, double depth, Double3 direction, out double normal, RainLevel rain = RainLevel.Sunny,
            bool rigid = false)
        {
            double lo = 0, hi = 0.05, held = 0;
            Holds(spec, world, depth, direction, 1e-7, rain, rigid, out double small, out normal);
            double stiffness = small / 1e-7; // the anchors' springs while they hold
            for (int i = 0; i < 60; i++)
            {
                double d = 0.5 * (lo + hi);
                if (Holds(spec, world, depth, direction, d, rain, rigid, out double tangential, out double n) && tangential >= stiffness * d * (1 - 1e-6))
                {
                    lo = d;
                    held = tangential;
                    normal = n;
                }
                else hi = d;
            }
            return held;
        }

        /// <summary>The rigid floor's creep, steps: d over 4 s, slowing linearly to rest, so the plain pairs' dampers (c_t·v)
        /// end at rest; on the way they add about (c_t/k_t/T)² of the hold to the spring, and the creep breaks early by that
        /// share (3e-6 here: 59.99982 N against μs·Fn's 60; measured).</summary>
        public const int RigidCreepSteps = 4000;

        static bool Holds(DroneSpec spec, FlightWorld world, double depth, Double3 direction, double d, RainLevel rain, bool rigid, out double tangential, out double normal)
        {
            FlightSetup setup = Setup(spec, world, default, rain, rigid: rigid);
            MassModel mass = setup.Mass;
            world.Query.ResetRuntimeObjects();
            var model = new ContactModel(setup.Contacts, world, mass.Properties.Mass, SurfaceTable.MoistureOf(rain));
            var events = new StepEvents();
            var s = new FlightState { Attitude = Quat.Identity, Position = new Double3(0, Standing(spec) - depth, 0) };
            model.Sense(s, ref s, events);
            Double3 f = default;
            int creep = rigid ? RigidCreepSteps : 0;
            for (int k = 0; k < 501 + creep; k++)
            {
                FlightState was = s;
                if (k == 500 && creep == 0) s.Position = s.Position + direction * d;
                if (k >= 500 && k < 500 + creep)
                {
                    // The creep: its step j of N moves (N − j)·2d/(N(N + 1)) at the velocity that moves it there, slowing to rest at d.
                    double move = 2 * d * (500 + creep - k) / ((double)creep * (creep + 1));
                    s.Position = s.Position + direction * move;
                    s.Velocity = direction * (move / FlightCore.Dt);
                }
                else s.Velocity = default;
                s.Step++;
                events.Clear();
                model.Sense(was, ref s, events);
                f = default;
                Double3 m = default;
                var trace = new StepTrace();
                model.Forces(ref s, mass.Properties, ref f, ref m, ref trace, events);
            }
            tangential = DetMath.Abs(Double3.Dot(f, direction));
            normal = f.Y;
            // Held: no half-skid's anchor slid, and (Breakaway's test) the force is still the springs' d times their
            // stiffness, which a slide caps below.
            for (int c = 0; c < 4; c++)
                if (model.SoilOf(c).Sliding) return false;
            return true;
        }

        // Design 3's passive resistance across a half-skid (½·k·∫z² along it) and at its outer end (½·k·2r·z_end²), from its
        // nodes' sinks by the model's own three-point rule (the T-joint at drone.json's 0.1025 m of the 0.22 m).
        static void Plough(DroneSpec spec, in ContactModel.SkidSoil sk, double kl, double r, out double side, out double end)
        {
            const double L = 0.22, a = 0.1025;
            double w1 = L * L * L / (6 * a * (L - a)), w2 = L / 2 - L * L / (6 * (L - a)), w0 = L - w1 - w2;
            double p0 = Math.Max(0, sk.Sink0), p1 = Math.Max(0, sk.Sink1), p2 = Math.Max(0, sk.Sink2);
            side = 0.5 * kl * (w0 * p0 * p0 + w1 * p1 * p1 + w2 * p2 * p2);
            end = 0.5 * kl * 2 * r * p2 * p2;
        }

        // ---- The liftoff ----------------------------------------------------------------------------------------------

        public struct Liftoff
        {
            /// <summary>The hover throttle the ramp rises past by 0.1, and when each corner's ground load last passed 1 N either
            /// way, pressing or pulled down by a wet groove (1.13) (s from the ramp's start; NaN if never after it).</summary>
            public double HoverThrottle;
            public double[] Left;

            /// <summary>Design 13's held leg: a corner still over 1 N at least 20 ms after the other three left, the attitude
            /// moving at least 0.5° toward it; the corner (−1 for none), the hold (s), the turn toward it (°), the peak ground
            /// force on it during the hold, its size either way (N), and whether its anchor slid.</summary>
            public int Held;
            public double Hold, Turn, Peak;
            public bool Slid;

            /// <summary>Each corner's deepest sink at rest before the ramp, m.</summary>
            public double[] Sink;

            /// <summary>The largest pull toward the ground on any corner during the ramp (N; a wet groove's or wet straw's,
            /// 1.13), how many corners still carried more than 1 N either way at its end (the drone held down), and the four
            /// corners' load then (N; below 0 the ground pulls the drone down by that much).</summary>
            public double Pull, EndLoad;
            public int Stuck;
        }

        /// <summary>The hover throttle of the drone in still air over world (the pilot's mean throttle over 2 s of a 1 m hover).</summary>
        public static double HoverThrottle(DroneSpec spec, FlightWorld world, Double3 at)
        {
            var core = new FlightCore(Setup(spec, world, at + new Double3(0, Standing(spec), 0)));
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, at + new Double3(0, Standing(spec) + 1, 0));
            double sum = 0;
            int n = 0;
            for (int k = 0; k < 6000; k++)
            {
                StickSample st = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                if (k >= 4000)
                {
                    sum += st.Throttle;
                    n++;
                }
                core.Advance(st);
            }
            return sum / n;
        }

        /// <summary>
        /// Design 13's liftoff: armed at rest for 2 s on the ground (the throttle at zero, the props idling), then the
        /// throttle ramped to the hover's + 0.1 over 0.5 s with roll, pitch and yaw centred, and held for 1 s. A flight seed
        /// draws the motors' asymmetry when asymmetry is on.
        /// </summary>
        public static Liftoff Lift(DroneSpec spec, FlightWorld world, Double3 at, double hover, RainLevel rain = RainLevel.Sunny, ulong seed = 0x450F,
            bool asymmetry = false, SurfaceTable table = null, bool rigid = false)
        {
            var core = new FlightCore(Setup(spec, world, at + new Double3(0, Standing(spec), 0), rain, seed, asymmetry, table, rigid));
            CrosswindScenario.Arm(core);
            for (int k = 0; k < 2000; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true });
            var r = new Liftoff { HoverThrottle = hover, Left = new double[4], Sink = new double[4], Held = -1 };
            Double3[] corner = Corners(core.Contacts.Spec);
            for (int c = 0; c < 4; c++)
            {
                ContactModel.SkidSoil sk = core.Contacts.SoilOf(c);
                r.Sink[c] = DetMath.Max(sk.Max0, DetMath.Max(sk.Max1, sk.Max2));
                r.Left[c] = double.NaN;
            }
            const int Steps = 1500;
            var lean = new double[Steps, 4];
            var load = new double[Steps, 4];
            var slid = new bool[Steps, 4];
            for (int k = 0; k < Steps; k++)
            {
                double t = k * FlightCore.Dt;
                double th = DetMath.Min(1, t / 0.5) * (hover + 0.1);
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true, Throttle = th });
                for (int c = 0; c < 4; c++)
                {
                    lean[k, c] = Lean(core.State, corner[c]);
                    load[k, c] = core.Trace.LegLoad[c];
                    slid[k, c] = core.Contacts.SoilOf(c).Sliding;
                    if (Math.Abs(load[k, c]) > 1) r.Left[c] = (k + 1) * FlightCore.Dt;
                    if (-load[k, c] > r.Pull) r.Pull = -load[k, c];
                }
            }
            for (int c = 0; c < 4; c++)
            {
                if (Math.Abs(load[Steps - 1, c]) > 1) r.Stuck++;
                r.EndLoad += load[Steps - 1, c];
            }
            // The last corner to leave, and when the other three had left.
            int held = 0;
            for (int c = 1; c < 4; c++)
                if (!(r.Left[c] <= r.Left[held])) held = c;
            double third = double.NegativeInfinity;
            for (int c = 0; c < 4; c++)
                if (c != held && r.Left[c] > third) third = r.Left[c];
            if (double.IsNaN(r.Left[held]) || !(third > 0)) return r;
            int from = (int)(third / FlightCore.Dt + 0.5) - 1, to = (int)(r.Left[held] / FlightCore.Dt + 0.5) - 1;
            double turn = 0, peak = 0;
            bool any = false;
            for (int k = from; k <= to; k++)
            {
                turn = DetMath.Max(turn, lean[k, held] - lean[from, held]);
                peak = DetMath.Max(peak, Math.Abs(load[k, held]));
                any |= slid[k, held];
            }
            r.Hold = r.Left[held] - third;
            r.Turn = turn;
            r.Peak = peak;
            r.Slid = any;
            if (r.Hold >= 0.02 && r.Turn >= 0.5) r.Held = held;
            return r;
        }

        // The corners' outer ends in body axes (the supports at the skids' ends).
        static Double3[] Corners(ContactSpec spec)
        {
            var at = new Double3[4];
            foreach (ContactShape s in spec.Shapes)
                if (s.Role == ContactRole.Skid) at[s.Corner] = s.A;
            return at;
        }

        // How far the body's up axis leans toward a corner's side, degrees: its world tilt along the corner's horizontal bearing.
        static double Lean(in FlightState s, Double3 corner)
        {
            Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0)), w = s.Attitude.Rotate(new Double3(corner.X, 0, corner.Z));
            w = new Double3(w.X, 0, w.Z);
            double len = w.Length();
            return Math.Asin(Double3.Dot(up, w) / len) * RadToDeg;
        }

        // ---- The landing with the throttle cut -----------------------------------------------------------------------

        public struct Landing
        {
            /// <summary>The descent at the touch (m/s); the deepest any node sank (m) and the deepest after 1 s at rest; the
            /// plastic set the soil keeps under the deepest node (m); the peak vertical acceleration after the touch (m/s²);
            /// the stop: the time from the touch until the body first stops going down (s); the most the body rose again after
            /// the touch (m); the attitude's most nose-down pitch after the touch (°).</summary>
            public double TouchSpeed, PeakSink, RestSink, Set, PeakAccel, Stop, Rebound, NoseDown;
            public int Tunnels, Crashes;
        }

        /// <summary>
        /// The pilot's landing: from a hover 1 m up the target sinks at descent m/s (and moves forward at forward m/s along
        /// body +z), the sticks are held where they are with the skids 0.1 m up, and the throttle is cut at the first touch;
        /// flown 2 s past it. ground is the stub floor or the range.
        /// </summary>
        public static Landing Land(DroneSpec spec, FlightWorld world, Double3 at, double descent, double forward = 0, RainLevel rain = RainLevel.Sunny,
            SurfaceTable table = null, bool rigid = false)
        {
            double stand = Standing(spec);
            var core = new FlightCore(Setup(spec, world, at + new Double3(0, stand, 0), rain, table: table, rigid: rigid));
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, at + new Double3(0, stand + 1, 0));
            int k = 0;
            for (; k < 5000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            var r = new Landing();
            StickSample held = default;
            bool holding = false;
            long touch = -1;
            double y0 = at.Y + stand + 1, lowY = double.PositiveInfinity, startY = 0, startPitch = 0;
            Double3 lastV = core.State.Velocity;
            bool stopped = false;
            for (; k < 30000; k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick = held;
                if (!holding)
                {
                    double t = (k - 5000) * FlightCore.Dt;
                    pilot.Target = at + new Double3(0, y0 - at.Y - descent * t, forward * t);
                    if (forward > 0)
                    {
                        pilot.HoldVelocity = true;
                        pilot.TargetVelocity = new Double3(0, -descent, forward);
                    }
                    stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                    if (s.Position.Y - at.Y - stand < 0.1 && s.Velocity.Y < 0)
                    {
                        holding = true;
                        held = stick;
                        held.Roll = held.Pitch = held.Yaw = 0;
                        stick = held;
                    }
                }
                if (touch >= 0) stick.Throttle = 0;
                stick.HostUs = s.Step * FlightCore.StepUs;
                core.Advance(stick);
                ref readonly FlightState n = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (touch < 0 && e.Kind == EventKind.ContactMade && e.Object == WorldIds.Ground)
                    {
                        touch = n.Step;
                        r.TouchSpeed = e.A;
                        startY = n.Position.Y;
                        startPitch = Pitch(n);
                    }
                }
                if (touch < 0)
                {
                    lastV = n.Velocity;
                    continue;
                }
                double t1 = (n.Step - touch) * FlightCore.Dt;
                double ay = (n.Velocity.Y - lastV.Y) / FlightCore.Dt;
                lastV = n.Velocity;
                r.PeakAccel = DetMath.Max(r.PeakAccel, DetMath.Abs(ay));
                lowY = DetMath.Min(lowY, n.Position.Y);
                r.Rebound = DetMath.Max(r.Rebound, n.Position.Y - lowY);
                r.NoseDown = DetMath.Max(r.NoseDown, startPitch - Pitch(n));
                if (!stopped && n.Velocity.Y >= 0)
                {
                    stopped = true;
                    r.Stop = t1;
                }
                for (int c = 0; c < 4; c++)
                {
                    ContactModel.SkidSoil sk = core.Contacts.SoilOf(c);
                    double deep = DetMath.Max(sk.Max0, DetMath.Max(sk.Max1, sk.Max2));
                    r.PeakSink = DetMath.Max(r.PeakSink, deep);
                    if (t1 >= 1.999 && sk.Engaged)
                    {
                        r.RestSink = DetMath.Max(r.RestSink, DetMath.Max(sk.Sink0, DetMath.Max(sk.Sink1, sk.Sink2)));
                        r.Set = DetMath.Max(r.Set, Soil.Set(SetOf(world, core.Contacts.Spec.Surfaces, sk.Surface, rain), deep));
                    }
                }
                if (t1 >= 2) break;
            }
            return r;
        }

        // ---- The wet pull (1.13) ---------------------------------------------------------------------------------------

        public struct Unstick
        {
            /// <summary>Per corner: the largest pull toward the ground (N); the separation from the groove's set (soil) or the
            /// layer's top (straw) at which the pull last acted (m); the work the pull did against the lift (J, the trapezoid
            /// over the separation); the wetted footprint by the test's own geometry (m²): the tube's chord at each node's
            /// deepest sink by the three-point rule in soil, the chord at the straw's compression at the press times the
            /// half-skid's length on straw; and whether a pull still acted at the end.</summary>
            public double[] Peak, Released, Work, Footprint;
            public bool[] Holding;

            /// <summary>Per corner at the press's end: the leg's deflection in soil, the corner's overlap on a layer (m), and its
            /// load (N).</summary>
            public double[] Press, PressLoad;

            /// <summary>The contacts' normal force at the press's end (N), the drone's weight (N), and the bytes allocated
            /// over the lift's steps.</summary>
            public double Load, Weight;
            public long Allocated;

            /// <summary>The contacts' force at the lift's end, world N (0 once every corner is off).</summary>
            public Double3 EndForce;
        }

        /// <summary>
        /// "A wet skid is held as it lifts": the legs-on drone held level over at (ContactModel alone, as Drag), its skids
        /// 1 mm over the ground, pressed down at 10 mm/s until its contacts carry its weight (each half-skid at its resting
        /// load), then lifted at 10 mm/s until 6 mm past the last pull, each step's pose moved with its velocity zero
        /// (quasi-static: the dampers read nothing, so the static law is what is measured). Reads each corner's pull and
        /// separation every step: in soil SoilOf's (the separation of the T-joint's node, the skids level, from Soil.Set of
        /// its deepest sink), on a wet layer the trace's (the leg's stretch F/k0 less the corner's overlap).
        /// </summary>
        public static Unstick Unsticking(DroneSpec spec, FlightWorld world, Double3 at, RainLevel rain, SurfaceTable table = null, Action<FlightWorld> place = null)
        {
            FlightSetup setup = Setup(spec, world, default, rain, table: table);
            MassModel mass = setup.Mass;
            world.Query.ResetRuntimeObjects();
            place?.Invoke(world);
            var model = new ContactModel(setup.Contacts, world, mass.Properties.Mass, SurfaceTable.MoistureOf(rain));
            var events = new StepEvents();
            var s = new FlightState { Attitude = Quat.Identity, Position = at + new Double3(0, Standing(spec) + 0.001, 0) };
            model.Sense(s, ref s, events);
            const double Step = 0.01 * FlightCore.Dt; // 10 mm/s
            var r = new Unstick
            {
                Peak = new double[4], Released = new double[4], Work = new double[4], Footprint = new double[4], Holding = new bool[4],
                Weight = mass.Properties.Mass * G,
            };
            double k0 = setup.Contacts.LegStiffness;
            setup.Contacts.Row("straw", out MaterialRow straw);
            // The straw under a level half-skid of S04's mat, by its area law (1.12): its row's stiffness × the footprint's factor.
            double layer = (rain == RainLevel.Sunny || !straw.HasWet ? straw.Stiffness : straw.WetStiffness)
                * Layer.Scale(straw.AreaLaw, straw.AreaReferenceDepth, MatDepth, false, L, 2 * TubeRadius);
            var trace = new StepTrace();
            // The press.
            for (int k = 0; k < 20000 && r.Load < r.Weight; k++)
            {
                FlightState was = s;
                s.Position = s.Position - new Double3(0, Step, 0);
                s.Step++;
                events.Clear();
                model.Sense(was, ref s, events);
                Double3 f = default, m = default;
                trace = new StepTrace();
                model.Forces(ref s, mass.Properties, ref f, ref m, ref trace, events);
                r.Load = f.Y;
            }
            double[] press = r.Press = new double[4];
            r.PressLoad = new double[4];
            for (int c = 0; c < 4; c++)
            {
                ContactModel.SkidSoil sk = model.SoilOf(c);
                press[c] = trace.LegCompression[c];
                r.PressLoad[c] = trace.LegLoad[c];
                // In soil the tube's chord at each node's deepest sink; on the straw layer its chord at the layer's own
                // compression (the corner's load over the layer's stiffness under the half-skid) along the half-skid.
                r.Footprint[c] = sk.Engaged ? W0 * Chord(sk.Max0) + W1 * Chord(sk.Max1) + W2 * Chord(sk.Max2) : Chord(trace.LegLoad[c] / layer) * L;
            }
            // The lift.
            var last = new double[4];
            var lastS = new double[4];
            bool[] pulled = new bool[4];
            double beyond = 0, lift = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 20000 && beyond < 0.006; k++)
            {
                FlightState was = s;
                s.Position = s.Position + new Double3(0, Step, 0);
                lift += Step;
                s.Step++;
                events.Clear();
                model.Sense(was, ref s, events);
                Double3 f = default, m = default;
                trace = new StepTrace();
                model.Forces(ref s, mass.Properties, ref f, ref m, ref trace, events);
                r.EndForce = f;
                bool any = false;
                for (int c = 0; c < 4; c++)
                {
                    ContactModel.SkidSoil sk = model.SoilOf(c);
                    double pull, sep;
                    if (sk.Engaged)
                    {
                        pull = sk.Pull;
                        sep = Soil.Set(SetOf(world, setup.Contacts.Surfaces, sk.Surface, rain), sk.Max1) - sk.Sink1;
                    }
                    else
                    {
                        // On a layer: the corner's overlap is the press's less the lift (the drone level, the face's plane
                        // fixed), and the part's separation the leg's stretch less it.
                        pull = trace.LegLoad[c] < 0 ? -trace.LegLoad[c] : 0;
                        sep = -pull / k0 - (press[c] - lift);
                    }
                    if (k > 0) r.Work[c] += 0.5 * (pull + last[c]) * (sep - lastS[c]);
                    if (pull > 0)
                    {
                        pulled[c] = true;
                        r.Peak[c] = DetMath.Max(r.Peak[c], pull);
                        r.Released[c] = sep;
                        any = true;
                    }
                    r.Holding[c] = pull > 0;
                    last[c] = pull;
                    lastS[c] = sep;
                }
                if (!any && (pulled[0] || pulled[1] || pulled[2] || pulled[3] || lift > 0.03)) beyond += Step;
            }
            r.Allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            return r;
        }

        /// <summary>The depth of S04's straw mat, m (its generator's mat_depth_m; physics_materials.json's straw row is
        /// stated there).</summary>
        public const double MatDepth = 0.125;

        /// <summary>A stub floor of belt_straw with one straw box on it, 2 m square and MatDepth deep, its top at MatDepth
        /// over the origin: S04's mat without its tiles' seams, so each support meets its top face alone. Place it with
        /// Unsticking's place after the floor's runtime objects are reset.</summary>
        public static FlightWorld MatFloor() => new FlightWorld(new FloorWorld(0, "belt_straw", "straw"));

        public static void PlaceMat(FlightWorld world)
        {
            var box = new ShapeGeometry
            {
                Kind = ShapeKind.Box, Center = new Double3(0, 0.5 * MatDepth, 0), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
                AxisZ = new Double3(0, 0, 1), HalfExtents = new Double3(1, 0.5 * MatDepth, 1), Material = 1,
            };
            ((FloorWorld)world.Query).AddObject("straw_mat", new[] { box });
        }

        // The skid's tube (drone.json, measured) and the three-point rule's weights on its nodes at 0, the T-joint and L.
        const double TubeRadius = 0.005, L = 0.22, A = 0.1025;
        static readonly double W1 = L * L * L / (6 * A * (L - A)), W2 = L / 2 - L * L / (6 * (L - A)), W0 = L - W1 - W2;

        // The tube's chord at sink z, the test's own (Math): 2√(2rz − z²) up to r, then its width.
        static double Chord(double z) => !(z > 0) ? 0 : z >= TubeRadius ? 2 * TubeRadius : 2 * Math.Sqrt(2 * TubeRadius * z - z * z);

        /// <summary>A world surface id's set in a table at a rain level.</summary>
        public static SoilSet SetOf(FlightWorld world, SurfaceTable table, byte surface, RainLevel rain)
        {
            string name = world.Query.SurfaceName(surface);
            foreach (SurfaceRow row in table.Surfaces)
                if (row.Name == name) return row.Set(SurfaceTable.MoistureOf(rain));
            throw new ArgumentException("no row for " + name);
        }

        // The nose's pitch up, degrees: the body's +z axis over the horizon.
        static double Pitch(in FlightState s) => Math.Asin(s.Attitude.Rotate(new Double3(0, 0, 1)).Y) * RadToDeg;
    }
}
