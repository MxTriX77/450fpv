using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Fpv450.Sim;
using Fpv450.Sim.World;

namespace Fpv450.WorldBench
{
    // The test range's cases (p6-world-query-gallery tasks 1.4 and 1.5, D-047): maps/test-range/placements.json loaded as
    // MapWorld, with a fixed four-post stand at the "field" start as its runtime object.
    // - Golden: 10 000 each of ground samples, swept capsules and rays, spread over the map's busiest spots (canonical
    //   order: input i lies in spot i mod the spot count), batched against one by one where a call takes a batch.
    // - Bench: a physics step's batch at the densest spots (a crown, beside H01_D, over E01's run, the posadka at 2 m,
    //   resting in S04's straw, lifting off the stand), in physics' pattern (review-physics W7) and K§10.5's, at physics'
    //   margin; then 100 000 capsules (moving at most 30 mm, physics' 30 m/s at 1 kHz), rays and samples over the golden's
    //   spots. Judged as D-054 sets it: physics' pattern inside PLT-04's 300 µs step at p99 with 0 B, the 60 µs target
    //   printed beside it; K§10.5's batch with its fiber shown, judged at phase 7d.
    internal static partial class Program
    {
        /// <summary>Physics' StaticContacts margin (review-physics P2, drone.json's contacts.margin_m is 0.1 on the drone).</summary>
        private const double PhysicsMargin = 0.05;

        /// <summary>The repository root: the first folder above the build output that holds the test range.</summary>
        private static string RepositoryRoot()
        {
            for (DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "maps", "test-range", "placements.json")))
                    return dir.FullName;
            throw new DirectoryNotFoundException("maps/test-range/placements.json not found above " + AppContext.BaseDirectory);
        }

        /// <summary>The test range with a four-post steel stand (20 mm posts, 0.25 m tall, on 0.2 m diagonals) at its "field" start.</summary>
        private static MapWorld TestRange()
        {
            MapWorld world = MapWorld.Load(RepositoryRoot(), "test-range");
            Double3 foot = StandFoot(world);
            ushort steel = world.MaterialId("steel");
            var shapes = new ShapeGeometry[4];
            for (int c = 0; c < 4; c++)
            {
                double x = (c & 1) == 0 ? 0.1414 : -0.1414, z = (c & 2) == 0 ? 0.1414 : -0.1414;
                shapes[c] = new ShapeGeometry
                {
                    Kind = ShapeKind.Box, Center = foot + new Double3(x, 0.125, z), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
                    AxisZ = new Double3(0, 0, 1), HalfExtents = new Double3(0.01, 0.125, 0.01), Material = steel,
                };
            }
            world.AddObject("stand", shapes);
            return world;
        }

        private static Double3 StandFoot(MapWorld world)
        {
            foreach (StartPoint s in world.StartPoints)
                if (s.Stand)
                    return s.Position;
            throw new InvalidOperationException("the test range has no stand start point");
        }

        // ---- Spots ---------------------------------------------------------------------------------------------------

        /// <summary>A box of the map where inputs are drawn, m.</summary>
        private readonly struct Spot
        {
            public readonly string Name;
            public readonly Double3 Min, Max;

            public Spot(string name, Double3 min, Double3 max)
            {
                Name = name;
                Min = min;
                Max = max;
            }

            public Double3 Centre => (Min + Max) * 0.5;
        }

        /// <summary>The first object named `variant`.</summary>
        private static int Find(MapWorld world, string variant)
        {
            for (int obj = 0; obj < world.PlacedObjects; obj++)
                if (world.ObjectName(obj) == variant)
                    return obj;
            throw new InvalidOperationException(variant + " is not placed on the test range");
        }

        /// <summary>The box of an object's shape centres and wire ends (of the material `only`, when given), padded.</summary>
        private static Spot Around(MapWorld world, string name, int obj, double pad, string only = null)
        {
            Double3 min = new Double3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity), max = -min;
            for (int s = 0; world.Geometry(obj, s, out ShapeGeometry g); s++)
            {
                if (only != null && world.MaterialName(g.Material) != only)
                    continue;
                foreach (Double3 p in g.Kind == ShapeKind.Wire ? new[] { g.SpanA, g.SpanB } : new[] { g.Center })
                {
                    min = new Double3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Double3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
            }
            var padding = new Double3(pad, pad, pad);
            return new Spot(name, min - padding, max + padding);
        }

        /// <summary>The golden's spots, in canonical order: the busiest places of the map, each kind of shape among them.</summary>
        private static Spot[] GoldenSpots(MapWorld world)
        {
            Double3 stand = StandFoot(world);
            return new[]
            {
                Around(world, "crown", Find(world, "V01_L"), 0.3, "twig"),
                Around(world, "house", Find(world, "H01_D"), 0.3),
                Around(world, "trench", Find(world, "E01_curving"), 0.3),
                new Spot("posadka", new Double3(-150, 0, -12), new Double3(165, 3, -8)),
                Around(world, "line", Find(world, "L03_medium"), 0.3),
                Around(world, "shaft", Find(world, "Z04_cellar_shaft"), 0.5),
                Around(world, "patch", Find(world, "S04_patch"), 0.3),
                new Spot("stand", stand + new Double3(-0.5, 0, -0.5), stand + new Double3(0.5, 0.5, 0.5)),
            };
        }

        private static Double3 In(ref Stream s, in Spot spot) =>
            new Double3(s.In(spot.Min.X, spot.Max.X), s.In(spot.Min.Y, spot.Max.Y), s.In(spot.Min.Z, spot.Max.Z));

        /// <summary>Inputs over the spots; capsules move up to `reach` m a step on each axis (the golden's 0.3 m exercises the latch).</summary>
        private static void SpotInputs(Spot[] spots, int n, out XZ[] points, out Capsule[] previous, out Capsule[] current, out Ray[] rays,
            double reach = 0.3)
        {
            var s = new Stream(450);
            points = new XZ[n];
            previous = new Capsule[n];
            current = new Capsule[n];
            rays = new Ray[n];
            for (int i = 0; i < n; i++)
            {
                Spot spot = spots[i % spots.Length];
                Double3 p = In(ref s, spot);
                points[i] = new XZ(p.X, p.Z);
                double r = s.In(0.003, 0.06);
                Double3 a = In(ref s, spot), b = i % 4 == 0 ? a : a + s.Move(0.15), move = s.Move(reach);
                previous[i] = new Capsule(a, b, r);
                current[i] = new Capsule(a + move, b + move + s.Move(0.02 * reach / 0.3), r);
                Double3 d = s.Move(1);
                rays[i] = new Ray(In(ref s, spot), d.Length() < 1e-3 ? new Double3(0, -1, 0) : d);
            }
        }

        // ---- Golden --------------------------------------------------------------------------------------------------

        private static List<(string Name, string Value, bool Agree)> TestRangeCases()
        {
            MapWorld world = TestRange();
            Spot[] spots = GoldenSpots(world);
            var cases = new List<(string, string, bool)>
            {
                ("query_version", world.QueryVersion.ToString(CultureInfo.InvariantCulture), true),
                ("content_hash", Hex(world.ContentHash), true),
            };
            SpotInputs(spots, GoldenCount, out XZ[] points, out Capsule[] previous, out Capsule[] current, out Ray[] rays);

            var batch = new GroundSample[points.Length];
            var single = new GroundSample[1];
            world.SampleGround(points, batch);
            Hasher batched = new Hasher(), oneByOne = new Hasher();
            int holes = 0;
            for (int i = 0; i < points.Length; i++)
            {
                batched.Add(batch[i]);
                world.SampleGround(points.AsSpan(i, 1), single);
                oneByOne.Add(single[0]);
                holes += batch[i].Flags == GroundFlags.Hole ? 1 : 0;
            }
            cases.Add(("test-range.ground", Hex(batched.Value), batched.Value == oneByOne.Value));
            Console.WriteLine($"  test-range.ground: {holes} of {points.Length} samples over a patch or a hole band");

            // Each capsule twice: into a buffer of 64, and into one of 2 (the overflow: the true count, the first two kept).
            var contacts = new StaticContact[64];
            var small = new StaticContact[2];
            Hasher contactHash = new Hasher(), overflowHash = new Hasher();
            int touched = 0, early = 0, ground = 0, soil = 0, wires = 0, stand = 0, overflowed = 0, most = 0;
            bool agree = true;
            for (int i = 0; i < previous.Length; i++)
            {
                int n = world.StaticContacts(previous[i], current[i], PhysicsMargin, contacts);
                contactHash.Add(n);
                for (int k = 0; k < Math.Min(n, contacts.Length); k++)
                {
                    StaticContact c = contacts[k];
                    contactHash.Add(c);
                    early += c.Time < 1 ? 1 : 0;
                    ground += c.Object == WorldIds.Ground ? 1 : 0;
                    soil += c.Object != WorldIds.Ground && c.Surface != WorldIds.NoSurface ? 1 : 0;
                    wires += c.WireParam >= 0 ? 1 : 0;
                    stand += c.Object >= world.PlacedObjects ? 1 : 0;
                }
                touched += n > 0 ? 1 : 0;
                most = Math.Max(most, n);
                int m = world.StaticContacts(previous[i], current[i], PhysicsMargin, small);
                overflowHash.Add(m);
                overflowed += m > small.Length ? 1 : 0;
                for (int k = 0; k < Math.Min(m, small.Length); k++)
                    overflowHash.Add(small[k]);
                agree &= m == n && (m == 0 || small[0].Object == contacts[0].Object && small[0].Shape == contacts[0].Shape
                    && small[0].Time.Equals(contacts[0].Time) && (m < 2 || small[1].Shape == contacts[1].Shape && small[1].Object == contacts[1].Object));
            }
            cases.Add(("test-range.contacts", Hex(contactHash.Value), true));
            cases.Add(("test-range.contacts_overflow", Hex(overflowHash.Value), agree));
            Console.WriteLine($"  test-range.contacts: {touched} of {previous.Length} capsules in contact (up to {most}), {early} contacts with Time < 1; "
                + $"{ground} on the plane, {soil} on soil shapes, {wires} on wires, {stand} on the stand; {overflowed} overflow a buffer of 2");

            var hits = new RayHit[rays.Length];
            var hit = new RayHit[1];
            world.Raycast(rays, 5, hits);
            Hasher rayBatched = new Hasher(), rayOne = new Hasher();
            int onGround = 0, onObjects = 0, onWires = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                rayBatched.Add(hits[i]);
                world.Raycast(rays.AsSpan(i, 1), 5, hit);
                rayOne.Add(hit[0]);
                if (hits[i].Distance < double.PositiveInfinity)
                {
                    bool isGround = hits[i].Object == WorldIds.Ground;
                    onGround += isGround ? 1 : 0;
                    onObjects += isGround ? 0 : 1;
                    onWires += !isGround && world.Geometry(hits[i].Object, hits[i].Shape, out ShapeGeometry g) && g.Kind == ShapeKind.Wire ? 1 : 0;
                }
            }
            cases.Add(("test-range.rays", Hex(rayBatched.Value), rayBatched.Value == rayOne.Value));
            Console.WriteLine($"  test-range.rays: {onGround} of {rays.Length} rays hit the plane, {onObjects} a shape ({onWires} a wire)");
            return cases;
        }

        // ---- Bench ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// One step's queries of a drone flying a path, as p6-flight-core 1.6's ContactModel makes them: its swept parts at
        /// drone.json's margin (contacts.margin_m, 0.1 m); each prop disc's bounding sphere at margin 0, then, when it meets
        /// anything, a chord capsule over each three of its 12 rim segments, then the segments under each chord that meets
        /// anything; a fiber trailing along the flight path; its ground samples and its rotor rays up and down.
        /// </summary>
        private sealed class Pattern
        {
            private const double Margin = 0.1; // drone.json contacts.margin_m
            private const double Disc = 0.127, Blade = 0.004;
            /// <summary>A 3-segment chord's farthest rim vertex from it, over the disc radius: cos 15° − cos 45°.</summary>
            private const double ChordReach = 0.2588190451025208;
            public readonly string Name;
            private readonly Double3[] _a, _b;   // parts in the drone's frame (axis-aligned; translated only)
            private readonly double[] _r;
            private readonly Double3[] _discs;   // prop hub centres
            private readonly Double3[] _samples; // ground sample offsets (their x, z)
            private readonly Double3[] _rotors;  // ray origins, each cast up and down
            private readonly int _fiber;         // fiber segments: node k on the path 25·k steps back
            private readonly Double3 _coil = new Double3(0, -0.06, -0.12);
            private readonly XZ[] _points;
            private readonly Ray[] _rays;
            private readonly GroundSample[] _ground;
            private readonly RayHit[] _hits;
            private readonly StaticContact[] _contacts = new StaticContact[64];
            /// <summary>The last step's swept queries; and since the last Reset, the sweeps' and the rays' time and counts.</summary>
            public int Queries;
            public long SweepTicks, RayTicks, Sweeps, Rays;

            public void Reset() => SweepTicks = RayTicks = Sweeps = Rays = 0;

            public Pattern(string name, List<(Double3 A, Double3 B, double R)> parts, Double3[] discs, Double3[] samples, Double3[] rotors, int fiber)
            {
                Name = name;
                _a = new Double3[parts.Count];
                _b = new Double3[parts.Count];
                _r = new double[parts.Count];
                for (int i = 0; i < parts.Count; i++)
                    (_a[i], _b[i], _r[i]) = parts[i];
                _discs = discs;
                _samples = samples;
                _rotors = rotors;
                _fiber = fiber;
                _points = new XZ[samples.Length + fiber];
                _rays = new Ray[2 * rotors.Length];
                _ground = new GroundSample[_points.Length];
                _hits = new RayHit[_rays.Length];
            }

            public void Step(IWorldQuery world, Func<int, Double3> path, int s)
            {
                Double3 from = path(s), to = path(s + 1);
                for (int i = 0; i < _samples.Length; i++)
                    _points[i] = new XZ(to.X + _samples[i].X, to.Z + _samples[i].Z);
                for (int k = 1; k <= _fiber; k++)
                {
                    Double3 node = path(s + 1 - 25 * k);
                    _points[_samples.Length + k - 1] = new XZ(node.X, node.Z);
                }
                world.SampleGround(_points, _ground);
                long t0 = Stopwatch.GetTimestamp();
                int queries = 0;
                for (int i = 0; i < _a.Length; i++, queries++)
                    world.StaticContacts(new Capsule(from + _a[i], from + _b[i], _r[i]), new Capsule(to + _a[i], to + _b[i], _r[i]), Margin, _contacts);
                // The fiber: laid where the drone flew, so only its first segment (from the coil) moves.
                for (int k = 0; k < _fiber; k++, queries++)
                {
                    Double3 near0 = k == 0 ? from + _coil : path(s + 1 - 25 * k), near1 = k == 0 ? to + _coil : near0;
                    Double3 far = path(s + 1 - 25 * (k + 1));
                    world.StaticContacts(new Capsule(near0, far, 0.000125), new Capsule(near1, far, 0.000125), Margin, _contacts);
                }
                for (int d = 0; d < _discs.Length; d++, queries++)
                {
                    Double3 c0 = from + _discs[d], c1 = to + _discs[d];
                    if (world.StaticContacts(new Capsule(c0, c0, Disc + Blade), new Capsule(c1, c1, Disc + Blade), 0, _contacts) == 0)
                        continue;
                    for (int chord = 0; chord < 4; chord++, queries++)
                    {
                        Double3 ca = Rim(3 * chord), cb = Rim(3 * chord + 3);
                        double reach = ChordReach * Disc + Blade + 1e-6;
                        if (world.StaticContacts(new Capsule(c0 + ca, c0 + cb, reach), new Capsule(c1 + ca, c1 + cb, reach), 0, _contacts) == 0)
                            continue;
                        for (int k = 3 * chord; k < 3 * chord + 3; k++, queries++)
                        {
                            Double3 u = Rim(k), v = Rim(k + 1);
                            world.StaticContacts(new Capsule(c0 + u, c0 + v, Blade), new Capsule(c1 + u, c1 + v, Blade), 0, _contacts);
                        }
                    }
                }
                long t1 = Stopwatch.GetTimestamp();
                for (int i = 0; i < _rotors.Length; i++)
                {
                    _rays[2 * i] = new Ray(to + _rotors[i], new Double3(0, -1, 0));
                    _rays[2 * i + 1] = new Ray(to + _rotors[i], new Double3(0, 1, 0));
                }
                world.Raycast(_rays, 5, _hits);
                long t2 = Stopwatch.GetTimestamp();
                Queries = queries;
                SweepTicks += t1 - t0;
                RayTicks += t2 - t1;
                Sweeps += queries;
                Rays += _rays.Length;
            }

            /// <summary>The ring's k-th rim point: 12 on the disc's circle (exact 30° steps from a table).</summary>
            private static Double3 Rim(int k)
            {
                k %= 12;
                double c = RimCos[k], s = RimCos[(k + 9) % 12];
                return new Double3(Disc * c, 0, Disc * s);
            }

            private static readonly double[] RimCos = { 1, 0.8660254037844387, 0.5, 0, -0.5, -0.8660254037844387, -1, -0.8660254037844387, -0.5, 0, 0.5, 0.8660254037844387 };
        }

        /// <summary>
        /// Physics' pattern (p6-flight-core 1.6, drone.json's contacts at L4, rounded): 4 skids as a capsule and two support
        /// spheres each, 4 arms, the coil, and 4 prop discs with their chords and rings on demand; 12 samples (the supports
        /// and rotors); 8 rays (4 rotors, up and down).
        /// </summary>
        private static Pattern PhysicsPattern()
        {
            var parts = new List<(Double3, Double3, double)>();
            var samples = new List<Double3>();
            var rotors = new List<Double3>();
            for (int c = 0; c < 4; c++)
            {
                double sx = (c & 1) == 0 ? 1 : -1, sz = (c & 2) == 0 ? 1 : -1;
                Double3 joint = new Double3(sx * 0.099, -0.248, sz * 0.099), end = new Double3(sx * 0.099, -0.248, sz * 0.220);
                parts.Add((joint, end, 0.005));
                parts.Add((joint, joint, 0.005));
                parts.Add((end, end, 0.005));
                parts.Add((new Double3(sx * 0.03, 0, sz * 0.03), new Double3(sx * 0.16, 0, sz * 0.16), 0.003));
                samples.Add(joint);
                samples.Add(end);
                rotors.Add(new Double3(sx * 0.16, 0.03, sz * 0.16));
            }
            parts.Add((new Double3(0, 0.02, -0.1), new Double3(0, 0.02, 0.1), 0.04));
            samples.AddRange(rotors);
            return new Pattern("physics 12 + 17 + 4 discs + 8", parts, rotors.ToArray(), samples.ToArray(), rotors.ToArray(), 0);
        }

        /// <summary>
        /// K§10.5's batch: 44 samples (4 feet, 4 rotors, 4 body points, 32 fiber nodes), 46 swept capsules (4 feet, 4 arms,
        /// 4 disc spheres with their gates, the body and the coil, 32 fiber segments laid along the path's last 800 steps, the
        /// first from the coil), 8 rays.
        /// </summary>
        private static Pattern BatchPattern()
        {
            var parts = new List<(Double3, Double3, double)>();
            var samples = new List<Double3>();
            var rotors = new List<Double3>();
            for (int c = 0; c < 4; c++)
            {
                double sx = (c & 1) == 0 ? 1 : -1, sz = (c & 2) == 0 ? 1 : -1;
                Double3 foot = new Double3(sx * 0.099, -0.248, sz * 0.16);
                parts.Add((foot, foot, 0.005));
                parts.Add((new Double3(sx * 0.03, 0, sz * 0.03), new Double3(sx * 0.16, 0, sz * 0.16), 0.003));
                samples.Add(foot);
                rotors.Add(new Double3(sx * 0.16, 0.03, sz * 0.16));
                samples.Add(new Double3(sx * 0.05, 0, sz * 0.05));
            }
            parts.Add((new Double3(0, 0.02, -0.1), new Double3(0, 0.02, 0.1), 0.04));
            parts.Add((new Double3(0, -0.06, -0.12), new Double3(0, -0.06, -0.02), 0.03));
            samples.AddRange(rotors);
            return new Pattern("K10.5 44 + 46 + 8", parts, rotors.ToArray(), samples.ToArray(), rotors.ToArray(), 32);
        }

        /// <summary>A triangle wave in [0, 1] of period `period` steps.</summary>
        private static double Tri(int s, int period)
        {
            double f = (double)((s % period + period) % period) / period;
            return f < 0.5 ? 2 * f : 2 - 2 * f;
        }

        /// <summary>A drone's path through a spot at step s: triangle waves of unlike periods on each axis (10–25 m/s).</summary>
        private static Double3 Lissajous(in Spot spot, int s) => new Double3(
            spot.Min.X + (spot.Max.X - spot.Min.X) * Tri(s, 2 * 487), spot.Min.Y + (spot.Max.Y - spot.Min.Y) * Tri(s, 2 * 709),
            spot.Min.Z + (spot.Max.Z - spot.Min.Z) * Tri(s, 2 * 593));

        /// <summary>E01's run: its centre line from the placement file's ground.holes record "run", m.</summary>
        private static Double3[] RunLine()
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "maps", "test-range", "placements.json")));
            foreach (JsonElement hole in doc.RootElement.GetProperty("ground").GetProperty("holes").EnumerateArray())
            {
                if (hole.GetProperty("record").GetString() != "run")
                    continue;
                var line = new List<Double3>();
                foreach (JsonElement p in hole.GetProperty("centre_line_m").EnumerateArray())
                    line.Add(new Double3(p[0].GetDouble(), p[1].GetDouble(), p[2].GetDouble()));
                return line.ToArray();
            }
            throw new InvalidOperationException("placements.json has no run");
        }

        /// <summary>
        /// The footprint of H01_D's house itself (its metadata's bounds.lod: the walls, without the yard), turned by its row's
        /// yaw onto the map and grown by `pad`, m.
        /// </summary>
        private static Spot HouseWalls(MapWorld world, int house, double pad)
        {
            string root = RepositoryRoot();
            using JsonDocument rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "maps", "test-range", "placements.json")));
            JsonElement row = rows.RootElement.GetProperty("instances")[house];
            string meta = Path.Combine(root, MapWorld.ModelsRoot, row.GetProperty("class").GetString(), row.GetProperty("asset").GetString(),
                world.ObjectName(house) + ".assetmeta.json");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(meta));
            JsonElement lod = doc.RootElement.GetProperty("bounds").GetProperty("lod");
            JsonElement at = row.GetProperty("position_m");
            double yaw = row.GetProperty("yaw_deg").GetDouble() * Math.PI / 180, c = Math.Cos(yaw), sn = Math.Sin(yaw);
            double minX = double.PositiveInfinity, minZ = double.PositiveInfinity, maxX = double.NegativeInfinity, maxZ = double.NegativeInfinity;
            foreach (string cx in new[] { "min", "max" })
            {
                foreach (string cz in new[] { "min", "max" })
                {
                    double lx = lod.GetProperty(cx)[0].GetDouble(), lz = lod.GetProperty(cz)[2].GetDouble();
                    double x = at[0].GetDouble() + lx * c + lz * sn, z = at[2].GetDouble() - lx * sn + lz * c; // Unity's yaw
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minZ = Math.Min(minZ, z);
                    maxZ = Math.Max(maxZ, z);
                }
            }
            return new Spot("walls", new Double3(minX - pad, 0, minZ - pad), new Double3(maxX + pad, 0, maxZ + pad));
        }

        /// <summary>The point at `along` m round a box's footprint (x, z) at its lower y, the loop repeated.</summary>
        private static Double3 Round(in Spot box, double along)
        {
            double w = box.Max.X - box.Min.X, d = box.Max.Z - box.Min.Z, f = Math.Max(0, along) % (2 * (w + d));
            if (f < w) return new Double3(box.Min.X + f, 0, box.Min.Z);
            if ((f -= w) < d) return new Double3(box.Max.X, 0, box.Min.Z + f);
            if ((f -= d) < w) return new Double3(box.Max.X - f, 0, box.Max.Z);
            return new Double3(box.Min.X, 0, box.Max.Z - (f - w));
        }

        /// <summary>The point at `along` m along a polyline, back and forth (a ping-pong over its length).</summary>
        private static Double3 Along(Double3[] line, double along)
        {
            double total = 0;
            for (int k = 1; k < line.Length; k++)
                total += (line[k] - line[k - 1]).Length();
            double f = Math.Max(0, along) % (2 * total);
            f = f < total ? f : 2 * total - f;
            for (int k = 1; k < line.Length; k++)
            {
                double l = (line[k] - line[k - 1]).Length();
                if (f <= l)
                    return line[k - 1] + (line[k] - line[k - 1]) * (f / l);
                f -= l;
            }
            return line[line.Length - 1];
        }

        private static int TestRangeBench()
        {
            Console.WriteLine("  the test range (D-047), with a stand at the field start:");
            MapWorld world = TestRange();
            int crown = Find(world, "V01_L"), house = Find(world, "H01_D"), trench = Find(world, "E01_curving"), patch = Find(world, "S04_patch");
            world.Geometry(patch, 1, out ShapeGeometry strip); // a straw strip, its top the plane's height
            Double3 straw = new Double3(strip.Center.X, 0.235, strip.Center.Z), stand = StandFoot(world) + new Double3(0, 0.253, 0);
            Spot inCrown = Around(world, "crown", crown, 0, "twig");
            Spot walls = HouseWalls(world, house, 0.25);
            Double3[] run = RunLine();
            Double3 legFrom = new Double3(-150, 2, -10), legTo = new Double3(165, 2, -10);
            const int steps = 20000;
            (string Name, Func<int, Double3> Path)[] paths =
            {
                ($"inside V01_L's crown (object {crown})", s => Lissajous(inCrown, s)),
                ($"round H01_D's walls at 10 m/s, 0.25 m out, 0.4-4.5 m up under its eaves (object {house})",
                    s => Round(walls, 0.01 * s) + new Double3(0, 0.4 + 4.1 * Tri(s, 2 * 1709), 0)),
                ($"along and over E01's run at 10 m/s, its skids from the floor to 1 m up (object {trench})",
                    s => Along(run, 0.01 * s) + new Double3(0, -1.25 + 2.25 * Tri(s, 2 * 1709), 0)),
                ("the posadka at 2 m (its bench leg, 15.75 m/s)", s => legFrom + (legTo - legFrom) * (Math.Max(0, s) / (double)steps)),
                // physics' 1.6b bench rows: the skids 13 mm into the straw, the pose trembling by a micrometre; the arms on the
                // stand's post tops for 1 s, then a 0.5 m/s climb.
                ($"resting in S04's straw (object {patch}), the skids 13 mm into it", s => straw + new Double3(0, 1e-6 * Tri(s, 14), 0)),
                ("on the field start's stand, its arms on the posts for 1 s, then climbing at 0.5 m/s",
                    s => stand + new Double3(0, 0.0005 * Math.Max(0, s - 1000), 0)),
            };
            var times = new long[steps];
            int failures = 0;
            double us = 1e6 / Stopwatch.Frequency;
            Pattern physics = PhysicsPattern();
            foreach (Pattern pattern in new[] { physics, BatchPattern() })
            {
                foreach (var path in paths)
                {
                    long most = 0, queries = 0, allocated = 0;
                    for (int pass = 0; pass < 2; pass++) // the first pass warms the JIT and the caches
                    {
                        long before = GC.GetAllocatedBytesForCurrentThread();
                        queries = 0;
                        if (pattern == physics && pass == 0 && path.Name == paths[0].Name)
                            physics.Reset();
                        for (int s = 0; s < steps; s++)
                        {
                            long start = Stopwatch.GetTimestamp();
                            pattern.Step(world, path.Path, s);
                            times[s] = Stopwatch.GetTimestamp() - start;
                            queries += pattern.Queries;
                        }
                        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    }
                    Array.Sort(times);
                    most = times[steps - 1];
                    double median = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us;
                    // D-054: physics' pattern is judged inside PLT-04's 300 us step (p99, 0 B); 60 us is the target, printed.
                    bool judged = pattern == physics, over = judged && !(p99 < 300);
                    failures += over || allocated != 0 ? 1 : 0;
                    Console.WriteLine($"  {pattern.Name}, {path.Name}: median {median:F2} us, p99 {p99:F2} us, max {most * us:F1} us, "
                        + $"{(double)queries / steps:F1} swept queries a step, {allocated} B; the 60 us target {(p99 < 60 ? "met" : "missed")} at p99"
                        + (over ? " (OVER the step's 300 us)" : judged ? "" : " (shown: judged at phase 7d, D-054)"));
                }
            }

            // K§10.4's 100 000 capsules and rays, in physics' pattern: its swept queries and rotor rays over the six paths
            // (both passes), per 100 000.
            double perSweeps = physics.SweepTicks * 1e3 / Stopwatch.Frequency * 100000 / physics.Sweeps;
            double perRays = physics.RayTicks * 1e3 / Stopwatch.Frequency * 100000 / physics.Rays;
            Console.WriteLine($"  test-range.capsules in physics' pattern ({physics.Sweeps} swept over the six paths): {perSweeps:F2} ms per 100000 (budget 100 ms)");
            Console.WriteLine($"  test-range.rays in physics' pattern ({physics.Rays} rotor rays of 5 m): {perRays:F2} ms per 100000 (budget 100 ms)");
            failures += perSweeps < 100 && perRays < 100 ? 0 : 1;

            // 100 000 of each at random poses in the golden's spot boxes (solids included): shown, not judged.
            Spot[] spots = GoldenSpots(world);
            const int n = 100000;
            SpotInputs(spots, n, out XZ[] points, out Capsule[] previous, out Capsule[] current, out Ray[] rays, 0.03);
            var samples = new GroundSample[n];
            var hits = new RayHit[n];
            var contacts = new StaticContact[64];
            world.SampleGround(points, samples); // warm
            for (int i = 0; i < 1000; i++)
                world.StaticContacts(previous[i], current[i], PhysicsMargin, contacts);
            world.Raycast(rays.AsSpan(0, 1000), 2, hits);
            long a0 = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < n; i++)
                world.StaticContacts(previous[i], current[i], PhysicsMargin, contacts);
            long t1 = Stopwatch.GetTimestamp();
            world.Raycast(rays, 2, hits);
            long t2 = Stopwatch.GetTimestamp();
            world.SampleGround(points, samples);
            long t3 = Stopwatch.GetTimestamp(), a1 = GC.GetAllocatedBytesForCurrentThread();
            double ms = 1e3 / Stopwatch.Frequency;
            double capsulesMs = (t1 - t0) * ms, raysMs = (t2 - t1) * ms, samplesMs = (t3 - t2) * ms;
            Console.WriteLine($"  test-range.capsules {n} at random poses in {spots.Length} spot boxes, moving up to 30 mm: {capsulesMs:F2} ms (shown)");
            Console.WriteLine($"  test-range.rays {n} of <= 2 m at random poses there: {raysMs:F2} ms (shown)");
            Console.WriteLine($"  test-range.samples {n} there: {samplesMs:F2} ms (budget 10 ms); {a1 - a0} B allocated by all three");
            failures += samplesMs < 10 && a1 == a0 ? 0 : 1;
            Console.WriteLine(failures == 0 ? "worldbench: bench done, every budget met (judged on AC only)" : $"worldbench: {failures} over budget or allocating");
            return failures == 0 ? 0 : 1;
        }
    }
}
