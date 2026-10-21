using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // The contact probe (p6-world-query-gallery task 1.2): a fake drone over IWorldQuery's public calls, built from
    // drone.json at L4 (the file's own loadout: 1.0 kg of payload, the coil half paid out), legs on and legs off, driven over
    // the floor stub and runtime probe shapes. Its sign-off is that change's review-physics.md.
    //
    // The parts, in body axes (DetMath.cs): four skid segments (each skid split at its middle, one per corner and carried by
    // that corner's leg) with a support sphere at the overhang end and one at the T-joint; four arms; the coil; four prop
    // discs. A cylinder part is the capsule of its own diameter and overall length. A disc is the flown prop
    // (props.diameter_m): a bounding sphere for the broadphase, and a ring of capsules round its rim once that sphere meets
    // something other than the ground.
    //
    // The body is translation-only: the probe checks what the world answers, not how the drone flies. Its contact law is
    // api-review §4's latch, corrected here (Planes), with a spring-damper along the latched normal at assumed values; 1.6
    // owns the real one.
    //
    // --probe passes on the stub. --probe --break <latch | margin | refresh | spheres | disc> swaps in one known-wrong piece
    // and must fail, which shows the checks can.
    internal static class ContactProbeCase
    {
        public const string Name = "contact-probe";
        const string DroneJson = "assets-src/drone/drone.json";

        /// <summary>The known-wrong piece a --break run swaps in.</summary>
        public enum Break
        {
            None,
            /// <summary>No latch: each contact is used as its step reports it, with no memory between steps.</summary>
            Latch,
            /// <summary>The latch with a 1 mm margin, too small to keep a skid landing on a thin rod in the result.</summary>
            Margin,
            /// <summary>api-review §4's refresh as written: any same-side Time = 1 contact moves the plane, however deep the
            /// part's core is inside the shape.</summary>
            Refresh,
            /// <summary>Each skid carried by its capsule's one contact, without the support spheres (PHB-05's first lesson).</summary>
            Spheres,
            /// <summary>Each prop disc taken as its bounding sphere, without the rim ring.</summary>
            Disc,
        }

        const double Dt = FlightCore.Dt;
        static readonly double Gravity = -FlightCore.Gravity.Y;

        /// <summary>Assumed: a frame part's series stiffness on a rigid material (the first attempt's frame on its rails,
        /// surfaces-review §1), N/m. 1.6 derives each part's own.</summary>
        const double PartK = 1e6;

        /// <summary>Assumed: the damping ratio of that compliance.</summary>
        const double PartZeta = 0.1;

        /// <summary>Derived: a latched pair must stay in the result while the part is past the shape but still inside its
        /// latched plane, so the margin covers the deepest stop less what the part needs to clear the shape. The probe's
        /// deepest is two arms at 30 m/s, v·√(m/2k) = 30·√(4.83/2e6) = 0.047 m; its tightest, skids on a 9 mm rod at 2 m/s,
        /// 2·√(4.83/2e4) − 0.019 = 0.012 m. m.</summary>
        const double Margin = 0.05;

        /// <summary>Derived: a 12-gon's chords stay within R·(1 − cos 15°) = 4.3 mm of the 0.127 m rim, the blade's 4 mm
        /// half-thickness.</summary>
        const int Ring = 12;

        /// <summary>Published: the stand-in wire, L03_medium's A-50 conductor (wires[0], diameter_m 0.009), m.</summary>
        const double WireRadius = 0.0045;

        static readonly string[] Corner = { "rr", "fr", "rl", "fl" };
        static readonly Double3 X = new Double3(1, 0, 0), Y = new Double3(0, 1, 0), Z = new Double3(0, 0, 1);
        static readonly StaticContact[] Buf = new StaticContact[16];
        static Break _break;
        static int _failed;

        public static bool Run(Break b)
        {
            _break = b;
            _failed = 0;
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(FindRepoFile(DroneJson)));
            Drone on = Build(spec, true), off = Build(spec, false);
            Console.WriteLine($"{Name}: drone.json at L4 (hash 0x{spec.ContentHash:X16}): legs on {on.Mass:F3} kg, legs off {off.Mass:F3} kg; "
                + $"break: {b.ToString().ToLowerInvariant()}");
            Check("geometry", on.SkidRadius == spec.Legs.FootRadius && Math.Abs(on.Support[0].Y - on.SkidRadius + 0.253) < 1e-12
                && Math.Abs(on.Skid[1].A.Z - on.Support[3].Z - 0.121) < 1e-12,
                $"skids at y {on.Support[0].Y - on.SkidRadius:F3} m with r {on.SkidRadius} m (legs.foot_radius_m), the overhang {on.Skid[1].A.Z - on.Support[3].Z:F3} m "
                + $"past each T-joint to the core end; arms r {on.ArmRadius} m; coil r {on.CoilRadius} m at y {on.Coil.A.Y} (legs off {off.Coil.A.Y}); "
                + $"discs R {on.DiscRadius} m, half-thickness {on.DiscHalf} m");
            Resting(on);
            Sliding(on);
            Dropping(on);
            Stand(spec, off);
            Sweeps(on);
            Props(on);
            Console.WriteLine($"{Name}: {(_failed == 0 ? "PASS" : $"FAIL ({_failed} checks)")}");
            return _failed == 0;
        }

        // ---- The probe drone ----------------------------------------------------------------------------------------

        struct Segment
        {
            public Double3 A, B;

            public Segment(Double3 a, Double3 b)
            {
                A = a;
                B = b;
            }
        }

        /// <summary>The contact parts in body axes, read from drone.json's parts at a loadout.</summary>
        sealed class Drone
        {
            public double Mass, LegK, LegC;
            public readonly Segment[] Skid = new Segment[4];    // A: the overhang end; B: the skid's middle
            public readonly Double3[] Support = new Double3[8]; // [2c]: the overhang end, [2c + 1]: the T-joint
            public double SkidRadius;
            public readonly Segment[] Arm = new Segment[4];
            public double ArmRadius;
            public Segment Coil;
            public double CoilRadius;
            public readonly Double3[] Hub = new Double3[4];
            public double DiscRadius, DiscHalf;
        }

        static Drone Build(DroneSpec spec, bool legs)
        {
            Loadout loadout = spec.Nominal;
            loadout.Legs = legs;
            MassModel model = spec.BuildMass(loadout);
            var d = new Drone { Mass = model.Properties.Mass, LegK = spec.Legs.Stiffness, LegC = spec.Legs.Damping };
            for (int c = 0; c < 4; c++)
            {
                DronePart arm = model.Part(spec.PartIndex("arm_" + Corner[c]));
                d.Arm[c] = Core(arm);
                d.ArmRadius = arm.Diameter / 2;
                d.Hub[c] = spec.Motors[c].Position;
                if (!legs) continue;
                DronePart leg = model.Part(spec.Legs.Parts[c]);
                DronePart skid = model.Part(spec.PartIndex(leg.Position.X > 0 ? "skid_right" : "skid_left"));
                double r = skid.Diameter / 2, reach = skid.Length / 2 - r;
                Double3 end = skid.Position + skid.Axis * (leg.Position.Z > 0 ? reach : -reach);
                d.Skid[c] = new Segment(end, skid.Position);
                d.Support[2 * c] = end;
                d.Support[2 * c + 1] = new Double3(leg.Position.X, skid.Position.Y, leg.Position.Z);
                d.SkidRadius = r;
            }
            DronePart coil = model.Part(spec.Coil.Part);
            d.Coil = Core(coil);
            d.CoilRadius = coil.Diameter / 2;
            d.DiscRadius = spec.Props.Diameter / 2;
            d.DiscHalf = model.Part(spec.PartIndex("prop_rr")).Length / 2;
            return d;
        }

        /// <summary>A cylinder part as the capsule of its diameter and overall length: its axis, short by a radius at each end.</summary>
        static Segment Core(in DronePart p)
        {
            Double3 h = p.Axis * (p.Length / 2 - p.Diameter / 2);
            return new Segment(p.Position - h, p.Position + h);
        }

        struct Pose
        {
            public Double3 P;
            public Quat Q;

            public Pose(Double3 p, Quat q)
            {
                P = p;
                Q = q;
            }

            public Double3 W(Double3 body) => P + Q.Rotate(body);

            public Capsule Of(in Segment s, double r) => new Capsule(W(s.A), W(s.B), r);

            public Capsule Ball(Double3 body, double r)
            {
                Double3 x = W(body);
                return new Capsule(x, x, r);
            }
        }

        /// <summary>A positive yaw turns north towards east (DetMath.cs).</summary>
        static Quat Yaw(double rad) => new Quat(0, Math.Sin(rad / 2), 0, Math.Cos(rad / 2));

        /// <summary>A positive angle about body x puts the nose down (DetMath.cs).</summary>
        static Quat NoseDown(double rad) => new Quat(Math.Sin(rad / 2), 0, 0, Math.Cos(rad / 2));

        // ---- The probe's contact law ----------------------------------------------------------------------------------

        /// <summary>
        /// One part's contact planes under api-review §4 (K§7.5), with this probe's correction. A first touch (Time &lt; 1)
        /// latches its point and normal; a later Time = 1 contact of the pair with a same-side normal refreshes the plane,
        /// and a flipped one or another Time &lt; 1 keeps it. The correction: a contact whose Distance puts the part's core
        /// inside the shape (Distance &lt; −r) never moves the plane, since the closest-point normal there is no longer the
        /// contact's: a part driven past a thin round shape's axis sees it turn round by less than 90° a step, so the
        /// same-side test alone lets the plane follow the part through or under. A latch goes when its plane distance
        /// passes the margin or its pair leaves the result; leaving while the plane still shows penetration, after a
        /// flip or with the part's deepest core end past the shape's centre (Geometry) along the normal it came in by, is a
        /// tunnel event. Break.Latch uses each contact as reported, with no memory; Break.Refresh drops the correction.
        /// </summary>
        sealed class Planes
        {
            struct Slot
            {
                public int Object, Shape;
                public Double3 P, N, Entry; // the plane, and the normal the part came in by
                public bool Latched, Flipped, Seen, Touching; // Touching: the plane showed penetration after the last update
            }

            readonly Slot[] _slot = new Slot[8];
            int _count;
            public int TunnelEvents, Latches;

            public int Count => _count;
            public Double3 Normal(int i) => _slot[i].N;

            public void Clear()
            {
                _count = 0;
                TunnelEvents = 0;
                Latches = 0;
            }

            /// <summary>The part's penetration of plane i, r − min(n·(A − P), n·(B − P)), m: at most 0 is no force.</summary>
            public double Depth(int i, in Capsule part) => part.Radius
                - Math.Min(Double3.Dot(_slot[i].N, part.A - _slot[i].P), Double3.Dot(_slot[i].N, part.B - _slot[i].P));

            public void Update(IWorldQuery world, ReadOnlySpan<StaticContact> found, in Capsule part, double margin, bool latch)
            {
                if (!latch) _count = 0;
                for (int i = 0; i < _count; i++) _slot[i].Seen = false;
                foreach (StaticContact c in found)
                {
                    int k = 0;
                    while (k < _count && (_slot[k].Object != c.Object || _slot[k].Shape != c.Shape)) k++;
                    if (k == _slot.Length) throw new InvalidOperationException("the probe holds 8 pairs a part");
                    ref Slot s = ref _slot[k];
                    if (k == _count)
                    {
                        _count++;
                        s = new Slot { Object = c.Object, Shape = c.Shape, P = c.Point, N = c.Normal, Entry = c.Normal, Latched = latch && c.Time < 1 };
                        if (s.Latched) Latches++;
                    }
                    else if (c.Time < 1)
                    {
                        if (!s.Latched)
                        {
                            // A first touch starts a contact; a Time 0 (through during the step) continues one.
                            s.Entry = s.Touching ? s.Entry : c.Normal;
                            s.P = c.Point;
                            s.N = c.Normal;
                            s.Latched = true;
                            s.Flipped = false;
                            Latches++;
                        }
                    }
                    else if (_break != Break.Refresh && c.Distance < -part.Radius)
                    {
                        s.Latched = true; // the core is inside the shape: the plane stays where it was
                    }
                    else if (!s.Latched || Double3.Dot(c.Normal, s.N) > 0)
                    {
                        if (!s.Touching) s.Entry = c.Normal;
                        s.P = c.Point; // refreshed: an ordinary contact
                        s.N = c.Normal;
                        s.Latched = false;
                        s.Flipped = false;
                    }
                    else
                    {
                        s.Flipped = true;
                    }
                    s.Seen = true;
                }
                int kept = 0;
                for (int i = 0; i < _count; i++)
                {
                    Slot s = _slot[i];
                    double depth = Depth(i, part);
                    if (!s.Seen)
                    {
                        if (s.Latched && depth > 0 && (s.Flipped || PastCentre(world, s, part))) TunnelEvents++;
                        continue;
                    }
                    if (s.Latched && -depth > margin) continue;
                    s.Touching = depth > 0;
                    _slot[kept++] = s;
                }
                _count = kept;
            }

            /// <summary>The part's deepest core end lies past the shape's centre along the normal it came in by.</summary>
            static bool PastCentre(IWorldQuery world, in Slot s, in Capsule part)
            {
                if (!world.Geometry(s.Object, s.Shape, out ShapeGeometry g) || g.Kind == ShapeKind.Plane) return false;
                Double3 deep = Double3.Dot(s.Entry, part.A) <= Double3.Dot(s.Entry, part.B) ? part.A : part.B;
                return Double3.Dot(s.Entry, deep - g.Center) < 0;
            }
        }

        /// <summary>The probe's normal law on each of a part's planes: k·δ − c·(v·n) along n, never pulling.</summary>
        static Double3 Push(Planes planes, in Capsule part, Double3 velocity, double k, double c, ref double deepest)
        {
            Double3 f = default;
            for (int i = 0; i < planes.Count; i++)
            {
                double depth = planes.Depth(i, part);
                if (!(depth > 0)) continue;
                deepest = Math.Max(deepest, depth);
                Double3 n = planes.Normal(i);
                double push = k * depth - c * Double3.Dot(velocity, n);
                if (push > 0) f = f + n * push;
            }
            return f;
        }

        static double Damping(double k, double zeta, double mass) => 2 * zeta * Math.Sqrt(k * mass);

        // ---- The world ---------------------------------------------------------------------------------------------

        static FloorWorld World() => new FloorWorld(0, "belt_bare", "steel", "timber", "cable");

        static ShapeGeometry Box(Double3 centre, Double3 half, ushort material, Double3 ax, Double3 az) => new ShapeGeometry
        {
            Kind = ShapeKind.Box, Center = centre, AxisX = ax, AxisY = Y, AxisZ = az, HalfExtents = half, Material = material,
        };

        /// <summary>A capsule along world x: AxisY = x, AxisX = y and AxisZ = −z, so AxisX × AxisY = AxisZ.</summary>
        static ShapeGeometry AlongX(Double3 centre, double radius, double halfLength, ushort material) => new ShapeGeometry
        {
            Kind = ShapeKind.Capsule, Center = centre, AxisX = Y, AxisY = X, AxisZ = new Double3(0, 0, -1),
            Radius = radius, HalfLength = halfLength, Material = material,
        };

        /// <summary>drone.json's stand (D-022, D-043) centred at (x, 0, z): a square post on each motor diagonal (shape c
        /// under corner c) and an X base of the same tube (shapes 4 and 5).</summary>
        static int AddStand(IWorldQuery w, DroneSpec spec, double x, double z)
        {
            ushort steel = w.MaterialId(spec.Stand.Material);
            double a = spec.Stand.PostDiameter / 2, h = spec.Stand.PostHeight, r = spec.Stand.PostRadius / Math.Sqrt(2), s = 1 / Math.Sqrt(2);
            var shapes = new ShapeGeometry[6];
            for (int c = 0; c < 4; c++)
                shapes[c] = Box(new Double3(x + Math.Sign(spec.Motors[c].Position.X) * r, h / 2, z + Math.Sign(spec.Motors[c].Position.Z) * r),
                    new Double3(a, h / 2, a), steel, X, Z);
            var arm = new Double3(spec.Stand.PostRadius + a, a, a);
            shapes[4] = Box(new Double3(x, a, z), arm, steel, new Double3(s, 0, s), new Double3(-s, 0, s));
            shapes[5] = Box(new Double3(x, a, z), arm, steel, new Double3(s, 0, -s), new Double3(s, 0, s));
            return w.AddObject("stand", shapes);
        }

        // ---- Resting (legs on) ------------------------------------------------------------------------------------------

        /// <summary>The static leg compression on the rigid floor: a quarter of the weight on each leg spring, m (derived).</summary>
        static double Sink(Drone d) => d.Mass * Gravity / (4 * d.LegK);

        /// <summary>The body height that puts the lowest support sphere `depth` into the floor at this attitude.</summary>
        static double HeightFor(Drone d, Quat q, double depth)
        {
            double low = double.PositiveInfinity;
            foreach (Double3 s in d.Support) low = Math.Min(low, q.Rotate(s).Y);
            return d.SkidRadius - low - depth;
        }

        /// <summary>
        /// The support points of corner c (world), each with its depth, as the representation under test gives them: the
        /// two support spheres on SampleGround's ground, plus the skid capsule's object contacts that are not within a
        /// skid radius of a touching sphere's point (a crossing between the spheres); or, under Break.Spheres, the
        /// capsule's own contacts, the ground's among them.
        /// </summary>
        static int Supports(IWorldQuery w, Drone d, int c, in Pose prev, in Pose pose, Span<Double3> points, Span<double> depths)
        {
            Capsule now = pose.Of(d.Skid[c], d.SkidRadius), was = prev.Of(d.Skid[c], d.SkidRadius);
            int found = w.StaticContacts(was, now, 0, Buf), n = 0;
            if (_break == Break.Spheres)
            {
                for (int i = 0; i < found; i++)
                {
                    if (!(Buf[i].Distance < 0)) continue;
                    points[n] = Buf[i].Point;
                    depths[n++] = -Buf[i].Distance;
                }
                return n;
            }
            Span<XZ> at = stackalloc XZ[2];
            Span<GroundSample> ground = stackalloc GroundSample[2];
            for (int k = 0; k < 2; k++)
            {
                Double3 x = pose.W(d.Support[2 * c + k]);
                at[k] = new XZ(x.X, x.Z);
            }
            w.SampleGround(at, ground);
            for (int k = 0; k < 2; k++)
            {
                Double3 x = pose.W(d.Support[2 * c + k]);
                double depth = (ground[k].Height - (x.Y - d.SkidRadius)) * ground[k].Normal.Y; // K§7.1 step 2
                if (!(depth > 0)) continue;
                points[n] = new Double3(x.X, ground[k].Height, x.Z);
                depths[n++] = depth;
            }
            int spheres = n;
            for (int i = 0; i < found; i++)
            {
                if (Buf[i].Object == WorldIds.Ground || !(Buf[i].Distance < 0)) continue;
                bool duplicate = false;
                for (int k = 0; k < spheres; k++)
                    duplicate |= (Buf[i].Point - points[k]).Length() <= d.SkidRadius + d.SkidRadius;
                if (duplicate) continue;
                points[n] = Buf[i].Point;
                depths[n++] = -Buf[i].Distance;
            }
            return n;
        }

        static void Resting(Drone d)
        {
            const string S = "resting, legs on";
            var w = World();
            double sink = Sink(d);
            foreach (double yaw in new[] { 0.0, Math.Atan2(3, 4) })
            {
                var pose = new Pose(new Double3(0.4, HeightFor(d, Yaw(yaw), sink), -0.3), Yaw(yaw));
                var at = new XZ[8];
                var ground = new GroundSample[8];
                for (int i = 0; i < 8; i++)
                {
                    Double3 x = pose.W(d.Support[i]);
                    at[i] = new XZ(x.X, x.Z);
                }
                w.SampleGround(at, ground);
                double worst = 0;
                bool fields = true;
                for (int i = 0; i < 8; i++)
                {
                    Capsule ball = pose.Ball(d.Support[i], d.SkidRadius);
                    double depth = (ground[i].Height - (ball.A.Y - d.SkidRadius)) * ground[i].Normal.Y;
                    int n = w.StaticContacts(ball, ball, 0, Buf);
                    StaticContact c = Buf[0];
                    fields &= n == 1 && c.Object == WorldIds.Ground && c.Shape == 0 && c.Time == 1 && c.WireParam == -1
                        && c.Material == WorldIds.NoMaterial && w.SurfaceName(c.Surface) == "belt_bare" && c.Normal.Y == 1
                        && ground[i].Flags == GroundFlags.None && w.SurfaceName(ground[i].Surface) == "belt_bare";
                    worst = Math.Max(worst, Math.Max(Math.Abs(depth - sink), Math.Abs(-c.Distance - depth)));
                }
                Check(S, fields && worst < 1e-12, $"yaw {yaw:F3} rad: the 8 support spheres meet the floor (object -1, shape 0, belt_bare, Time 1) "
                    + $"{sink * 1e3:F3} mm deep (m·g/4k); SampleGround and StaticContacts agree within {worst:E1} m");
            }

            // PHB-05 lesson 1 on the floor: a skid's capsule gives one contact at a core end, and a micro-radian of pitch
            // moves it 0.22 m; the spheres' support span does not move.
            var front = new double[2];
            var rear = new double[2];
            Span<Double3> points = stackalloc Double3[8];
            Span<double> depths = stackalloc double[8];
            for (int s = 0; s < 2; s++)
            {
                Quat q = NoseDown(s == 0 ? 1e-6 : -1e-6);
                var pose = new Pose(new Double3(0, HeightFor(d, q, sink), 0), q);
                front[s] = double.NegativeInfinity;
                rear[s] = double.PositiveInfinity;
                for (int c = 0; c < 4; c++)
                {
                    int n = Supports(w, d, c, pose, pose, points, depths);
                    for (int k = 0; k < n; k++)
                    {
                        double z = pose.Q.InverseRotate(points[k] - pose.P).Z;
                        front[s] = Math.Max(front[s], z);
                        rear[s] = Math.Min(rear[s], z);
                    }
                }
            }
            Check(S, Math.Abs(front[0] - front[1]) < 1e-3 && Math.Abs(rear[0] - rear[1]) < 1e-3, $"the supports span z {rear[0]:F4} to {front[0]:F4} m "
                + $"nose down 1 µrad and {rear[1]:F4} to {front[1]:F4} m nose up (a capsule alone keeps one core end a skid)");

            // p5-drone's note: pitched 15° nose up, the overhang ends touch 31 mm before the T-joints would.
            {
                Quat q = NoseDown(-15 * Math.PI / 180);
                var pose = new Pose(new Double3(-0.5, HeightFor(d, q, 1e-4), 0.5), q);
                int touching = 0;
                bool ends = true;
                for (int c = 0; c < 4; c++)
                {
                    int n = Supports(w, d, c, pose, pose, points, depths);
                    touching += n;
                    for (int k = 0; k < n; k++)
                        ends &= Math.Abs(pose.Q.InverseRotate(points[k] - pose.P).Z - d.Support[2 * c].Z) < d.SkidRadius + 1e-9 && c % 2 == 0;
                }
                double joint = pose.W(d.Support[1]).Y - d.SkidRadius;
                double expected = (d.Support[1].Z - d.Support[0].Z) * Math.Sin(15 * Math.PI / 180) - 1e-4;
                Check(S, touching == 2 && ends && Math.Abs(joint - expected) < 1e-9, $"pitched 15° nose up, only the two rear overhang ends touch; the rear "
                    + $"T-joints are {joint * 1e3:F1} mm clear, so four point feet would let the skids {joint * 1e3 + 0.1:F1} mm into the floor first");
            }

            // A crossing between the spheres: a 20 mm steel tube on the floor under the front-right skid, between its T-joint
            // and its overhang end. No sphere meets it; the capsule does.
            {
                var pose = new Pose(new Double3(2, 0, 2), Quat.Identity);
                double between = (d.Support[2].Z + d.Support[3].Z) / 2;
                w.AddObject("tube", new[] { AlongX(new Double3(2 + d.Support[2].X, 0.01, 2 + between), 0.01, 0.05, w.MaterialId("steel")) });
                pose.P.Y = 0.02 - (d.Support[2].Y - d.SkidRadius) - 0.002;
                int n = Supports(w, d, 1, pose, pose, points, depths);
                Capsule ball = pose.Ball(d.Support[2], d.SkidRadius), joint = pose.Ball(d.Support[3], d.SkidRadius);
                int sphereHits = w.StaticContacts(ball, ball, 0, Buf) + w.StaticContacts(joint, joint, 0, Buf);
                Check(S, n == 1 && Math.Abs(depths[0] - 0.002) < 1e-12 && Math.Abs(points[0].Z - (2 + between)) < 1e-9 && sphereHits == 0,
                    $"a skid across a 20 mm tube between its spheres: the capsule's contact carries it {depths[0] * 1e3:F3} mm deep at "
                    + $"z {points[0].Z - 2:F4} m; {sphereHits} sphere contacts");
                w.ResetRuntimeObjects();
            }

            // The rotor rays at rest: down to the floor at the hub's height, up into nothing.
            {
                var pose = new Pose(new Double3(0, HeightFor(d, Quat.Identity, sink), 0), Quat.Identity);
                var rays = new Ray[8];
                var hits = new RayHit[8];
                for (int c = 0; c < 4; c++)
                {
                    rays[2 * c] = new Ray(pose.W(d.Hub[c]), new Double3(0, -1, 0));
                    rays[2 * c + 1] = new Ray(pose.W(d.Hub[c]), Y);
                }
                w.Raycast(rays, 10, hits);
                bool ok = true;
                for (int c = 0; c < 4; c++)
                {
                    RayHit down = hits[2 * c], up = hits[2 * c + 1];
                    ok &= Math.Abs(down.Distance - rays[2 * c].Origin.Y) < 1e-12 && down.Object == WorldIds.Ground && down.Normal.Y == 1
                        && w.SurfaceName(down.Surface) == "belt_bare" && double.IsPositiveInfinity(up.Distance) && up.Object == WorldIds.Ground
                        && up.Surface == WorldIds.NoSurface;
                }
                Check(S, ok, $"the rotor rays: down to the floor at the hub height {rays[0].Origin.Y:F4} m (belt_bare), up a miss (+inf, surface 0)");
            }
        }

        // ---- Sliding (legs on) ------------------------------------------------------------------------------------------

        /// <summary>
        /// 1 s along the floor at (1, 0, 2) m/s, yawed, rocking ±0.02 rad in pitch at 2 Hz, the lowest sphere held at the
        /// static sink. A sphere in contact at both ends of a step must report Time = 1, SampleGround and StaticContacts must
        /// agree, and each corner's support moment must change smoothly (PHB-05's first lesson again).
        /// </summary>
        static void Sliding(Drone d)
        {
            const string S = "sliding, legs on";
            var w = World();
            double sink = Sink(d), worstJump = 0, worstAgree = 0;
            var velocity = new Double3(1, 0, 2);
            int stays = 0, badTime = 0;
            Span<Double3> points = stackalloc Double3[8];
            Span<double> depths = stackalloc double[8];
            var pitch = new double[4];
            var sample = new GroundSample[1];
            Pose prev = default;
            for (int k = 0; k <= 1000; k++)
            {
                double t = k * Dt;
                Quat q = Yaw(0.3) * NoseDown(0.02 * Math.Sin(2 * Math.PI * 2 * t));
                var pose = new Pose(new Double3(-2 + velocity.X * t, HeightFor(d, q, sink), -2 + velocity.Z * t), q);
                if (k == 0) prev = pose;
                for (int i = 0; i < 8; i++)
                {
                    Capsule was = prev.Ball(d.Support[i], d.SkidRadius), now = pose.Ball(d.Support[i], d.SkidRadius);
                    int n = w.StaticContacts(was, now, 0, Buf);
                    w.SampleGround(new[] { new XZ(now.A.X, now.A.Z) }, sample);
                    double depthNow = sample[0].Height - (now.A.Y - d.SkidRadius), depthWas = -(was.A.Y - d.SkidRadius);
                    // The contact's plane at the current pose (at a first touch, its latched plane) against SampleGround's depth.
                    if (n > 0) worstAgree = Math.Max(worstAgree, Math.Abs(d.SkidRadius - Double3.Dot(Buf[0].Normal, now.A - Buf[0].Point) - depthNow));
                    if (depthNow > 0 && depthWas > 0 && k > 0)
                    {
                        stays++;
                        if (n != 1 || Buf[0].Time != 1) badTime++;
                    }
                }
                for (int c = 0; c < 4; c++)
                {
                    int n = Supports(w, d, c, prev, pose, points, depths);
                    double moment = 0;
                    for (int i = 0; i < n; i++) moment += d.LegK * depths[i] * pose.Q.InverseRotate(points[i] - pose.P).Z;
                    if (k > 0) worstJump = Math.Max(worstJump, Math.Abs(moment - pitch[c]));
                    pitch[c] = moment;
                }
                prev = pose;
            }
            Check(S, badTime == 0 && worstAgree < 1e-12, $"{stays} sphere-steps in contact at both ends of the step, {badTime} not reported as one "
                + $"Time = 1 contact; SampleGround and StaticContacts agree within {worstAgree:E1} m");
            double whole = d.LegK * sink * (d.Support[2].Z - d.Skid[1].B.Z);
            Check(S, worstJump < 0.2 * whole, $"each corner's support moment about the arm plane (Σ k·δ·z) changes at most {worstJump:F3} N·m a step "
                + $"(limit a fifth of a skid end's whole {whole:F2} N·m)");
        }

        // ---- Dropping onto the floor (legs on) ----------------------------------------------------------------------------

        /// <summary>
        /// A free fall of 0.30 m onto the floor, level and yawed, the throttle cut: the legs' spring-dampers on SampleGround
        /// (the supports' deepest per corner), the spheres' StaticContacts beside them for the first touch's Time.
        /// </summary>
        static void Dropping(Drone d)
        {
            const string S = "dropping onto the floor, legs on";
            var w = World();
            double sink = Sink(d), fall = 0.30;
            Quat q = Yaw(Math.Atan2(3, 4));
            var pose = new Pose(new Double3(0.7, HeightFor(d, q, -fall), 0.2), q);
            double vy = 0, peak = 0, apex = double.NegativeInfinity, worstPlane = 0;
            double firstTime = 2, analytic = 2, firstDistance = double.NaN, closing = 0;
            bool left = false, landed = false;
            var at = new XZ[8];
            var ground = new GroundSample[8];
            for (int k = 0; k < 3000; k++)
            {
                for (int i = 0; i < 8; i++)
                {
                    Double3 x = pose.W(d.Support[i]);
                    at[i] = new XZ(x.X, x.Z);
                }
                w.SampleGround(at, ground);
                double force = 0, bottom = double.PositiveInfinity;
                for (int c = 0; c < 4; c++)
                {
                    double deepest = double.NegativeInfinity;
                    for (int s = 2 * c; s < 2 * c + 2; s++)
                    {
                        double y = pose.W(d.Support[s]).Y - d.SkidRadius;
                        bottom = Math.Min(bottom, y);
                        deepest = Math.Max(deepest, (ground[s].Height - y) * ground[s].Normal.Y);
                    }
                    if (deepest > 0) force += Math.Max(0, d.LegK * deepest - d.LegC * vy);
                }
                peak = Math.Max(peak, force);
                if (landed && bottom > 0) left = true;
                if (left && bottom > apex) apex = bottom;
                if (bottom < 0) landed = true;
                Pose prev = pose;
                vy += (force / d.Mass - Gravity) * Dt;
                pose.P.Y += vy * Dt;
                for (int s = 0; s < 8; s++)
                {
                    Capsule was = prev.Ball(d.Support[s], d.SkidRadius), now = pose.Ball(d.Support[s], d.SkidRadius);
                    int n = w.StaticContacts(was, now, 0, Buf);
                    if (n == 0) continue;
                    // The latched plane's depth at the current pose against SampleGround's.
                    double plane = d.SkidRadius - Double3.Dot(Buf[0].Normal, now.A - Buf[0].Point);
                    worstPlane = Math.Max(worstPlane, Math.Abs(plane - (0 - (now.A.Y - d.SkidRadius))));
                    if (Buf[0].Time < 1 && firstTime == 2)
                    {
                        double before = was.A.Y - d.SkidRadius, after = now.A.Y - d.SkidRadius;
                        firstTime = Buf[0].Time;
                        firstDistance = Buf[0].Distance;
                        analytic = before / (before - after);
                        closing = before - after;
                    }
                }
            }
            double rest = -(pose.W(d.Support[0]).Y - d.SkidRadius);
            double slack = 1e-6 / closing;
            Check(S, firstTime <= analytic + 1e-12 && analytic - firstTime <= slack + 1e-12 && firstDistance >= -1e-12 && firstDistance <= 1e-6 && worstPlane < 1e-12,
                $"the first touch reports Time {firstTime:F6} (analytic {analytic:F6}, conservative within 1 µm / {closing * 1e3:F2} mm), Distance "
                + $"{firstDistance:E1} m; its latched plane matches SampleGround's depth within {worstPlane:E1} m");
            Check(S, Math.Abs(rest - sink) < 1e-3 * sink && Math.Abs(vy) < 1e-6, $"at rest after 3 s {rest * 1e3:F4} mm deep (m·g/4k {sink * 1e3:F4} mm), "
                + $"|v| {Math.Abs(vy):E1} m/s");
            Info(S, $"from {fall:F2} m at {Math.Sqrt(2 * Gravity * fall):F2} m/s: peak leg force {peak / (d.Mass * Gravity):F1} g, then a hop of "
                + $"{apex * 1e3:F1} mm with the throttle cut (drone.json's k {d.LegK} N/m and c {d.LegC} N·s/m on a rigid floor; for 1.6's bounce scenario)");
            OntoRod(d);
        }

        /// <summary>
        /// A landing at 2 m/s onto a 9 mm steel rod across both front skids, between their T-joints and overhang ends, a
        /// metre above the floor: the skid capsules' contacts through the latch, each front leg's spring-damper along the
        /// latched normal. The legs' give lets the skids pass the rod's axis, and the 2 × 10 kN/m stop is 31 mm, more than the
        /// 19 mm a skid needs to clear the rod, so only a margin over that keeps the pair in the result.
        /// </summary>
        static void OntoRod(Drone d)
        {
            const string S = "landing on a rod, legs on";
            double margin = _break == Break.Margin ? 0.001 : Margin;
            bool latch = _break != Break.Latch;
            var w = World();
            double between = (d.Support[2].Z + d.Support[3].Z) / 2, rodY = 1.0;
            w.AddObject("rod", new[] { AlongX(new Double3(0, rodY, between), WireRadius, 1, w.MaterialId("steel")) });
            var pose = new Pose(new Double3(0, rodY + WireRadius + 0.03 - (d.Support[2].Y - d.SkidRadius), 0), Quat.Identity);
            var planes = new Planes[4];
            for (int c = 0; c < 4; c++) planes[c] = new Planes();
            double vy = -2, deepest = 0;
            int latches = 0, events = 0;
            for (int k = 0; k < 2000; k++)
            {
                Pose prev = pose;
                double force = 0;
                for (int c = 0; c < 4; c++)
                    force += Push(planes[c], pose.Of(d.Skid[c], d.SkidRadius), new Double3(0, vy, 0), d.LegK, d.LegC, ref deepest).Y;
                vy += (force / d.Mass - Gravity) * Dt;
                pose.P.Y += vy * Dt;
                for (int c = 0; c < 4; c++)
                {
                    Capsule was = prev.Of(d.Skid[c], d.SkidRadius), now = pose.Of(d.Skid[c], d.SkidRadius);
                    int n = w.StaticContacts(was, now, margin, Buf);
                    planes[c].Update(w, new ReadOnlySpan<StaticContact>(Buf, 0, Math.Min(n, Buf.Length)), now, margin, latch);
                }
            }
            for (int c = 0; c < 4; c++)
            {
                latches += planes[c].Latches;
                events += planes[c].TunnelEvents;
            }
            double above = pose.W(d.Support[2]).Y - rodY, rest = d.Mass * Gravity / (2 * d.LegK);
            Check(S, above > 0 && Math.Abs(above - (d.SkidRadius + WireRadius - rest)) < 0.01 * rest && events == 0 && latches >= 2,
                $"the front skids end {above * 1e3:F2} mm above the rod's axis (resting on it: {(d.SkidRadius + WireRadius - rest) * 1e3:F2} mm, "
                + $"m·g/2k {rest * 1e3:F2} mm deep), {latches} first touches latched, the deepest {deepest * 1e3:F1} mm, {events} tunnel events "
                + $"(latch {(latch ? "on" : "off")}, margin {margin * 1e3:F0} mm)");
        }

        // ---- The stand (legs off) ---------------------------------------------------------------------------------------

        static void Stand(DroneSpec spec, Drone d)
        {
            const string S = "the stand, legs off";
            var w = World();
            int stand = AddStand(w, spec, -1.5, 1.0);
            double top = spec.Stand.PostHeight, sink = d.Mass * Gravity / (4 * PartK);
            var rest = new Pose(new Double3(-1.5, top + d.ArmRadius - sink, 1.0), Quat.Identity);

            bool ok = true;
            for (int c = 0; c < 4; c++)
            {
                Capsule arm = rest.Of(d.Arm[c], d.ArmRadius);
                int n = w.StaticContacts(arm, arm, Margin, Buf);
                StaticContact h = Buf[0];
                ok &= n == 1 && h.Object == stand && h.Shape == c && w.MaterialName(h.Material) == "steel" && h.Time == 1 && h.Normal.Y == 1
                    && Math.Abs(h.Distance + sink) < 1e-12 && Math.Abs(h.Point.Y - top) < 1e-12;
            }
            Capsule coil = rest.Of(d.Coil, d.CoilRadius);
            int coilHits = w.StaticContacts(coil, coil, Margin, Buf);
            var below = new RayHit[1];
            Double3 middle = (coil.A + coil.B) * 0.5;
            w.Raycast(new[] { new Ray(new Double3(middle.X, middle.Y - d.CoilRadius, middle.Z), new Double3(0, -1, 0)) }, 1, below);
            Check(S, ok && coilHits == 0 && below[0].Object == stand, $"resting: one contact per arm, on its own post (object {stand} '{w.ObjectName(stand)}', "
                + $"shape = corner, steel, Time 1, {sink * 1e6:F1} µm deep); the coil hangs clear, {below[0].Distance:F4} m above the base "
                + $"(drone.json's note: 0.135 m with the 6 mm plate; the probe's arm is the 21 mm mass cylinder)");

            // The first attempt's F2: after a reset and a second stand, still one contact per arm.
            w.ResetRuntimeObjects();
            int again = AddStand(w, spec, -1.5, 1.0);
            int contacts = 0;
            for (int c = 0; c < 4; c++)
            {
                Capsule arm = rest.Of(d.Arm[c], d.ArmRadius);
                contacts += w.StaticContacts(arm, arm, Margin, Buf);
            }
            Check(S, contacts == 4 && again == stand, $"after ResetRuntimeObjects and the stand added again: {contacts} arm contacts, object {again}");

            // Set down from 5 cm onto the posts, then lifted off at 2 m/s.
            var planes = new Planes[4];
            for (int c = 0; c < 4; c++) planes[c] = new Planes();
            var pose = new Pose(rest.P + new Double3(0, 0.05, 0), Quat.Identity);
            double vy = 0, c4 = Damping(PartK, PartZeta, d.Mass / 4);
            int latches = 0, tunnels = 0, lifted = 0;
            for (int k = 0; k < 2500; k++)
            {
                Pose prev = pose;
                if (k < 2000)
                {
                    double force = 0, deepest = 0;
                    for (int c = 0; c < 4; c++)
                        force += Push(planes[c], pose.Of(d.Arm[c], d.ArmRadius), new Double3(0, vy, 0), PartK, c4, ref deepest).Y;
                    vy += (force / d.Mass - Gravity) * Dt;
                }
                else
                {
                    vy = 2;
                }
                pose.P.Y += vy * Dt;
                for (int c = 0; c < 4; c++)
                {
                    Capsule was = prev.Of(d.Arm[c], d.ArmRadius), now = pose.Of(d.Arm[c], d.ArmRadius);
                    int n = w.StaticContacts(was, now, Margin, Buf);
                    planes[c].Update(w, new ReadOnlySpan<StaticContact>(Buf, 0, Math.Min(n, Buf.Length)), now, Margin, _break != Break.Latch);
                }
                if (k == 1999)
                {
                    double settled = top + d.ArmRadius - pose.P.Y;
                    Check(S, Math.Abs(settled - sink) < 0.01 * sink, $"set down from 5 cm: at rest {settled * 1e6:F2} µm deep on the posts (m·g/4k "
                        + $"{sink * 1e6:F2} µm)");
                }
            }
            for (int c = 0; c < 4; c++)
            {
                latches += planes[c].Latches;
                tunnels += planes[c].TunnelEvents;
                lifted += planes[c].Count;
            }
            Check(S, latches >= 4 && tunnels == 0 && lifted == 0, $"{latches} first touches latched on the posts, {tunnels} tunnel events; lifted "
                + $"{0.5 * 2:F1} m, {lifted} pairs still held");

            // Set down level on the bare floor without the stand: the spool is the first part to touch (D-043).
            {
                var floor = World();
                var p = new Pose(new Double3(0, 0.3, 0), Quat.Identity);
                var parts = new (string Name, Segment Core, double Radius)[5 + 4 * Ring];
                parts[0] = ("coil", d.Coil, d.CoilRadius);
                for (int c = 0; c < 4; c++)
                {
                    parts[1 + c] = ("arm", d.Arm[c], d.ArmRadius);
                    for (int s = 0; s < Ring; s++) parts[5 + c * Ring + s] = ("disc", RingChord(d, c, s), d.DiscHalf);
                }
                string first = null;
                for (int k = 0; k < 2000 && first == null; k++)
                {
                    Pose prev = p;
                    p.P.Y -= 0.5 * Dt;
                    double best = 2;
                    foreach ((string name, Segment core, double radius) in parts)
                    {
                        if (floor.StaticContacts(prev.Of(core, radius), p.Of(core, radius), 0, Buf) > 0 && Buf[0].Time < best)
                        {
                            best = Buf[0].Time;
                            first = name;
                        }
                    }
                }
                Check(S, first == "coil", $"set down on the bare floor, the first part to touch is the {first} (it cannot stand on the spool)");
            }
        }

        // ---- Arm sweeps (legs on) --------------------------------------------------------------------------------------------

        /// <summary>
        /// Level flight into a 20 mm timber board and a 20 mm steel tube at the arm plane, at 10, 20 and 30 m/s, each at
        /// 16 phases of the step. Only the arms are queried (the discs and the coil clear both). Gravity is balanced by
        /// thrust. A tunnel is the drone still going forward with its arms past the far face.
        /// </summary>
        static void Sweeps(Drone d)
        {
            const string S = "arm sweeps, legs on";
            double margin = _break == Break.Margin ? 0.001 : Margin;
            bool latch = _break != Break.Latch;
            double lead = 0; // the arms' leading core end, body z
            for (int c = 0; c < 4; c++) lead = Math.Max(lead, Math.Max(d.Arm[c].A.Z, d.Arm[c].B.Z));
            var planes = new Planes[4];
            for (int c = 0; c < 4; c++) planes[c] = new Planes();
            double cArms = Damping(PartK, PartZeta, d.Mass / 2);
            foreach (bool board in new[] { true, false })
            {
                var w = World();
                const double obstacle = 1.0, half = 0.01, rise = 0.003;
                ShapeGeometry shape = board
                    ? Box(new Double3(0, 2, obstacle), new Double3(1, 0.02, half), w.MaterialId("timber"), X, Z)
                    : AlongX(new Double3(0, 2 + rise, obstacle), half, 1, w.MaterialId("steel"));
                int obj = w.AddObject(board ? "board" : "tube", new[] { shape });
                // The arm's leading core end touches when it is this far short of the obstacle's centre (board: its face).
                double reach = board ? half + d.ArmRadius : Math.Sqrt((half + d.ArmRadius) * (half + d.ArmRadius) - rise * rise);
                int tunnelled = 0, events = 0, runs = 0, badTouch = 0;
                double worstDepth = 0, worstTime = 0;
                foreach (double speed in new[] { 10.0, 20.0, 30.0 })
                {
                    int tunnelledHere = 0;
                    double deepestHere = 0;
                    for (int phase = 0; phase < 16; phase++)
                    {
                        double p = (phase + 0.5) / 16, step = speed * Dt;
                        var pose = new Pose(new Double3(0, 2, obstacle - reach - lead - (2 + p) * step), Quat.Identity);
                        var v = new Double3(0, 0, speed);
                        for (int c = 0; c < 4; c++) planes[c].Clear();
                        bool touched = false;
                        for (int k = 0; k < 400; k++)
                        {
                            Pose prev = pose;
                            double deepest = 0;
                            Double3 force = default;
                            for (int c = 0; c < 4; c++) force = force + Push(planes[c], pose.Of(d.Arm[c], d.ArmRadius), v, PartK, cArms, ref deepest);
                            deepestHere = Math.Max(deepestHere, deepest);
                            v = v + force * (Dt / d.Mass);
                            pose.P = pose.P + v * Dt;
                            for (int c = 0; c < 4; c++)
                            {
                                Capsule was = prev.Of(d.Arm[c], d.ArmRadius), now = pose.Of(d.Arm[c], d.ArmRadius);
                                int n = w.StaticContacts(was, now, margin, Buf);
                                if (!touched && n > 0 && Buf[0].Time < 1)
                                {
                                    touched = true;
                                    double expected = (obstacle - reach - (prev.P.Z + lead)) / (now.A.Z - was.A.Z);
                                    worstTime = Math.Max(worstTime, expected - Buf[0].Time);
                                    if (k != 2 || Buf[0].Time > expected + 1e-12 || expected - Buf[0].Time > 1e-6 / step + 1e-12 || !(Buf[0].Distance >= -1e-12)
                                        || Buf[0].Distance > 1e-6 || Buf[0].Object != obj || w.MaterialName(Buf[0].Material) != (board ? "timber" : "steel")
                                        || !(Buf[0].Normal.Z < 0))
                                        badTouch++;
                                }
                                planes[c].Update(w, new ReadOnlySpan<StaticContact>(Buf, 0, Math.Min(n, Buf.Length)), now, margin, latch);
                            }
                            if (v.Z < 0 && planes[0].Count + planes[1].Count + planes[2].Count + planes[3].Count == 0) break;
                        }
                        runs++;
                        for (int c = 0; c < 4; c++) events += planes[c].TunnelEvents;
                        if (v.Z > 0 && pose.P.Z + lead - d.ArmRadius > obstacle + half)
                        {
                            tunnelled++;
                            tunnelledHere++;
                        }
                    }
                    worstDepth = Math.Max(worstDepth, deepestHere);
                    Info(S, $"{(board ? "board" : "tube")} at {speed:F0} m/s: {tunnelledHere} of 16 phases through, the deepest plane penetration "
                        + $"{deepestHere * 1e3:F1} mm ({PartK * deepestHere * 2 / (d.Mass * Gravity):F0} g on the body)");
                }
                Check(S, badTouch == 0, $"{(board ? "20 mm timber board" : "20 mm steel tube")}: every first touch is one Time < 1 contact in the "
                    + $"right step with Distance under 1 µm, the obstacle's object, material and an entry normal; Time conservative within {worstTime:E1}");
                Check(S, tunnelled == 0 && events == 0, $"{(board ? "20 mm timber board" : "20 mm steel tube")}: {tunnelled} of {runs} runs "
                    + $"tunnelled, {events} tunnel events (latch {(latch ? "on" : "off")}, margin {margin * 1e3:F0} mm)");
            }
        }

        // ---- Prop discs and a wire ------------------------------------------------------------------------------------------

        /// <summary>
        /// The first strike of disc `prop` over a straight flight: each step the disc's bounding sphere (radius R plus the
        /// blade's half-thickness) is swept, and only when it meets something other than the ground is the rim ring swept
        /// (Ring capsules of the blade's half-thickness). Break.Disc takes the bounding sphere's contact as the strike.
        /// </summary>
        static int FirstStrike(IWorldQuery w, Drone d, int prop, Pose pose, Double3 velocity, int steps, out StaticContact hit, out int gated)
        {
            hit = default;
            gated = 0;
            for (int k = 0; k < steps; k++)
            {
                Pose prev = pose;
                pose.P = pose.P + velocity * Dt;
                Capsule wasBall = prev.Ball(d.Hub[prop], d.DiscRadius + d.DiscHalf), nowBall = pose.Ball(d.Hub[prop], d.DiscRadius + d.DiscHalf);
                int n = w.StaticContacts(wasBall, nowBall, 0, Buf);
                bool candidate = false;
                for (int i = 0; i < n; i++) candidate |= Buf[i].Object != WorldIds.Ground;
                if (!candidate) continue;
                gated++;
                if (_break == Break.Disc)
                {
                    hit = Buf[0];
                    return k;
                }
                double best = 2;
                for (int s = 0; s < Ring; s++)
                {
                    Segment chord = RingChord(d, prop, s);
                    int m = w.StaticContacts(prev.Of(chord, d.DiscHalf), pose.Of(chord, d.DiscHalf), 0, Buf);
                    for (int i = 0; i < m; i++)
                    {
                        if (Buf[i].Object == WorldIds.Ground || !(Buf[i].Time < 1 || Buf[i].Distance <= 0) || !(Buf[i].Time < best)) continue;
                        best = Buf[i].Time;
                        hit = Buf[i];
                    }
                }
                if (best < 2) return k;
            }
            return -1;
        }

        static Segment RingChord(Drone d, int prop, int s)
        {
            double a = 2 * Math.PI * s / Ring, b = 2 * Math.PI * (s + 1) / Ring;
            return new Segment(d.Hub[prop] + new Double3(d.DiscRadius * Math.Cos(a), 0, d.DiscRadius * Math.Sin(a)),
                d.Hub[prop] + new Double3(d.DiscRadius * Math.Cos(b), 0, d.DiscRadius * Math.Sin(b)));
        }

        static void Props(Drone d)
        {
            const string S = "prop discs, legs on";
            const int fr = 1;
            double reach = d.DiscRadius + d.DiscHalf + WireRadius;

            // Edge-on at 20 m/s: the wire across the path in the prop plane; 2.37 steps away at the start.
            {
                var w = World();
                double speed = 20, step = speed * Dt, gap = 2.37 * step, height = 2 + d.Hub[fr].Y;
                int wire = w.AddObject("wire", new[] { AlongX(new Double3(0, height, d.Hub[fr].Z + reach + gap), WireRadius, 5, w.MaterialId("cable")) });
                int k = FirstStrike(w, d, fr, new Pose(new Double3(0, 2, 0), Quat.Identity), new Double3(0, 0, speed), 20, out StaticContact hit, out int gated);
                bool geometry = w.Geometry(hit.Object, hit.Shape, out ShapeGeometry g);
                double expected = 0.37;
                Check(S, k == 2 && hit.Time <= expected + 1e-12 && expected - hit.Time <= 1e-6 / step + 1e-12 && hit.Object == wire && geometry
                    && g.Kind == ShapeKind.Capsule && g.Radius == WireRadius && w.MaterialName(hit.Material) == "cable" && hit.WireParam == -1
                    && Math.Abs(hit.Point.X - d.Hub[fr].X) < 1e-6 && Math.Abs(hit.Point.Y - height) < 1e-6,
                    $"edge-on at {speed:F0} m/s: struck in step {k + 1} at Time {hit.Time:F6} (analytic {expected:F6}) by the rim's leading point; "
                    + $"'{w.ObjectName(hit.Object)}', Geometry {g.Kind} of {2 * g.Radius * 1e3:F1} mm, {w.MaterialName(hit.Material)}, WireParam "
                    + $"{hit.WireParam} (the stand-in is a capsule; Wire is 1.4's)");
            }

            // Climbing at 5 m/s into a wire 0.2 m above the prop plane, crossing the disc 0.06 m off the hub (a chord).
            {
                var w = World();
                double speed = 5, step = speed * Dt, height = 2 + d.Hub[fr].Y + 0.2;
                w.AddObject("wire", new[] { AlongX(new Double3(0, height, d.Hub[fr].Z + 0.06), WireRadius, 5, w.MaterialId("cable")) });
                int k = FirstStrike(w, d, fr, new Pose(new Double3(0, 2, 0), Quat.Identity), new Double3(0, speed, 0), 100, out StaticContact hit, out int gated);
                double climb = 0.2 - d.DiscHalf - WireRadius, steps = climb / step;
                int expectedStep = (int)Math.Ceiling(steps) - 1;
                double expected = steps - expectedStep;
                Check(S, k == expectedStep && hit.Time <= expected + 1e-9 && expected - hit.Time <= 1e-6 / step + 1e-9 && hit.Normal.Y < -0.99,
                    $"climbing at {speed:F0} m/s into a wire over a chord 0.06 m off the hub: struck in step {k + 1} at Time {hit.Time:F6} (analytic "
                    + $"{expected:F6}), by the rim from below; the broadphase sphere opened the ring {gated} steps of the way");
            }

            // Level at 20 m/s under a wire 0.11 m above the prop plane: inside the disc's bounding sphere, clear of the disc.
            {
                var w = World();
                w.AddObject("wire", new[] { AlongX(new Double3(0, 2 + d.Hub[fr].Y + 0.11, d.Hub[fr].Z + 0.5), WireRadius, 5, w.MaterialId("cable")) });
                int k = FirstStrike(w, d, fr, new Pose(new Double3(0, 2, 0), Quat.Identity), new Double3(0, 0, 20), 60, out StaticContact hit, out int gated);
                Check(S, k < 0 && gated > 0, $"under a wire 0.11 m above the prop plane at 20 m/s: {(k < 0 ? "no strike" : $"a strike in step {k + 1}")}; "
                    + $"the bounding sphere met it on {gated} steps, so a sphere alone would call it a strike");
            }

            // The ring's limit: a compact shape (a 20 mm ball, a branch end) rising into the disc's face at the hub.
            {
                var w = World();
                w.AddObject("ball", new[] { new ShapeGeometry { Kind = ShapeKind.Sphere, Center = new Double3(d.Hub[fr].X, 2 + d.Hub[fr].Y - 0.05, d.Hub[fr].Z),
                    AxisX = X, AxisY = Y, AxisZ = Z, Radius = 0.02, Material = w.MaterialId("timber") } });
                int k = FirstStrike(w, d, fr, new Pose(new Double3(0, 2, 0), Quat.Identity), new Double3(0, -1, 0), 120, out StaticContact hit, out int gated);
                Info(S, $"a 20 mm ball rising through the disc's face at the hub: the ring {(k < 0 ? "misses it" : $"strikes it in step {k + 1}")}, the "
                    + $"bounding sphere sees it on {gated} steps (1.6 adds the blades)");
            }

            // A rotor ray down onto a wire, then cast again from inside the wire: the floor (the L01/L03 reading's 'rays skip wires').
            {
                var w = World();
                double hub = 2 + d.Hub[fr].Y;
                w.AddObject("wire", new[] { AlongX(new Double3(0, hub - 0.1, d.Hub[fr].Z), WireRadius, 5, w.MaterialId("cable")) });
                var o = new Double3(d.Hub[fr].X, hub, d.Hub[fr].Z);
                var hits = new RayHit[2];
                w.Raycast(new[] { new Ray(o, new Double3(0, -1, 0)) }, 10, new Span<RayHit>(hits, 0, 1));
                w.Raycast(new[] { new Ray(o - Y * (hits[0].Distance + WireRadius), new Double3(0, -1, 0)) }, 10, new Span<RayHit>(hits, 1, 1));
                Check(S, Math.Abs(hits[0].Distance - (0.1 - WireRadius)) < 1e-12 && hits[0].Object >= 0 && hits[1].Object == WorldIds.Ground
                    && Math.Abs(hits[1].Distance - (hub - 0.1)) < 1e-12, $"a rotor ray meets the wire at {hits[0].Distance:F4} m; cast again from the "
                    + $"wire's axis it starts inside and reaches the floor at {hits[1].Distance:F4} m");
            }
        }

        // ---- The bench -------------------------------------------------------------------------------------------------

        /// <summary>
        /// The probe drone's queries for one step, legs on: 12 ground samples (the 8 supports and the 4 hubs), 21 swept
        /// queries (4 skid capsules, 8 support spheres, 4 arms, the coil, 4 disc spheres) and 8 rotor rays, plus each disc's
        /// ring (12 capsules) when its sphere meets something. Clear: 1 m over the floor beside the stand, the board and a
        /// wire. Near: the front discs' spheres on the wire, their rings swept.
        /// </summary>
        public static bool Bench()
        {
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(FindRepoFile(DroneJson)));
            Drone d = Build(spec, true);
            var w = World();
            AddStand(w, spec, 2, 0);
            w.AddObject("board", new[] { Box(new Double3(-2, 1, 0), new Double3(1, 0.02, 0.01), w.MaterialId("timber"), X, Z) });
            double wireZ = d.Hub[1].Z + d.DiscRadius;
            w.AddObject("wire", new[] { AlongX(new Double3(0, 1 + d.Hub[1].Y, wireZ), WireRadius, 5, w.MaterialId("cable")) });
            var points = new XZ[12];
            var ground = new GroundSample[12];
            var rays = new Ray[8];
            var hits = new RayHit[8];
            bool ok = true;
            foreach (bool near in new[] { false, true })
            {
                const int warmup = 2000, steps = 60000;
                var times = new long[steps];
                int rings = 0;
                long allocated = 0;
                for (int k = -warmup; k < steps; k++)
                {
                    if (k == 0) allocated = GC.GetAllocatedBytesForCurrentThread();
                    double wobble = 1e-4 * (k & 15);
                    var prev = new Pose(new Double3(wobble, 1, near ? -0.002 : -1), Quat.Identity);
                    var pose = new Pose(prev.P + new Double3(0, 0, 0.002), Quat.Identity);
                    long t0 = Stopwatch.GetTimestamp();
                    rings += Step(w, d, prev, pose, points, ground, rays, hits);
                    if (k >= 0) times[k] = Stopwatch.GetTimestamp() - t0;
                }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Array.Sort(times);
                double us = 1e6 / Stopwatch.Frequency, p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us;
                bool pass = allocated == 0 && p50 < 60;
                ok &= pass;
                Console.WriteLine($"{Name}: {(near ? "near the wire" : "clear")}: {steps} steps of 12 samples, {21 + rings / (steps + warmup) * Ring} swept "
                    + $"queries, 8 rays: p50 {p50:F2} µs, p99 {p99:F2} µs, steady-state allocation {allocated} B: {(pass ? "PASS" : "FAIL")} (p50 < 60 µs, 0 B)");
            }
            return ok;
        }

        /// <summary>One step's probe queries; returns how many disc rings were swept.</summary>
        static int Step(IWorldQuery w, Drone d, in Pose prev, in Pose pose, XZ[] points, GroundSample[] ground, Ray[] rays, RayHit[] hits)
        {
            for (int i = 0; i < 8; i++)
            {
                Double3 x = pose.W(d.Support[i]);
                points[i] = new XZ(x.X, x.Z);
            }
            for (int c = 0; c < 4; c++)
            {
                Double3 h = pose.W(d.Hub[c]);
                points[8 + c] = new XZ(h.X, h.Z);
                rays[2 * c] = new Ray(h, new Double3(0, -1, 0));
                rays[2 * c + 1] = new Ray(h, Y);
            }
            w.SampleGround(points, ground);
            for (int c = 0; c < 4; c++)
            {
                w.StaticContacts(prev.Of(d.Skid[c], d.SkidRadius), pose.Of(d.Skid[c], d.SkidRadius), Margin, Buf);
                w.StaticContacts(prev.Ball(d.Support[2 * c], d.SkidRadius), pose.Ball(d.Support[2 * c], d.SkidRadius), Margin, Buf);
                w.StaticContacts(prev.Ball(d.Support[2 * c + 1], d.SkidRadius), pose.Ball(d.Support[2 * c + 1], d.SkidRadius), Margin, Buf);
                w.StaticContacts(prev.Of(d.Arm[c], d.ArmRadius), pose.Of(d.Arm[c], d.ArmRadius), Margin, Buf);
            }
            w.StaticContacts(prev.Of(d.Coil, d.CoilRadius), pose.Of(d.Coil, d.CoilRadius), Margin, Buf);
            int rings = 0;
            for (int c = 0; c < 4; c++)
            {
                int n = w.StaticContacts(prev.Ball(d.Hub[c], d.DiscRadius + d.DiscHalf), pose.Ball(d.Hub[c], d.DiscRadius + d.DiscHalf), 0, Buf);
                bool candidate = false;
                for (int i = 0; i < n; i++) candidate |= Buf[i].Object != WorldIds.Ground;
                if (!candidate) continue;
                rings++;
                for (int s = 0; s < Ring; s++)
                {
                    Segment chord = RingChord(d, c, s);
                    w.StaticContacts(prev.Of(chord, d.DiscHalf), pose.Of(chord, d.DiscHalf), 0, Buf);
                }
            }
            w.Raycast(rays, 2, hits);
            return rings;
        }

        // ---- Plumbing --------------------------------------------------------------------------------------------------

        static void Check(string scenario, bool ok, string detail)
        {
            if (!ok) _failed++;
            Console.WriteLine($"{Name}: {scenario}: {detail}: {(ok ? "PASS" : "FAIL")}");
        }

        static void Info(string scenario, string detail) => Console.WriteLine($"{Name}: {scenario}: {detail}");

        // The file, from the working directory or the tool's own folder upwards.
        static string FindRepoFile(string relative)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (string dir = start; dir != null; dir = Path.GetDirectoryName(dir))
                    if (File.Exists(Path.Combine(dir, relative)))
                        return Path.Combine(dir, relative);
            throw new FileNotFoundException(relative + " was not found above the working directory");
        }
    }
}
