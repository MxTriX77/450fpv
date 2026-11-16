using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// QA of the w9 step budget's second pass (47d62c5b): Sense skips the world's shapes in every shape's sweep when one
    /// NoGround query of a sphere about the origin, holding every non-disc shape at both poses, its margin grown by the step's
    /// move, finds nothing (ContactModel.Clear). Its claim: whenever that query is clear, each shape's own NoGround sweep finds
    /// nothing, so its unfiltered sweep is its ground's alone. Made states try to break it, at a random attitude, still, flying,
    /// spinning fast, on a fast line or turning a quarter turn in one step, over the test range (its wires and poles) and the
    /// farmland (the ruins' boards, the trees, the terrain), among thin runtime shapes (a 1 mm line, a 3 mm twig, a 2 cm board,
    /// a 2 mm bead), with parts far from the origin (the resting blades, a hanging skid half, a loose piece's shapes 1.5 m out):
    /// the drone's offset along a line toward a world point is bisected to where its shapes' sweeps first meet a world shape,
    /// and to where the gate's sphere first does; the states on both sides of each edge are sensed and checked, the clear ones
    /// against every world shape by a distance of the test's own (no grid, no bounds). A shape that appears between two steps is
    /// met at the next. Two known-wrong gates on the same states are each caught: the sphere without the shapes' radius, and a
    /// still sphere at the current pose without the step's move.
    /// </summary>
    public sealed class SenseGateTests
    {
        static void Print(string s) => TestContext.Progress.WriteLine(s);

        // The world as the model sees it, every call passed on; the gate's query (a sphere at both poses, the ground left out) noted.
        sealed class GateWatch : IWorldQuery
        {
            readonly IWorldQuery _w;
            public int LastGate = -1;

            public GateWatch(IWorldQuery w) => _w = w;

            public ulong ContentHash => _w.ContentHash;
            public int QueryVersion => _w.QueryVersion;
            public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results) => _w.SampleGround(points, results);

            public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results, QueryFilter filter = QueryFilter.None)
            {
                int n = _w.StaticContacts(previous, current, margin, results, filter);
                if (filter == QueryFilter.NoGround && Same(previous.A, previous.B) && Same(current.A, current.B)) LastGate = n;
                return n;
            }

            public bool ShapeContact(int obj, int shape, in Capsule previous, in Capsule current, double margin, out StaticContact contact) =>
                _w.ShapeContact(obj, shape, previous, current, margin, out contact);
            public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits, QueryFilter filter = QueryFilter.None) =>
                _w.Raycast(rays, maxDistance, hits, filter);
            public int WiresNear(Double3 centre, double radius, Span<Conductor> results) => _w.WiresNear(centre, radius, results);
            public int SnagsNear(Double3 centre, double radius, Span<SnagSegment> results) => _w.SnagsNear(centre, radius, results);
            public bool Geometry(int obj, int shape, out ShapeGeometry geometry) => _w.Geometry(obj, shape, out geometry);
            public int MicroDetailNear(Double3 centre, double radius, MicroKind kinds, Span<MicroElement> results) =>
                _w.MicroDetailNear(centre, radius, kinds, results);
            public int StemsNear(XZ centre, double radius, double height, Span<StemPatch> results) => _w.StemsNear(centre, radius, height, results);
            public bool Feature(int obj, int shape, out GroundFeature feature) => _w.Feature(obj, shape, out feature);
            public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes) => _w.AddObject(name, shapes);
            public void ResetRuntimeObjects() => _w.ResetRuntimeObjects();
            public string ObjectName(int obj) => _w.ObjectName(obj);
            public string MaterialName(ushort material) => _w.MaterialName(material);
            public ushort MaterialId(string name) => _w.MaterialId(name);
            public string SurfaceName(byte surface) => _w.SurfaceName(surface);
        }

        static bool Same(Double3 a, Double3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        static bool SameBits(in StaticContact a, in StaticContact b) =>
            Bits(a.Point, b.Point) && Bits(a.Normal, b.Normal) && Bits(a.Distance, b.Distance) && a.Object == b.Object && a.Shape == b.Shape
            && a.Material == b.Material && a.Surface == b.Surface && Bits(a.WireParam, b.WireParam) && Bits(a.Time, b.Time);

        static bool Bits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        static bool Bits(Double3 a, Double3 b) => Bits(a.X, b.X) && Bits(a.Y, b.Y) && Bits(a.Z, b.Z);

        static Double3 World(in FlightState s, Double3 body) => s.Position + s.Attitude.Rotate(body);

        // A map with what the trials aim at, and every shape of it as the test's own distance reads it.
        sealed class Scene
        {
            public MapWorld Map;
            public readonly List<Double3> Aims = new List<Double3>();
            public readonly List<ShapeGeometry> Solids = new List<ShapeGeometry>();
            public readonly List<(Double3 A, Double3 B, double R)> Wires = new List<(Double3, Double3, double)>();
        }

        // One model in a scene.
        sealed class Rig
        {
            public string Name;
            public Scene Scene;
            public GateWatch Watch;
            public ContactModel Model;
            public FlightCore Core;
            public readonly List<int> On = new List<int>();
            public int Farthest, Blades;
        }

        sealed class Tally
        {
            public int States, Found, Swept, Clear, Gated, Violations, Mismatches, Replica, Brute, BruteMisses, Appeared, AppearedClear,
                Turned, TurnedGated, WrongRadius, WrongStill;
            public readonly List<string> First = new List<string>();

            public void Note(string s)
            {
                if (First.Count < 8) First.Add(s);
            }
        }

        const int Trials = 500;
        const double Dt = 0.001, Slack = 1e-3; // the gate's ClearSlack

        [Test]
        public void TheGateNeverSkipsAContactASweepFinds()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            var range = new Scene { Map = MapWorld.Load(RangeScenarios.Root, "test-range") };
            var farm = new Scene { Map = MapWorld.Load(RangeScenarios.Root, "farmland") };
            var rigs = new List<Rig>();
            try
            {
                rigs.Add(Flying("test range", range, spec, materials, hash));
                rigs.Add(Flying("farmland", farm, spec, materials, hash));
                Rig hanging = Flying("farmland, a hanging half", farm, spec, materials, hash);
                Hang(hanging);
                rigs.Add(hanging);
                rigs.Add(Piece("farmland, a loose piece 1.5 m out", farm, spec, materials, hash));
                Fill(range, 11);
                Fill(farm, 12);
                foreach (Rig g in rigs)
                {
                    for (int i = 0; i < g.Model.ShapeCount; i++)
                    {
                        if (g.Model.ShapeOff(i) || g.Model.Shape(i).Role == ContactRole.Disc) continue;
                        g.On.Add(i);
                        if (g.Model.Shape(i).Role == ContactRole.Blade) g.Blades++;
                    }
                    Reach(g, out double far, out double thick);
                    double best = -1;
                    foreach (int i in g.On)
                    {
                        ContactShape c = g.Model.Shape(i);
                        double end = Math.Max(Double3.Dot(c.A, c.A), Double3.Dot(c.B, c.B));
                        if (end > best)
                        {
                            best = end;
                            g.Farthest = i;
                        }
                    }
                    Print($"{g.Name}: {g.On.Count} shapes in the gate ({g.Blades} resting blades'), farthest end {Math.Sqrt(far):F4} m ({g.Model.Shape(g.Farthest).Name}), " +
                        $"thickest {thick:F4} m, margin {g.Model.Spec.Margin} m, terrain {g.Model.Terrain}");
                }
                Print($"aims: test range {range.Aims.Count}, farmland {farm.Aims.Count}; shapes by the test's own distance: test range {range.Solids.Count} " +
                    $"and {range.Wires.Count} wire segments, farmland {farm.Solids.Count} and {farm.Wires.Count}");
                var r = new Random(0x5E45E);
                var t = new Tally();
                var buf = new StaticContact[ContactModel.PairsPerShape + 8];
                var other = new StaticContact[buf.Length];
                for (int trial = 0; trial < Trials * rigs.Count; trial++)
                    Trial(rigs[trial % rigs.Count], r, t, buf, other);
                Print($"{t.States} made states over {rigs.Count} rigs: {t.Found} with a shape's sweep meeting a world shape ({t.Swept} by a first " +
                    $"touch within the step), {t.Clear} clear ({t.Brute} of them at the gate's edge, checked against every world shape: {t.BruteMisses} " +
                    $"within the gate's margin), {t.Gated} not clear by a moved set; violations {t.Violations}, GroundOnly mismatches {t.Mismatches}, " +
                    $"replica off {t.Replica}; appeared {t.Appeared} (clear {t.AppearedClear}); a rotor turned {t.Turned} (gate aside {t.TurnedGated}); known-wrong caught: no radius {t.WrongRadius}, " +
                    $"still sphere {t.WrongStill}");
                foreach (string s in t.First) Print(s);
                Assert.That(t.Violations, Is.Zero, "the gate skipped a contact a shape's own sweep finds");
                Assert.That(t.Mismatches, Is.Zero, "clear, a shape's unfiltered sweep is its GroundOnly sweep, bit for bit");
                Assert.That(t.BruteMisses, Is.Zero, "clear, no world shape lies within the gate's margin of its sphere");
                Assert.That(t.AppearedClear, Is.Zero, "a shape that appears between two steps is met at the next");
                Assert.That(t.Replica, Is.Zero, "the test's copy of the gate's query is the model's");
                Assert.That(t.TurnedGated, Is.EqualTo(t.Turned), "a moved set: the gate stands aside");
                Assert.That(t.Turned, Is.GreaterThan(Trials), "the blades turn at rest");
                Assert.That(t.Found, Is.GreaterThan(Trials), "the made states meet the world at the shapes' margins");
                Assert.That(t.Brute, Is.GreaterThan(Trials), "and at the gate's, clear");
                Assert.That(t.Swept, Is.GreaterThan(Trials / 100), "some meet it by a first touch within the step, not at its end");
                Assert.That(t.WrongRadius, Is.GreaterThan(0), "known-wrong: a sphere without the shapes' radius is caught");
                Assert.That(t.WrongStill, Is.GreaterThan(0), "known-wrong: a still sphere without the step's move is caught");
            }
            finally
            {
                foreach (Rig g in rigs) g.Core?.Wind?.Dispose();
                range.Map.ResetRuntimeObjects();
                farm.Map.ResetRuntimeObjects();
            }
        }

        // The drone flying a map: its own contacts with the rotors attached (the blades resting, so their shapes are on).
        static Rig Flying(string name, Scene scene, DroneSpec spec, MaterialRow[] materials, ulong hash)
        {
            var watch = new GateWatch(scene.Map);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, true, new Double3(0, 30, 0));
            setup.World = new FlightWorld(watch, scene.Map);
            var core = new FlightCore(setup);
            return new Rig { Name = name, Scene = scene, Watch = watch, Model = core.Contacts, Core = core };
        }

        // Leg 3 off: its skid half hangs from leg 2's T-joint (HangingHalfTests' build).
        static void Hang(Rig g)
        {
            FlightState s = g.Core.State;
            var events = new StepEvents(256);
            g.Model.Legs.Remove(3);
            g.Model.Restructure(s, events);
            Assert.That((g.Model.Corner(3) & CornerState.Hanging) != 0, "leg_fl's half hangs");
            g.Model.Sense(s, ref s, events);
        }

        // A loose piece's contacts (LoosePieces' Reuse): the four legs' shapes, 1.5 m out along the body's x.
        static Rig Piece(string name, Scene scene, DroneSpec spec, MaterialRow[] materials, ulong hash)
        {
            var watch = new GateWatch(scene.Map);
            Loadout loadout = spec.Nominal;
            ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
            var model = new ContactModel(contacts, new FlightWorld(watch, scene.Map), spec.BuildMass(loadout).Properties.Mass, random: new FlightRandom(0x450F));
            var shapes = new List<int>();
            var geometry = new List<ContactShape>();
            for (int i = 0; i < contacts.Shapes.Length; i++)
            {
                ContactShape c = contacts.Shapes[i];
                if (c.Role != ContactRole.Support && c.Role != ContactRole.Skid) continue;
                c.A = c.A + new Double3(1.5, 0, 0);
                c.B = c.B + new Double3(1.5, 0, 0);
                shapes.Add(i);
                geometry.Add(c);
            }
            model.Reuse(0.05, shapes.ToArray(), shapes.Count, geometry.ToArray());
            return new Rig { Name = name, Scene = scene, Watch = watch, Model = model };
        }

        // Thin runtime objects scattered over the map's middle (a 1 mm line, a 3 mm twig, a 2 cm board, a 2 mm bead), then the
        // points to aim at (in or on every placed and runtime shape and wire; the thin ones twice) and every shape for the test's
        // own distance.
        static void Fill(Scene scene, int seed)
        {
            MapWorld map = scene.Map;
            var r = new Random(seed);
            ushort timber = map.MaterialId("timber");
            Assert.That(timber, Is.Not.EqualTo(WorldIds.NoMaterial), "the map knows timber");
            var ground = new GroundSample[1];
            for (int k = 0; k < 12; k++)
            {
                double x = (r.NextDouble() - 0.5) * 60, z = (r.NextDouble() - 0.5) * 60;
                map.SampleGround(new[] { new XZ(x, z) }, ground);
                double y = double.IsInfinity(ground[0].Height) ? 0 : ground[0].Height;
                var c = new Double3(x, y + 0.2 + 3 * r.NextDouble(), z);
                Quat q = Attitude(r);
                Double3 ax = q.Rotate(new Double3(1, 0, 0)), ay = q.Rotate(new Double3(0, 1, 0)), az = Double3.Cross(ax, ay);
                map.AddObject("qa thin " + k, new[]
                {
                    new ShapeGeometry { Kind = ShapeKind.Capsule, Center = c, AxisX = ax, AxisY = ay, AxisZ = az, Radius = 0.0005, HalfLength = 3, Material = timber },
                    new ShapeGeometry { Kind = ShapeKind.Cylinder, Center = c + ax * 0.4, AxisX = az, AxisY = ax, AxisZ = ay, Radius = 0.0015, HalfLength = 0.3, Material = timber },
                    new ShapeGeometry { Kind = ShapeKind.Box, Center = c + az * 0.7, AxisX = ax, AxisY = ay, AxisZ = az, HalfExtents = new Double3(0.01, 0.5, 0.4), Material = timber },
                    new ShapeGeometry { Kind = ShapeKind.Sphere, Center = c - ax * 0.5, AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1),
                        Radius = 0.001, Material = timber },
                });
            }
            for (int obj = 0; map.ObjectName(obj) != null; obj++)
                for (int shape = 0; map.Geometry(obj, shape, out ShapeGeometry g); shape++)
                {
                    if (g.Kind == ShapeKind.Wire)
                    {
                        ReadOnlySpan<Double3> chain = map.WirePoints(obj, shape);
                        for (int k = 0; k + 1 < chain.Length; k++)
                        {
                            scene.Wires.Add((chain[k], chain[k + 1], g.Radius));
                            if (k % 4 == 0) scene.Aims.Add(chain[k] + (chain[k + 1] - chain[k]) * r.NextDouble());
                        }
                        continue;
                    }
                    scene.Solids.Add(g);
                    double u = 2 * r.NextDouble() - 1, v = 2 * r.NextDouble() - 1, w = 2 * r.NextDouble() - 1;
                    Double3 p = g.Kind == ShapeKind.Box ? g.AxisX * (u * g.HalfExtents.X) + g.AxisY * (v * g.HalfExtents.Y) + g.AxisZ * (w * g.HalfExtents.Z)
                        : g.Kind == ShapeKind.Sphere ? default : g.AxisY * (v * g.HalfLength);
                    scene.Aims.Add(g.Center + p);
                    bool thin = g.Kind == ShapeKind.Box ? Math.Min(g.HalfExtents.X, Math.Min(g.HalfExtents.Y, g.HalfExtents.Z)) < 0.015 : g.Radius < 0.015;
                    if (thin) scene.Aims.Add(g.Center + p);
                }
        }

        static Quat Attitude(Random r)
        {
            double x = Gauss(r), y = Gauss(r), z = Gauss(r), w = Gauss(r), n = Math.Sqrt(x * x + y * y + z * z + w * w);
            return new Quat(x / n, y / n, z / n, w / n);
        }

        static double Gauss(Random r) => Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble());

        static Double3 Unit(Random r)
        {
            var v = new Double3(Gauss(r), Gauss(r), Gauss(r));
            return v * (1 / v.Length());
        }

        static Quat Turn(Double3 axis, double angle) =>
            new Quat(axis.X * Math.Sin(angle / 2), axis.Y * Math.Sin(angle / 2), axis.Z * Math.Sin(angle / 2), Math.Cos(angle / 2));

        // One trial: a random shape's point driven toward a world point along a random line, at a random attitude and motion.
        // The offset is bisected to where the shapes' sweeps first meet a world shape (the states on both sides and just past
        // checked) and to where the gate's sphere first does (both sides checked); then a 2 mm bead appears on the shape's point.
        static void Trial(Rig g, Random r, Tally t, StaticContact[] buf, StaticContact[] other)
        {
            int pick = r.Next(4) == 0 ? g.Farthest : g.On[r.Next(g.On.Count)]; // the shape with the farthest end a quarter of the time
            ContactShape sh = g.Model.Shape(pick);
            Double3 b = sh.A + (sh.B - sh.A) * r.NextDouble();
            Double3 aim = g.Scene.Aims[r.Next(g.Scene.Aims.Count)];

            Quat q = Attitude(r);
            int mode = r.Next(5); // still, flying, spinning fast, a fast line (up to 0.6 m a step), a quarter turn in a step
            double spin = mode == 0 ? 0 : mode == 1 ? 50 * r.NextDouble() : mode == 2 ? 600 * r.NextDouble() : mode == 3 ? 100 * r.NextDouble() : 0;
            double speed = mode == 0 ? 0 : mode == 1 ? 30 * r.NextDouble() : mode == 2 ? 100 * r.NextDouble() : mode == 3 ? 600 * r.NextDouble() : 0;
            Quat qWas = mode == 4 ? Turn(Unit(r), Math.PI / 2 * r.NextDouble()) * q : spin > 0 ? Quat.Integrate(q, Unit(r) * spin, -Dt) : q;
            if (r.Next(8) == 0) qWas = new Quat(-qWas.X, -qWas.Y, -qWas.Z, -qWas.W); // the same turn, the other sign
            Double3 step = Unit(r) * (speed * Dt), line = Unit(r), at = aim - q.Rotate(b);
            if (pick == g.Farthest && r.Next(2) == 0) line = q.Rotate(b) * (1 / b.Length()); // the farthest end leading, radially

            FlightState now = g.Core != null ? g.Core.State : default;
            now.Attitude = q;
            FlightState was = now;
            was.Attitude = qWas;
            bool Shapes(double s)
            {
                Place(ref was, ref now, at, line, s, step);
                return Meets(g, g.On, was, now, buf);
            }
            bool Sphere(double s)
            {
                Place(ref was, ref now, at, line, s, step);
                return Gate(g, was, now, buf) > 0;
            }
            if (Edge(Shapes, out double lo, out double hi))
            {
                Place(ref was, ref now, at, line, lo, step);
                Check(g, was, now, t, buf, other, true, false);
                Place(ref was, ref now, at, line, hi, step);
                Check(g, was, now, t, buf, other, false, false);
                Place(ref was, ref now, at, line, hi + 2e-3 * r.NextDouble(), step);
                Check(g, was, now, t, buf, other, false, false);
            }
            if (Edge(Sphere, out lo, out hi))
            {
                Place(ref was, ref now, at, line, lo, step);
                Check(g, was, now, t, buf, other, false, false);
                Place(ref was, ref now, at, line, hi, step);
                Check(g, was, now, t, buf, other, false, true);
            }

            // A bead appears on the shape's point between two steps: the next Sense meets it.
            MapWorld map = g.Scene.Map;
            map.AddObject("qa bead", new[]
            {
                new ShapeGeometry { Kind = ShapeKind.Sphere, Center = World(now, b), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
                    AxisZ = new Double3(0, 0, 1), Radius = 0.001, Material = map.MaterialId("timber") },
            });
            Assert.That(Meets(g, new List<int> { pick }, was, now, buf), "the bead lies on the shape's path");
            g.Watch.LastGate = -1;
            FlightState n = now;
            g.Model.Sense(was, ref n, new StepEvents(1024));
            t.Appeared++;
            if (g.Watch.LastGate == 0) t.AppearedClear++;
            RemoveLast(map);

            // A resting rotor turned since the last Sense moves its blades' shapes (a set moved, as a leg's): the gate stands
            // aside before its query; and again as they turn back.
            if (g.Core == null || g.Blades == 0) return;
            FlightState turned = now;
            turned.RotorAngle[r.Next(4)] += 0.3 + r.NextDouble();
            for (int pass = 0; pass < 2; pass++)
            {
                g.Watch.LastGate = -1;
                FlightState s = pass == 0 ? turned : now;
                g.Model.Sense(was, ref s, new StepEvents(1024));
                t.Turned++;
                if (g.Watch.LastGate < 0) t.TurnedGated++;
            }
        }

        // The edge of `meets` along the line: lo meets, hi (= lo plus a few 1e-14 m) does not; false where the aim's own point
        // does not meet or 40 m out still does.
        static bool Edge(Func<double, bool> meets, out double lo, out double hi)
        {
            lo = 0;
            hi = 2.5;
            if (!meets(lo)) return false;
            while (meets(hi))
            {
                lo = hi;
                hi *= 2;
                if (hi > 40) return false;
            }
            for (int k = 0; k < 48; k++)
            {
                double mid = lo + (hi - lo) * 0.5;
                if (!(mid > lo && mid < hi)) break;
                if (meets(mid)) lo = mid;
                else hi = mid;
            }
            return true;
        }

        // Every runtime object but the last (the bead), as they were.
        static void RemoveLast(MapWorld map)
        {
            var kept = new List<(string, ShapeGeometry[])>();
            for (int obj = map.PlacedObjects; ; obj++)
            {
                string name = map.ObjectName(obj);
                if (name == null) break;
                var shapes = new List<ShapeGeometry>();
                for (int s = 0; map.Geometry(obj, s, out ShapeGeometry geo); s++) shapes.Add(geo);
                kept.Add((name, shapes.ToArray()));
            }
            map.ResetRuntimeObjects();
            for (int k = 0; k + 1 < kept.Count; k++) map.AddObject(kept[k].Item1, kept[k].Item2);
        }

        static void Place(ref FlightState was, ref FlightState now, Double3 at, Double3 line, double s, Double3 step)
        {
            now.Position = at - line * s;
            was.Position = now.Position - step;
        }

        // Whether any of the shapes' own sweeps meets a world shape (the ground left out).
        static bool Meets(Rig g, List<int> shapes, in FlightState was, in FlightState now, StaticContact[] buf)
        {
            foreach (int i in shapes)
            {
                ContactShape sh = g.Model.Shape(i);
                if (g.Scene.Map.StaticContacts(new Capsule(World(was, sh.A), World(was, sh.B), sh.Radius), new Capsule(World(now, sh.A), World(now, sh.B), sh.Radius),
                        g.Model.Spec.Margin, buf, QueryFilter.NoGround) > 0) return true;
            }
            return false;
        }

        static void Reach(Rig g, out double far, out double thick)
        {
            far = thick = 0;
            foreach (int i in g.On)
            {
                ContactShape c = g.Model.Shape(i);
                far = DetMath.Max(far, DetMath.Max(Double3.Dot(c.A, c.A), Double3.Dot(c.B, c.B)));
                thick = DetMath.Max(thick, c.Radius);
            }
        }

        // The gate's sphere (ContactModel.Clear's arithmetic, op for op): its radius, its margin, and its query's count.
        static int Gate(Rig g, in FlightState was, in FlightState now, StaticContact[] buf) => Gate(g, was, now, buf, out _, out _);

        static int Gate(Rig g, in FlightState was, in FlightState now, StaticContact[] buf, out double r, out double margin)
        {
            Reach(g, out double far, out double thick);
            double reach = DetMath.Sqrt(far) + thick;
            Quat a = was.Attitude, b = now.Attitude;
            double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z, dw = b.W - a.W;
            double move = (now.Position - was.Position).Length() + 4 * DetMath.Sqrt(dx * dx + dy * dy + dz * dz + dw * dw) * reach;
            r = reach + Slack;
            margin = g.Model.Spec.Margin + move + Slack;
            return g.Scene.Map.StaticContacts(new Capsule(was.Position, was.Position, r), new Capsule(now.Position, now.Position, r), margin, buf,
                QueryFilter.NoGround);
        }

        static void Check(Rig g, in FlightState was, in FlightState now, Tally t, StaticContact[] buf, StaticContact[] other, bool inside, bool edge)
        {
            ContactModel m = g.Model;
            MapWorld map = g.Scene.Map;
            double margin = m.Spec.Margin;
            g.Watch.LastGate = -1;
            FlightState n = now;
            m.Sense(was, ref n, new StepEvents(1024));
            Assert.That(Same(n.Position, now.Position) && Bits(n.Attitude.X, now.Attitude.X) && Bits(n.Attitude.W, now.Attitude.W), "Sense moves nothing");
            t.States++;
            int gate = Gate(g, was, now, other, out double r, out double gateMargin);
            if (g.Watch.LastGate < 0) t.Gated++;
            else if (g.Watch.LastGate != gate) t.Replica++;
            bool clear = g.Watch.LastGate == 0;
            if (clear) t.Clear++;
            bool found = false, swept = false;
            foreach (int i in g.On)
            {
                ContactShape sh = m.Shape(i);
                var before = new Capsule(World(was, sh.A), World(was, sh.B), sh.Radius);
                var after = new Capsule(World(now, sh.A), World(now, sh.B), sh.Radius);
                int k = map.StaticContacts(before, after, margin, buf, QueryFilter.NoGround);
                if (k > 0)
                {
                    found = true;
                    for (int j = 0; j < k && j < buf.Length; j++) swept |= buf[j].Time < 1;
                    if (clear)
                    {
                        t.Violations++;
                        t.Note($"VIOLATION {g.Name}: {sh.Name} meets object {buf[0].Object} shape {buf[0].Shape} at {buf[0].Distance} m, time {buf[0].Time}; " +
                            $"was {was.Position} {was.Attitude}, now {now.Position} {now.Attitude}");
                    }
                }
                if (!clear || m.Terrain && sh.Role == ContactRole.Skid) continue;
                int all = map.StaticContacts(before, after, margin, buf, QueryFilter.None);
                int ground = map.StaticContacts(before, after, margin, other, QueryFilter.GroundOnly);
                bool same = all == ground;
                for (int j = 0; same && j < all && j < buf.Length; j++) same = SameBits(buf[j], other[j]);
                if (!same) t.Mismatches++;
            }
            if (found) t.Found++;
            if (swept) t.Swept++;
            Assert.That(!inside || found, "the bisection's inside state meets the world");
            if (clear && edge)
            {
                // At the gate's edge: no world shape within its margin of its sphere now, by the test's own distance.
                t.Brute++;
                double nearest = Nearest(g.Scene, now.Position) - r;
                if (!(nearest > gateMargin - 1e-9))
                {
                    t.BruteMisses++;
                    t.Note($"BRUTE {g.Name}: a world shape {nearest} m from the clear gate's sphere, its margin {gateMargin} m; now {now.Position}");
                }
            }
            if (!found) return;
            // The known-wrong gates on the same state: either one clear here would have skipped a contact.
            Reach(g, out double far, out double thick);
            double bare = Math.Sqrt(far) + Slack, wide = Math.Sqrt(far) + thick + Slack;
            if (map.StaticContacts(new Capsule(was.Position, was.Position, bare), new Capsule(now.Position, now.Position, bare), gateMargin, buf,
                    QueryFilter.NoGround) == 0) t.WrongRadius++;
            if (map.StaticContacts(new Capsule(now.Position, now.Position, wide), new Capsule(now.Position, now.Position, wide), margin + Slack, buf,
                    QueryFilter.NoGround) == 0) t.WrongStill++;
        }

        // The least distance from p to any world shape (placed, runtime, wire), the ground left out: plain geometry, every shape.
        static double Nearest(Scene scene, Double3 p)
        {
            double best = double.PositiveInfinity;
            foreach (ShapeGeometry s in scene.Solids) best = Math.Min(best, Distance(s, p));
            foreach ((Double3 A, Double3 B, double R) w in scene.Wires) best = Math.Min(best, (p - OnSegment(w.A, w.B, p)).Length() - w.R);
            return best;
        }

        static Double3 OnSegment(Double3 a, Double3 b, Double3 p)
        {
            Double3 d = b - a;
            double dd = Double3.Dot(d, d), u = dd > 0 ? Double3.Dot(p - a, d) / dd : 0;
            return a + d * Math.Max(0, Math.Min(1, u));
        }

        // A point's signed distance to a sphere, capsule, box or cylinder.
        static double Distance(in ShapeGeometry s, Double3 p)
        {
            Double3 v = p - s.Center;
            double x = Double3.Dot(v, s.AxisX), y = Double3.Dot(v, s.AxisY), z = Double3.Dot(v, s.AxisZ);
            switch (s.Kind)
            {
                case ShapeKind.Sphere:
                    return v.Length() - s.Radius;
                case ShapeKind.Capsule:
                {
                    double along = Math.Max(-s.HalfLength, Math.Min(s.HalfLength, y));
                    return (v - s.AxisY * along).Length() - s.Radius;
                }
                case ShapeKind.Box:
                {
                    double qx = Math.Abs(x) - s.HalfExtents.X, qy = Math.Abs(y) - s.HalfExtents.Y, qz = Math.Abs(z) - s.HalfExtents.Z;
                    double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0), oz = Math.Max(qz, 0);
                    return Math.Sqrt(ox * ox + oy * oy + oz * oz) + Math.Min(Math.Max(qx, Math.Max(qy, qz)), 0);
                }
                case ShapeKind.Cylinder:
                {
                    double dr = Math.Sqrt(x * x + z * z) - s.Radius, dy = Math.Abs(y) - s.HalfLength;
                    double or = Math.Max(dr, 0), oy = Math.Max(dy, 0);
                    return Math.Sqrt(or * or + oy * oy) + Math.Min(Math.Max(dr, dy), 0);
                }
                default:
                    Assert.Fail($"a world shape of kind {s.Kind}");
                    return 0;
            }
        }
    }
}
