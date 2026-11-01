using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// ground-contact's scenarios on the test range (p6-flight-core 1.6b, D-044, D-047): the repository's drone with its
    /// contacts on MapWorld over maps/test-range, its start points, its patches and its objects, flown by SimPilot in still
    /// air as ContactScenarios flies them on the stub. The suite asserts them (RangeContactTests); tools/flightbench
    /// --contacts prints them.
    ///
    /// The spots were read off the test range's own data (a ray grid over S04's instance 707, E01's "run" hole record, the
    /// objects' shapes; review-physics §6's cases) and each scenario checks its spot's premise, so a regenerated map fails
    /// there, not somewhere downstream.
    /// </summary>
    public static class RangeScenarios
    {
        const double DegToRad = 3.141592653589793 / 180;

        static MapWorld _world;
        static string _root;

        /// <summary>The repository root (above maps/test-range/placements.json).</summary>
        public static string Root => _root ?? (_root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
            ContactScenarios.RepositoryFile("maps/test-range/placements.json")))));

        /// <summary>The test range, loaded once. A flight resets its runtime objects and adds its own (FlightCore).</summary>
        public static MapWorld World => _world ?? (_world = MapWorld.Load(Root, "test-range"));

        public static StartPoint Start(string id)
        {
            foreach (StartPoint p in World.StartPoints)
                if (p.Id == id) return p;
            throw new System.InvalidOperationException("the test range has no start point '" + id + "'");
        }

        public static ContactScenarios.Site At(StartPoint p) => new ContactScenarios.Site(new FlightWorld(World), p.Position, p.HeadingDeg * DegToRad);

        public static ContactScenarios.Site At(Double3 foot, double headingDeg) => new ContactScenarios.Site(new FlightWorld(World), foot, headingDeg * DegToRad);

        /// <summary>The id of the occurrence'th placed object of a variant.</summary>
        public static int Placed(string variant, int occurrence = 0)
        {
            for (int obj = 0; obj < World.PlacedObjects; obj++)
                if (World.ObjectName(obj) == variant && occurrence-- == 0) return obj;
            throw new System.InvalidOperationException(variant + " is not placed");
        }

        // ---- The spots -------------------------------------------------------------------------------------------------

        /// <summary>S04's instance 707: a 1.2 m square of its straw mat (straw boxes, top at y 0, over the soil box).</summary>
        public static readonly Double3 Straw = new Double3(-1.4, 0, -16.355);

        /// <summary>S04's instance 707: bare soil between its straw boxes (the soil box's top, belt_straw, at y −0.125).</summary>
        public static readonly Double3 Soil = new Double3(-6.7, -0.125, -17.0);

        /// <summary>E01's run 18.75 m along its centre line, in the middle of its longest stretch clear of spoil (17.5–20.0 m),
        /// where it runs at heading 107° (RunHeading): the belt_bare floor fillers at y −1.5, 0.64 m between the walls at the
        /// skids and 0.69 m at the props (the drone spans 0.574 m).</summary>
        public static readonly Double3 Run = new Double3(67.966, -1.5, -14.936);
        public const double RunHeading = 107;

        /// <summary>The flights in: a foot on the plane, a heading, the height (m over the foot) held, and the object met.</summary>
        public struct Approach
        {
            public string Name, Variant;
            public int Occurrence;
            public Double3 Foot;
            public double HeadingDeg, Height, Speed;
            public int ClimbSteps;
            public double ClimbRate;
        }

        /// <summary>
        /// - pole: L01_straight #21 at (0, −34), 50 m north at 1.5 m (ground-contact's "Into a pole" on the test range; 30 m
        ///   until p7c-wind 1.6's lower rotor drag let the pilot's speed overshoot there, 11.3 m/s, as on the stub)
        /// - crown: V01_L #31's crown in the posadka, 35 m north at 10 m: among its twig shapes (30 N/m, pushed through)
        ///   and bark limbs, then the belt's trees behind it
        /// - wall: H01_D's south wall (masonry), 30 m north at 1.5 m
        /// - car: R01_A's flank (sheet metal), 30 m south at 0.8 m
        /// - wire: L03_medium #23's third conductor (y 5.555 at mid-span), 50 m north at 5.47 m: the front discs' rims meet
        ///   it first only in a band of the held height 1 cm wide (5.47–5.48 m): held lower the payload meets it first, held
        ///   higher the front motor; found by a scan in 1 cm steps on p7c-wind 1.6's rotor drag (5.73 m from 30 m on the tuned
        ///   drag before it)
        /// - climb: straight up at 3 m/s under #23's lowest conductor (5.06 m at mid-span, z −34.263), nose north, the
        ///   CoM 0.22 m south of it: the conductor crosses both front discs' rims (hubs at body z 0.160, r 0.127) clear of
        ///   the coil (to z 0.140), the cans (0.179) and the arms, so a prop ring meets it first (review-physics §6)
        /// </summary>
        public static readonly Approach[] Approaches =
        {
            new Approach { Name = "pole", Variant = "L01_straight", Occurrence = 3, Foot = new Double3(0, 0, -84), HeadingDeg = 0, Height = 1.5, Speed = 10, ClimbSteps = 3000 },
            new Approach { Name = "crown", Variant = "V01_L", Occurrence = 0, Foot = new Double3(-136.5, 0, -40), HeadingDeg = 0, Height = 10, Speed = 10, ClimbSteps = 6000 },
            new Approach { Name = "wall", Variant = "H01_D", Occurrence = 0, Foot = new Double3(-125, 0, -90), HeadingDeg = 0, Height = 1.5, Speed = 10, ClimbSteps = 3000 },
            new Approach { Name = "car", Variant = "R01_A", Occurrence = 0, Foot = new Double3(-161.5, 0, -13), HeadingDeg = 180, Height = 0.8, Speed = 10, ClimbSteps = 3000 },
            new Approach { Name = "wire", Variant = "L03_medium", Occurrence = 1, Foot = new Double3(-140, 0, -84), HeadingDeg = 0, Height = 5.47, Speed = 10, ClimbSteps = 6000 },
            new Approach { Name = "climb", Variant = "L03_medium", Occurrence = 1, Foot = new Double3(-140, 0, -34.483), HeadingDeg = 0, Height = 1.5, Speed = 0, ClimbSteps = 3000, ClimbRate = 3 },
        };

        public static Approach Find(string name)
        {
            foreach (Approach a in Approaches)
                if (a.Name == name) return a;
            throw new System.ArgumentException("no approach '" + name + "'");
        }

        public static ContactScenarios.PoleStrike Into(DroneSpec spec, MaterialRow[] materials, ulong hash, in Approach a, bool airframe = false)
        {
            int target = Placed(a.Variant, a.Occurrence);
            ContactScenarios.Site site = At(a.Foot, a.HeadingDeg);
            site.Airframe = airframe;
            return ContactScenarios.Into(spec, materials, hash, site, a.Height, a.Speed, _ => target, null, a.ClimbSteps, a.ClimbRate);
        }

        // ---- Resting ----------------------------------------------------------------------------------------------------

        public struct Resting
        {
            /// <summary>How far the body settled below standing unloaded, m; its speed at the end, m/s.</summary>
            public double Sink, Speed;

            /// <summary>The legs' loads at the end, N, and their sum over the weight; the air's upward force on the drone then
            /// (the body's, the coil's and the rotors' drag in the site's wind) over the weight, which the legs do not carry.</summary>
            public Corners LegLoad;
            public double LoadOverWeight, AeroOverWeight;

            /// <summary>The open contacts at the end, by the log's ContactMade and ContactBroken events: one line each.</summary>
            public List<string> Pairs;

            /// <summary>Of them: on the plane (object −1), on soil shapes (a placed object with a surface), on material shapes;
            /// and the supports whose SampleGround is Hole while one of their pairs is the plane (a double count).</summary>
            public int Plane, Soil, Material, PlaneInHole;

            /// <summary>The supports' SampleGround at the end: how many report Hole.</summary>
            public int HoleSupports;

            /// <summary>Contact events in the last second (0: no churn at rest).</summary>
            public int LateEvents;

            public int Tunnels, Crashes, Lost;
        }

        /// <summary>
        /// The legs-on drone set on its skids at the site (standing, unloaded), disarmed, for seconds: it settles on its legs
        /// and whatever is under them. The open contacts are read back from the events, as the log carries them.
        /// </summary>
        public static Resting Rest(DroneSpec spec, MaterialRow[] materials, ulong hash, ContactScenarios.Site site, double seconds = 3)
        {
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, hash);
            double stand = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, true, site.Foot + new Double3(0, stand, 0));
            setup.World = site.Open();
            setup.StartAttitude = site.Attitude();
            var core = new FlightCore(site.Effects(setup, spec));
            var r = new Resting { Pairs = new List<string>() };
            var open = new Dictionary<(int, int, int), FlightEvent>();
            int steps = (int)(seconds * 1000);
            for (int k = 0; k < steps; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (e.Kind == EventKind.ContactsLost) r.Lost++;
                    if (e.Kind != EventKind.ContactMade && e.Kind != EventKind.ContactBroken) continue;
                    if (k >= steps - 1000) r.LateEvents++;
                    var key = (e.Index, e.Object, e.Shape);
                    if (e.Kind == EventKind.ContactMade) open[key] = e;
                    else open.Remove(key);
                }
            }
            ref readonly FlightState s = ref core.State;
            r.Sink = site.Foot.Y + stand - s.Position.Y;
            r.Speed = DetMath.Sqrt(Double3.Dot(s.Velocity, s.Velocity));
            r.LegLoad = core.Trace.LegLoad;
            r.LoadOverWeight = (r.LegLoad[0] + r.LegLoad[1] + r.LegLoad[2] + r.LegLoad[3]) / (core.Body.Mass * 9.80665);
            r.AeroOverWeight = core.Trace.AeroForce.Y / (core.Body.Mass * 9.80665);

            // The ground under each shape's first core point now, as SampleGround gives it.
            ContactShape[] shapes = core.Contacts.Spec.Shapes;
            var hole = new bool[shapes.Length];
            var g = new GroundSample[1];
            for (int i = 0; i < shapes.Length; i++)
            {
                Double3 x = s.Position + s.Attitude.Rotate(shapes[i].A);
                core.World.SampleGround(new[] { new XZ(x.X, x.Z) }, g);
                hole[i] = (g[0].Flags & GroundFlags.Hole) != 0;
                if (hole[i] && shapes[i].Role == ContactRole.Support) r.HoleSupports++;
            }
            var keys = new List<(int, int, int)>(open.Keys);
            keys.Sort();
            IWorldQuery q = core.World.Query;
            foreach (var key in keys)
            {
                FlightEvent e = open[key];
                string shape = core.Contacts.Spec.Shapes[e.Index].Name;
                string what = e.Object == WorldIds.Ground ? "the plane" : q.ObjectName(e.Object) + "#" + e.Object + "/" + e.Shape;
                string stuff = e.Surface != WorldIds.NoSurface ? "surface " + q.SurfaceName(e.Surface) : q.MaterialName(e.Material);
                r.Pairs.Add(shape + " on " + what + " (" + stuff + ")");
                if (e.Object == WorldIds.Ground)
                {
                    r.Plane++;
                    if (hole[e.Index]) r.PlaneInHole++;
                }
                else if (e.Surface != WorldIds.NoSurface) r.Soil++;
                else r.Material++;
            }
            return r;
        }

        // ---- Legs off: a belly landing ----------------------------------------------------------------------------------

        public struct Belly
        {
            /// <summary>The first touch (a body shape's ContactMade on the ground): its part and its closing speed, m/s.</summary>
            public string FirstPart;
            public double TouchSpeed;

            /// <summary>After the touch: the peak acceleration of the body from its contacts, m/s² (Impact events), the time
            /// to rest (every contact still, the body slower than 1 cm/s), s, NaN if never; the parts resting at the end.</summary>
            public double PeakAccel, Settled;
            public List<string> Resting;

            public int Tunnels, Crashes, Lost;
        }

        /// <summary>
        /// D-043's legs-off drone from the stand at the site: SimPilot lifts it off the stand, moves 3 m along the heading at
        /// 1 m over the foot, and comes down at 0.5 m/s; at the first body touch of the ground the throttle is cut, and the
        /// flight runs 4 s more. The spool and the payload are its lowest parts (drone.json's load-bearing spheres).
        /// </summary>
        public static Belly BellyLanding(DroneSpec spec, MaterialRow[] materials, ulong hash, ContactScenarios.Site site)
        {
            Loadout off = spec.Nominal;
            off.Legs = false;
            ContactSpec contacts = spec.BuildContacts(off, materials, hash);
            FlightWorld world = site.Open();
            FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, false, LaunchStand.RestingOrigin(spec, contacts, site.Foot));
            setup.World = world;
            setup.StartAttitude = site.Attitude();
            setup.RuntimeObjects = new[] { LaunchStand.Build(spec, site.Foot, site.Heading, world.Query.MaterialId(spec.Stand.Material)) };
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            Double3 aside = site.Foot + site.Nose() * 3;
            var pilot = new SimPilot(core.Controller, site.Foot + new Double3(0, 1, 0)) { Heading = site.Nose() };
            var r = new Belly { Settled = double.NaN, Resting = new List<string>() };
            long touch = -1;
            var open = new Dictionary<(int, int, int), FlightEvent>();
            for (int k = 0; k < 30000; k++)
            {
                ref readonly FlightState s = ref core.State;
                if (k == 3000) pilot.Target = aside + new Double3(0, 1, 0);
                if (k >= 8000 && touch < 0) pilot.Target = aside + new Double3(0, DetMath.Max(-1, 1 - 0.5 * (k - 8000) * FlightCore.Dt), 0);
                StickSample stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                if (touch >= 0) stick.Throttle = 0;
                core.Advance(stick);
                ref readonly FlightState n = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (e.Kind == EventKind.ContactsLost) r.Lost++;
                    if (e.Kind == EventKind.Impact && touch >= 0) r.PeakAccel = DetMath.Max(r.PeakAccel, e.A);
                    if (e.Kind != EventKind.ContactMade && e.Kind != EventKind.ContactBroken) continue;
                    var key = (e.Index, e.Object, e.Shape);
                    if (e.Kind == EventKind.ContactMade) open[key] = e;
                    else open.Remove(key);
                    if (touch < 0 && k > 3000 && e.Kind == EventKind.ContactMade && e.Surface != WorldIds.NoSurface)
                    {
                        touch = n.Step;
                        r.FirstPart = core.Contacts.Spec.Shapes[e.Index].Name;
                        r.TouchSpeed = e.A;
                    }
                }
                if (touch < 0) continue;
                double speed = DetMath.Sqrt(Double3.Dot(n.Velocity, n.Velocity));
                if (speed < 0.01 && double.IsNaN(r.Settled)) r.Settled = (n.Step - touch) * FlightCore.Dt;
                if (speed >= 0.01) r.Settled = double.NaN;
                if ((n.Step - touch) * FlightCore.Dt >= 4) break;
            }
            foreach (var e in open.Values) r.Resting.Add(core.Contacts.Spec.Shapes[e.Index].Name);
            r.Resting.Sort(System.StringComparer.Ordinal);
            return r;
        }

        // ---- A skid across a conductor ----------------------------------------------------------------------------------

        public struct OnWire
        {
            /// <summary>The first contact on the conductor: the drone's contact shape and its closing speed, m/s.</summary>
            public string FirstShape;
            public double TouchSpeed;

            /// <summary>Every contact shape that touched the conductor, by name, in order; the most a leg carried, N.</summary>
            public List<string> Shapes;
            public double PeakLegLoad;

            /// <summary>The body's height over the conductor 1.5 s after the touch, m (negative: it fell off).</summary>
            public double After;

            public int Tunnels, Crashes, Lost, Strikes;
        }

        /// <summary>
        /// review-physics §6's "a skid landing across a conductor at 2 m/s": nose north over L03_medium #23's top conductor
        /// at its mid-span (it runs east-west, across both skids), the conductor 0.05 m behind the skids' T-joints at the
        /// front, so it crosses each front skid half between its spheres; SimPilot comes down at 2 m/s and the sticks are
        /// held with the throttle cut at the first touch. 1.5 s past it.
        /// </summary>
        public static OnWire OntoWire(DroneSpec spec, MaterialRow[] materials, ulong hash)
        {
            int line = Placed("L03_medium", 1);
            Double3 mid = MidSpan(line, 0);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, hash);
            double stand = ContactScenarios.Standing(contacts);
            // The CoM 0.05 m south of the conductor: it then crosses the front skid halves at body z +0.05.
            Double3 over = new Double3(mid.X, mid.Y, mid.Z - 0.05);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, true, over + new Double3(0, stand + 1.5, 0));
            setup.World = new FlightWorld(World);
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, over + new Double3(0, stand + 1.5, 0));
            var r = new OnWire { Shapes = new List<string>() };
            long touch = -1;
            for (int k = 0; k < 20000; k++)
            {
                ref readonly FlightState s = ref core.State;
                if (k >= 3000 && touch < 0) pilot.Target = over + new Double3(0, stand + 1.5 - 2 * (k - 3000) * FlightCore.Dt, 0);
                StickSample stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                if (touch >= 0) stick = new StickSample { HostUs = stick.HostUs, Arm = true };
                core.Advance(stick);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (e.Kind == EventKind.ContactsLost) r.Lost++;
                    if (e.Kind == EventKind.PropStrike) r.Strikes++;
                    if (e.Kind != EventKind.ContactMade || e.Object != line) continue;
                    string name = core.Contacts.Spec.Shapes[e.Index].Name;
                    r.Shapes.Add(name);
                    if (touch >= 0) continue;
                    touch = core.State.Step;
                    r.FirstShape = name;
                    r.TouchSpeed = e.A;
                }
                if (touch < 0) continue;
                Corners load = core.Trace.LegLoad;
                for (int c = 0; c < 4; c++) r.PeakLegLoad = DetMath.Max(r.PeakLegLoad, load[c]);
                if (core.State.Step - touch >= 1500)
                {
                    r.After = core.State.Position.Y - mid.Y;
                    break;
                }
            }
            return r;
        }

        /// <summary>A conductor's mid-span point (its chain's middle point, at the sag's lowest).</summary>
        public static Double3 MidSpan(int obj, int shape)
        {
            System.ReadOnlySpan<Double3> chain = World.WirePoints(obj, shape);
            return chain[chain.Length / 2];
        }
    }
}
