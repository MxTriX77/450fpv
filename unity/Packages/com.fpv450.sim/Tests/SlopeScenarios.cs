using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The flight on hills (w3-farmland-ground 2.2): the legs-on drone with surfaces.json on a map package's terrain. A slope
    /// is a package written here (Slope): one 64 m tile at 1 m spacing, a plane rising north (+z) whose facets all share one
    /// normal, of one surface class, so every answer has its plane to compare with; 0° is the stub floor of that surface
    /// (FloorWorld), the same ground as a plane. The ledge is the farmland's own (its surround's east step at x = 2000). The
    /// suite asserts them (SlopeTests); tools/flightbench --slopes prints them.
    /// </summary>
    public static class SlopeScenarios
    {
        const double G = 9.80665;
        const double RadToDeg = 57.29577951308232;

        /// <summary>The tile's side (m) and its height samples' rise (m a unit: size_m y = 32766 × it). A slope's rows rise by
        /// a whole number of units a metre, so its facets are coplanar to the bit's rounding and its angle is atan(units × it).</summary>
        public const double Side = 64, UnitRise = 0.002;
        const int Resolution = 65;

        static readonly Dictionary<string, MapWorld> Worlds = new Dictionary<string, MapWorld>();

        /// <summary>The height units a metre of the slope nearest deg.</summary>
        public static int Units(double deg) => (int)Math.Round(Math.Tan(deg / RadToDeg) / UnitRise);

        /// <summary>A slope of about deg (Units) of one surface: its package written to a fresh folder, loaded, the folder removed.</summary>
        public static MapWorld Slope(double deg, string surface = "meadow_sod")
        {
            int units = Units(deg);
            string key = units + "/" + surface;
            if (Worlds.TryGetValue(key, out MapWorld cached)) return cached;
            string dir = Path.Combine(Path.GetTempPath(), "fpv450-slope-" + Guid.NewGuid().ToString("N"));
            try
            {
                string maps = Path.Combine(dir, "maps", "slope");
                Directory.CreateDirectory(Path.Combine(maps, "heights"));
                Directory.CreateDirectory(Path.Combine(maps, "classes"));
                Directory.CreateDirectory(Path.Combine(dir, "assets-src"));
                File.Copy(ContactScenarios.RepositoryFile(MapWorld.MaterialsFile), Path.Combine(dir, MapWorld.MaterialsFile));
                var heights = new byte[2 * Resolution * Resolution];
                for (int r = 0; r < Resolution; r++)
                    for (int c = 0; c < Resolution; c++)
                    {
                        int s = (Resolution - 1 - r) * units, i = 2 * (r * Resolution + c); // row 0 north, the highest
                        heights[i] = (byte)s;
                        heights[i + 1] = (byte)(s >> 8);
                    }
                byte id = MapWorld.SurfaceId(surface);
                var files = new (string Name, byte[] Data)[] { ("heights/slope.r16", heights), ("classes/slope.raw", new[] { id, id, id, id }) };
                var listed = new StringBuilder();
                foreach (var f in files)
                {
                    File.WriteAllBytes(Path.Combine(maps, f.Name), f.Data);
                    using (SHA256 sha = SHA256.Create())
                        listed.Append(listed.Length > 0 ? "," : "").Append('"').Append(f.Name).Append("\":\"")
                            .Append(BitConverter.ToString(sha.ComputeHash(f.Data)).Replace("-", "").ToLowerInvariant()).Append('"');
                }
                File.WriteAllText(Path.Combine(maps, "map.json"), "{\"format\":\"fpv450-map-package\",\"format_version\":1,\"map\":\"slope\",\"frame\":\"unity-lh-yup\","
                    + "\"map_rect_m\":[-32,-32,32,32],\"tiles\":[{\"id\":\"slope\",\"ring\":\"map\",\"position_m\":[-32,0,-32],\"size_m\":[64,65.532,64],"
                    + "\"heights\":{\"file\":\"heights/slope.r16\",\"resolution\":65},\"classes\":{\"file\":\"classes/slope.raw\",\"resolution\":2}}],"
                    + "\"files\":{" + listed + "}}");
                MapWorld world = MapWorld.Load(dir, "slope");
                Worlds[key] = world;
                return world;
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        /// <summary>The ground under (x, z) of world.</summary>
        public static GroundSample Ground(IWorldQuery world, double x, double z)
        {
            var g = new GroundSample[1];
            world.SampleGround(new[] { new XZ(x, z) }, g);
            return g[0];
        }

        /// <summary>The facet's angle (deg) of a ground sample, from its normal.</summary>
        public static double FacetDeg(in GroundSample g) =>
            Math.Atan2(Math.Sqrt(g.Normal.X * g.Normal.X + g.Normal.Z * g.Normal.Z), g.Normal.Y) * RadToDeg;

        /// <summary>A site on a slope of deg (0: the stub floor of the same surface, a plane): its world, the foot at the centre,
        /// the ground's normal there.</summary>
        public static FlightWorld Site(double deg, out Double3 foot, out Double3 normal, string surface = "meadow_sod", bool terrainAtZero = false)
        {
            IWorldQuery q = deg == 0 && !terrainAtZero ? new FloorWorld(0, surface, "steel", "timber") : Slope(deg, surface);
            GroundSample g = Ground(q, 0, 0);
            foot = new Double3(0, g.Height, 0);
            normal = g.Normal;
            return new FlightWorld(q);
        }

        /// <summary>The attitude whose up is the ground's normal: a yaw (deg; 0 the nose north, uphill on a slope; 90° east, its skids
        /// across a slope), then the least turn that carries up onto the normal.</summary>
        public static Quat Aligned(Double3 normal, double yawDeg)
        {
            double yaw = yawDeg / RadToDeg;
            var y = new Quat(0, Math.Sin(yaw / 2), 0, Math.Cos(yaw / 2));
            Double3 axis = Double3.Cross(new Double3(0, 1, 0), normal);
            double sin = axis.Length();
            if (!(sin > 0)) return y;
            double angle = Math.Atan2(sin, normal.Y), k = Math.Sin(angle / 2) / sin;
            return (new Quat(axis.X * k, axis.Y * k, axis.Z * k, Math.Cos(angle / 2)) * y).Normalized();
        }

        static double TiltTo(in FlightState s, Double3 normal) =>
            Math.Acos(Math.Max(-1, Math.Min(1, Double3.Dot(s.Attitude.Rotate(new Double3(0, 1, 0)), normal)))) * RadToDeg;

        // ---- At rest on the slope ---------------------------------------------------------------------------------------

        public struct Rest
        {
            /// <summary>The facet's angle (deg); how far the body moved down the fall line (m), and in the last second alone, and its
            /// speed at the end (m/s); the largest tilt of its up from the slope's normal (deg); the corners sliding on their soil at
            /// the end; crashes and tunnels.</summary>
            public double Facet, Slide, Late, Speed, Tilt;
            public int Sliding, Crashes, Tunnels;
        }

        /// <summary>
        /// The drone set down disarmed on the slope, its skids on it (its up the slope's normal, the skids 10 µm in) with the nose
        /// uphill (yaw 0: the skids along the fall line) or across it (yaw 90°), in rain's moisture, for seconds.
        /// </summary>
        public static Rest Settle(DroneSpec spec, double deg, RainLevel rain, double yawDeg, double seconds = 3, SurfaceTable table = null)
        {
            FlightWorld world = Site(deg, out Double3 foot, out Double3 n);
            double stand = SoilScenarios.Standing(spec);
            FlightSetup setup = SoilScenarios.Setup(spec, world, foot + n * (stand - 1e-5), rain, table: table);
            setup.StartAttitude = Aligned(n, yawDeg);
            var core = new FlightCore(setup);
            Double3 start = core.State.Position, down = Downhill(n);
            var r = new Rest { Facet = SlopeAngle(n) };
            int steps = (int)(seconds / FlightCore.Dt + 0.5);
            Double3 late = start;
            for (int k = 0; k < steps; k++)
            {
                if (k == steps - 1000) late = core.State.Position;
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                Count(core, ref r.Crashes, ref r.Tunnels);
                r.Tilt = Math.Max(r.Tilt, TiltTo(core.State, n));
            }
            r.Slide = Double3.Dot(core.State.Position - start, down);
            r.Late = Double3.Dot(core.State.Position - late, down);
            r.Speed = core.State.Velocity.Length();
            for (int c = 0; c < 4; c++)
                if (core.Contacts.SoilOf(c).Sliding) r.Sliding++;
            return r;
        }

        /// <summary>The steepest slope (deg, a facet's) the resting drone holds on (Settle slides under Slipped in its seconds),
        /// by bisection over the slopes' whole units between lo and hi degrees; NaN if it slides at lo or holds at hi.</summary>
        public static double Holds(DroneSpec spec, RainLevel rain, double yawDeg, double lo = 5, double hi = 40, double seconds = 3)
        {
            int a = Units(lo), b = Units(hi);
            if (Slipped(Settle(spec, Deg(a), rain, yawDeg, seconds)) || !Slipped(Settle(spec, Deg(b), rain, yawDeg, seconds))) return double.NaN;
            while (b - a > 1)
            {
                int m = (a + b) / 2;
                if (Slipped(Settle(spec, Deg(m), rain, yawDeg, seconds))) b = m;
                else a = m;
            }
            return Math.Atan(a * UnitRise) * RadToDeg;
        }

        /// <summary>A resting drone that still moved more than this down the fall line in Settle's last second has slipped, m
        /// (assumed: set down unloaded it settles in its first half second, by up to 7 cm on the downhill legs' side give, and
        /// then rocks by a millimetre on held anchors; a slip moves it on, at metres a second past μs).</summary>
        public const double SlipDistance = 0.01;

        public static bool Slipped(in Rest r) => r.Late > SlipDistance;

        static double Deg(int units) => Math.Atan(units * UnitRise) * RadToDeg;

        /// <summary>A slope's steepest a body held by friction alone holds, deg: atan μs of the surface's set at rain's moisture (the
        /// bound without the groove's passive resistance or the wet interface's adhesion), or with kinetic atan μk, the steepest a
        /// sliding body stops on. A drone on compliant legs holds between them: the legs split the slope's pull by their side
        /// stiffness, not by their load, so the light uphill corners slide first, at μk, and the downhill ones hold the rest.</summary>
        public static double FrictionBound(RainLevel rain, string surface = "meadow_sod", bool kinetic = false)
        {
            foreach (SurfaceRow row in SoilScenarios.Table().Surfaces)
                if (row.Name == surface)
                {
                    SoilSet set = row.Set(SurfaceTable.MoistureOf(rain));
                    return Math.Atan(kinetic ? set.KineticFriction : set.StaticFriction) * RadToDeg;
                }
            throw new ArgumentException("no row for " + surface);
        }

        static Double3 Downhill(Double3 n)
        {
            // Down the fall line on the surface: the horizontal part of the normal, then along the surface.
            double h = Math.Sqrt(n.X * n.X + n.Z * n.Z);
            if (!(h > 0)) return default;
            return new Double3(n.X / h * n.Y, -h, n.Z / h * n.Y);
        }

        static double SlopeAngle(Double3 n) => Math.Atan2(Math.Sqrt(n.X * n.X + n.Z * n.Z), n.Y) * RadToDeg;

        static void Count(FlightCore core, ref int crashes, ref int tunnels)
        {
            for (int i = 0; i < core.Events.Count; i++)
            {
                EventKind e = core.Events[i].Kind;
                if (e == EventKind.Crash) crashes++;
                if (e == EventKind.Tunnel) tunnels++;
            }
        }

        // ---- The pilot's landing on the slope ----------------------------------------------------------------------------

        public struct Touchdown
        {
            /// <summary>The facet (deg); the descent at the first touch (m/s); the largest acceleration of the body after it (m/s²,
            /// the velocity's change a step); the deepest any node of a half-skid sank (m); how far it slid along the ground from
            /// the touch to the end (m) and its speed then (m/s); the largest and the last tilt of its up from the slope's normal
            /// (deg) after the touch; over: past 60° to it; crashes and tunnels.</summary>
            public double Facet, TouchSpeed, PeakAccel, PeakSink, Slide, EndSpeed, MaxTilt, EndTilt;
            public bool Over;
            public int Crashes, Tunnels;
        }

        /// <summary>
        /// The pilot's landing on the slope (as SoilScenarios.Land: armed, SimPilot hovers the body 1 m over the stand above the
        /// foot for 5 s, the target sinks at 0.5 m/s, the sticks are held where they are once the body is 0.1 m over the foot's
        /// stand, and the throttle is cut at the first touch on the ground), the drone level, its nose uphill (yaw 0) or across
        /// (yaw 90°); flown 3 s past the touch.
        /// </summary>
        public static Touchdown Land(DroneSpec spec, double deg, RainLevel rain, double yawDeg, bool terrainAtZero = false)
        {
            FlightWorld world = Site(deg, out Double3 at, out Double3 n, terrainAtZero: terrainAtZero);
            double stand = SoilScenarios.Standing(spec);
            FlightSetup setup = SoilScenarios.Setup(spec, world, at + new Double3(0, stand + 1, 0), rain);
            double yaw = yawDeg / RadToDeg;
            setup.StartAttitude = new Quat(0, Math.Sin(yaw / 2), 0, Math.Cos(yaw / 2));
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, at + new Double3(0, stand + 1, 0)) { Heading = new Double3(Math.Sin(yaw), 0, Math.Cos(yaw)) };
            int k = 0;
            for (; k < 5000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            var r = new Touchdown { Facet = SlopeAngle(n) };
            StickSample held = default;
            bool holding = false;
            long touch = -1;
            Double3 touchedAt = default, lastV = core.State.Velocity;
            double y0 = at.Y + stand + 1;
            for (; k < 30000 && (touch < 0 || core.State.Step - touch < 3000); k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick = held;
                if (!holding)
                {
                    pilot.Target = at + new Double3(0, y0 - at.Y - 0.5 * (k - 5000) * FlightCore.Dt, 0);
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
                ref readonly FlightState now = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (touch < 0 && e.Kind == EventKind.ContactMade && e.Object == WorldIds.Ground)
                    {
                        touch = now.Step;
                        r.TouchSpeed = -now.Velocity.Y;
                        touchedAt = now.Position;
                    }
                }
                if (touch >= 0)
                {
                    double acc = (now.Velocity - lastV).Length() / FlightCore.Dt;
                    r.PeakAccel = Math.Max(r.PeakAccel, acc);
                    double tilt = TiltTo(now, n);
                    r.MaxTilt = Math.Max(r.MaxTilt, tilt);
                    r.EndTilt = tilt;
                    for (int c = 0; c < 4; c++)
                    {
                        ContactModel.SkidSoil sk = core.Contacts.SoilOf(c);
                        r.PeakSink = Math.Max(r.PeakSink, Math.Max(sk.Max0, Math.Max(sk.Max1, sk.Max2)));
                    }
                }
                lastV = now.Velocity;
            }
            r.Over = r.MaxTilt > 60;
            r.EndSpeed = core.State.Velocity.Length();
            Double3 slid = core.State.Position - touchedAt;
            r.Slide = slid.Length();
            return r;
        }

        // ---- A crash into the slope ---------------------------------------------------------------------------------------

        public struct Impact
        {
            /// <summary>The facet (deg); the crash's step after the start (−1 for none), its part, speed (m/s) and peak acceleration
            /// (m/s², the event's); the deepest any part's surface reached past the slope's plane (m) and the shape that did;
            /// tunnels.</summary>
            public double Facet, Speed, PeakAccel, Depth;
            public long CrashStep;
            public int Tunnels;
            public string DepthBy;
            public string CrashBy;
        }

        /// <summary>
        /// A crash into the slope: the drone disarmed and level, its nose uphill, flying north (uphill) at speed m/s along the
        /// horizontal, its lowest part 0.1 m over the ground beneath it; 1 s. The slope is a plane, so a part's depth past it
        /// is exact: the deepest of its core's ends' distances to the plane, less its radius.
        /// </summary>
        public static Impact Crash(DroneSpec spec, double deg, double speed, bool plane = false)
        {
            FlightWorld world = Site(plane ? 0 : deg, out Double3 foot, out Double3 n);
            double stand = SoilScenarios.Standing(spec);
            double back = 0.5;
            FlightSetup setup;
            if (plane)
            {
                // The same crash in the slope's frame on the plane: the drone pitched nose-down by the slope's angle, diving at it.
                double a = Math.Atan(Units(deg) * UnitRise);
                var pitch = new Quat(Math.Sin(a / 2), 0, 0, Math.Cos(a / 2));
                double low = double.PositiveInfinity;
                foreach (ContactShape sh in SoilScenarios.Setup(spec, world, default).Contacts.Shapes)
                {
                    if (sh.Role == ContactRole.Disc) continue;
                    low = Math.Min(low, Math.Min(pitch.Rotate(sh.A).Y, pitch.Rotate(sh.B).Y) - sh.Radius);
                }
                setup = SoilScenarios.Setup(spec, world, new Double3(0, 0.1 * Math.Cos(a) - low, -back));
                setup.StartAttitude = pitch;
                setup.StartVelocity = new Double3(0, -speed * Math.Sin(a), speed * Math.Cos(a));
            }
            else
            {
                GroundSample under = Ground(world.Query, 0, -back);
                setup = SoilScenarios.Setup(spec, world, new Double3(0, under.Height + stand + 0.1, -back));
                setup.StartVelocity = new Double3(0, 0, speed);
            }
            var core = new FlightCore(setup);
            var r = new Impact { Facet = SlopeAngle(n), CrashStep = -1 };
            ContactShape[] shapes = setup.Contacts.Shapes;
            for (int k = 0; k < 1000; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.Crash && r.CrashStep < 0)
                    {
                        r.CrashStep = core.State.Step;
                        r.Speed = e.A;
                        r.PeakAccel = e.C;
                        r.CrashBy = spec.Parts[e.Part].Name;
                    }
                }
                ref readonly FlightState s = ref core.State;
                foreach (ContactShape sh in shapes)
                {
                    if (sh.Role == ContactRole.Disc) continue;
                    foreach (Double3 end in new[] { sh.A, sh.B })
                    {
                        Double3 x = s.Position + s.Attitude.Rotate(end);
                        double past = sh.Radius - Double3.Dot(n, x - foot);
                        if (past > r.Depth) r.DepthBy = sh.Name;
                        r.Depth = Math.Max(r.Depth, past);
                    }
                }
            }
            return r;
        }

        // ---- The prop's strike on the ground ----------------------------------------------------------------------------

        /// <summary>StrikeScenarios.TippedLanding (the skid catches drifting at 2.5 m/s and the drone rolls onto its lower props
        /// near the hover's speed, the throttle cut) on the slope, dry, with surfaces.json; at 0° on the stub floor of the same
        /// surface (a plane), or on the package's terrain.</summary>
        public static StrikeScenarios.TipResult Strike(DroneSpec spec, double deg, bool terrainAtZero = false) =>
            StrikeScenarios.TippedLanding(spec, Site(deg, out Double3 at, out _, terrainAtZero: terrainAtZero), at, RainLevel.Sunny, SoilScenarios.Table(), false);

        // ---- The ledge ------------------------------------------------------------------------------------------------------

        static MapWorld _farmland;

        /// <summary>maps/farmland, loaded once.</summary>
        public static MapWorld Farmland() => _farmland ?? (_farmland = MapWorld.Load(RangeScenarios.Root, "farmland"));

        /// <summary>The ledge's east step (the surround's top west of x = 2000 over the far ring) is measured at z = LedgeZ; the
        /// far seam on the same line, between far_N and far_NE, at its highest step for z in [2100, 4900].</summary>
        public const double LedgeX = 2000, LedgeZ = 500;

        public struct Ledge
        {
            /// <summary>The step's height there (m); the largest acceleration of the body (m/s²) and load on a leg (N, the trace's);
            /// the most the body rose over its start (m); its end position's x and its height over the ground beneath (m);
            /// crashes, tunnels.</summary>
            public double Step, PeakAccel, PeakLoad, Rise, EndX, EndHeight;
            public int Crashes, Tunnels;
        }

        /// <summary>
        /// A leg at a step of the farmland's ground, disarmed, its up the facet's normal under it, for 3 s. Over the lip: on the
        /// ledge's top (1.4–1.6 m), the skids along the step, the body's origin over the lip and its right skid over the far ring,
        /// so it tips over and falls the step. Into a face: on the low side of the far seam's highest step (7–10 cm: the steps a
        /// level drone meets with its skids, since its props reach 0.29 m out at 0.29 m over them, past a skid's end, and strike
        /// the ledge's face first), its nose toward the face and the skids' front ends 20 mm out, sliding at 1 m/s into it: the
        /// front supports meet the face, the leg tubes 0.12 m behind.
        /// </summary>
        public static Ledge AtLedge(DroneSpec spec, bool intoFace)
        {
            var world = new FlightWorld(Farmland());
            IWorldQuery q = world.Query;
            double z = LedgeZ;
            if (intoFace)
            {
                double best = 0;
                for (double at = 2100; at <= 4900; at += 0.5)
                {
                    double step = Math.Abs(Ground(q, LedgeX - 0.01, at).Height - Ground(q, LedgeX + 0.01, at).Height);
                    if (step > best)
                    {
                        best = step;
                        z = at;
                    }
                }
            }
            double west = Ground(q, LedgeX - 0.01, z).Height, east = Ground(q, LedgeX + 0.01, z).Height;
            double way = west > east ? -1 : 1; // into the face: from the low side toward the high one
            double stand = SoilScenarios.Standing(spec), end = 0.22, foot = 0.005;
            double x = intoFace ? LedgeX - way * (end + foot + 0.02) : LedgeX;
            GroundSample under = intoFace ? Ground(q, x, z) : Ground(q, LedgeX - 0.01, z);
            var place = new Double3(x, under.Height, z) + under.Normal * (stand - 1e-5);
            FlightSetup setup = SoilScenarios.Setup(spec, world, place);
            setup.StartAttitude = Aligned(under.Normal, intoFace ? way * 90 : 0);
            if (intoFace) setup.StartVelocity = new Double3(way, 0, 0);
            var core = new FlightCore(setup);
            var r = new Ledge { Step = Math.Abs(west - east) };
            double y0 = core.State.Position.Y;
            Double3 lastV = core.State.Velocity;
            for (int k = 0; k < 3000; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                Count(core, ref r.Crashes, ref r.Tunnels);
                r.PeakAccel = Math.Max(r.PeakAccel, (core.State.Velocity - lastV).Length() / FlightCore.Dt);
                lastV = core.State.Velocity;
                r.Rise = Math.Max(r.Rise, core.State.Position.Y - y0);
                for (int c = 0; c < 4; c++) r.PeakLoad = Math.Max(r.PeakLoad, core.Trace.LegLoad[c]);
            }
            r.EndX = core.State.Position.X;
            r.EndHeight = core.State.Position.Y - Ground(q, core.State.Position.X, core.State.Position.Z).Height;
            return r;
        }

        public struct LineLedge
        {
            /// <summary>The fastest any node of the line moved (m/s); the deepest any node lay behind the ledge's face, past it
            /// below the top (m); the line paid out (m); whether it broke, and how.</summary>
            public double FastestNode, Behind, Paid;
            public bool Broken;
            public string Break;
        }

        /// <summary>
        /// The line over the ledge (w3-farmland-ground 2.2): the drone starts on the far ring 6 m east of the ledge's east face
        /// at LedgeZ, its nose west and its line laid behind it (east) by the table's launch, the cuts on; it climbs to 3 m over
        /// the start in 3 s and flies west at 3 m/s at 3 m over the top until 15 m past the lip, then hovers, 20 s in all, so the
        /// laid line is dragged up the face and over the lip. A node past the face below the top is behind it.
        /// </summary>
        public static LineLedge LineOverLedge(DroneSpec spec)
        {
            var world = new FlightWorld(Farmland());
            IWorldQuery q = world.Query;
            FlightSetup setup = TetherScenarios.Setup(spec, cuts: true);
            double stand = TetherScenarios.Stand(setup), top = Ground(q, LedgeX - 0.01, LedgeZ).Height;
            GroundSample start = Ground(q, LedgeX + 6, LedgeZ);
            setup.World = world;
            setup.StartPosition = new Double3(LedgeX + 6, start.Height + stand, LedgeZ);
            setup.StartAttitude = new Quat(0, Math.Sin(-Math.PI / 4), 0, Math.Cos(-Math.PI / 4)); // the nose west
            var r = new LineLedge();
            Double3 s0 = setup.StartPosition;
            TetherScenarios.Fly(setup, 20, (t, core, pilot) =>
            {
                pilot.Heading = new Double3(-1, 0, 0);
                pilot.HoldVelocity = false;
                double x = t < 3 ? s0.X : Math.Max(LedgeX - 15, s0.X - 3 * (t - 3));
                pilot.Target = new Double3(x, t < 3 ? s0.Y + t : Math.Max(s0.Y + 3, top + stand + 3), LedgeZ);
            }, core =>
            {
                CableChain chain = core.Tether.Chain;
                for (int i = 0; i < chain.Count; i++)
                {
                    r.FastestNode = Math.Max(r.FastestNode, chain[i].Velocity.Length());
                    Double3 x = chain[i].Position;
                    if (x.X < LedgeX && x.X > LedgeX - 0.5) r.Behind = Math.Max(r.Behind, Ground(q, x.X, x.Z).Height - x.Y);
                }
                r.Paid = core.Tether.PaidOut;
                if (!r.Broken && core.Tether.Broken)
                {
                    r.Broken = true;
                    r.Break = core.Tether.LastBreak.Mechanism + " at step " + core.Tether.BrokenAt;
                }
            });
            return r;
        }
    }
}
