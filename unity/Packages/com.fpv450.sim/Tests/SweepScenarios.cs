using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The map-wide landing sweep (w9-village-physics 3.0): start points drawn from a seed over a map's own ground classes, slope
    /// bands and (on a map with a stem field) its bare, short and tall grass, in proportion to their area with at least MinPerClass
    /// of each; at each a hover-and-land with a liftoff after it, a cut landing at 0.5 m/s, a drop from 1 m and a slide at 2 m/s, each
    /// at its own drawn heading, flown by SimPilot on the pilot's loadout (L1: 2 kg, legs on) with every model of the player's
    /// flight but the line. Every step is judged by the invariants (Check), each a verdict of its own with its first failing step.
    /// tools/flightbench --sweep runs it over a map; SweepTests holds its known-wrong. It reads the core and never changes it.
    /// </summary>
    public static class SweepScenarios
    {
        public enum Manoeuvre : byte { Land = 0, Cut = 1, Drop = 2, Slide = 3, Liftoff = 4 }

        public const int Manoeuvres = 5;

        /// <summary>The invariants, each a row's verdict.</summary>
        public enum Check : byte
        {
            /// <summary>Nothing NaN or unbounded: the state finite, |v| ≤ MaxSpeed, |ω| ≤ MaxRate, within MaxRange of the start.</summary>
            Finite = 0,
            /// <summary>No frame part, support or skid deeper past the ground's (or a soil shape's) surface than GroundTolerance beyond
            /// what its corner's soil model says its nodes sank and its leg's own give (0 for a half whose leg is gone), or, for a frame
            /// part, its own penalty spring's overlap at the force its pairs carry.</summary>
            Ground,
            /// <summary>Every touching pair's force acts within Reach of the CoM (PairReach), so the contacts' moment is at most Reach
            /// times the sum of their forces' sizes (the spec's rule for G1).</summary>
            Lever,
            /// <summary>The body's kinetic energy since the start never above the rotors', the air's and gravity's work (the blades'
            /// passes among the rotors', up to what they took from the rotors) by more than EnergyTolerance: the contacts give back no
            /// more than they took.</summary>
            Energy,
            /// <summary>No part logs the same event (kind, part, index, object, shape) twice in a step (G5's kind).</summary>
            EventsOnce,
            /// <summary>The step's event buffer refuses nothing.</summary>
            EventsKept,
            /// <summary>No WorldBug event, no Tunnel through anything rigid, and none through the ground or a soil shape deeper than
            /// GroundTolerance past its soil's sink and the part's own give (Ground's allowance).</summary>
            Tunnel,
            /// <summary>A gentle landing's peak acceleration after the touch under twice the legs' design 50 g (flightbench --slopes' rule).</summary>
            Peak,
            /// <summary>The drone at rest within RestSeconds of the touch (and the touch comes).</summary>
            Rest,
            /// <summary>The liftoff leaves the ground within LiftSeconds (judged where the landing ended upright).</summary>
            Lifts,
            /// <summary>The flight's log replays headless bit for bit.</summary>
            Replay,
            /// <summary>The steps after the first second allocate 0 B.</summary>
            Alloc,
            /// <summary>The core throws nothing.</summary>
            Throws,
        }

        public const int Checks = 13;

        // ---- The limits (each assumed, with why) --------------------------------------------------------------------------

        /// <summary>The drone's reach about its CoM, m (AnchorReachTests.Reach, the spec's 0.5 m).</summary>
        public const double Reach = 0.5;

        /// <summary>The deepest a part may lie past the ground's surface, m (assumed: the ledger's gap 14 names no number. 20 mm is
        /// several times the sink the soil model gives a loaded skid on the softest class at a landing (tilled, mm); a skid 17–19 cm
        /// in is a part through the ground). A frame part's penalty overlap is counted at its force (PairReach.Backed), not in it:
        /// a motor can on E01's soil took 26 mm at 2.2 kN in a 4.9 m/s drop (3.0b).</summary>
        public const double GroundTolerance = 0.020;

        /// <summary>The energy the contacts may return beyond what they took since the start, J (assumed: 1 % of a 2.5 kg drone's
        /// kinetic energy at 2 m/s, ten times the rounding of the step's work sums measured on a level hover).</summary>
        public const double EnergyTolerance = 0.05;

        public const double MaxSpeed = 50, MaxRate = 100, MaxRange = 1000;

        /// <summary>At rest: for RestHold steps in a row the body's mean speed and turn rate since the hold began under RestSpeed and
        /// RestRate (it goes nowhere: an idle motor's vibration in place is not motion), within RestSeconds of the touch (the spec's
        /// 10 s).</summary>
        public const double RestSpeed = 0.05, RestRate = 0.1, RestSeconds = 10;
        public const int RestHold = 500;

        /// <summary>A liftoff leaves within this, s (the held-leg harness's 5 s).</summary>
        public const double LiftSeconds = 5;

        /// <summary>A gentle landing's peak acceleration bound, m/s² (flightbench --slopes' land rows: 2 × 490.3).</summary>
        public const double PeakBound = 2 * 490.3;

        /// <summary>At least this many points of every class, band and stem stratum.</summary>
        public const int MinPerClass = 20;

        /// <summary>The slope bands' lower edges, deg.</summary>
        public static readonly double[] Bands = { 0, 5, 10, 15, 20, 30 };

        public static string BandName(int b) => b + 1 < Bands.Length ? $"{Bands[b]:F0}-{Bands[b + 1]:F0}°" : $"{Bands[b]:F0}°+";

        /// <summary>The map's own rectangle (farmland's map.json map_rect_m; the test range's placements.json extent_m).</summary>
        public static (double X0, double Z0, double X1, double Z1) Rect(string mapId) =>
            mapId == "farmland" ? (0, 0, 1000, 1000) : mapId == "test-range" ? (-256, -256, 256, 256)
            : throw new ArgumentException("the sweep knows the rectangles of farmland and test-range, not " + mapId);

        // ---- The points ---------------------------------------------------------------------------------------------------

        public struct Point
        {
            public int Index;
            public double X, Z, Height, Slope;
            public Double3 Normal;
            public string Surface, Stems;
            public int Band;
            /// <summary>Drawn to fill a class's minimum, not in proportion.</summary>
            public bool Quota;
        }

        /// <summary>SplitMix64: the sweep's own draws (no System.Random, whose algorithm is the runtime's).</summary>
        public static ulong Mix(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        static double Unit(ulong x) => (x >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>The ground a drone would land on at (x, z): the first surface a ray from above meets, if it is the ground or a
        /// soil shape; false under any other object (a crown, a roof, a wire).</summary>
        public static bool GroundAt(MapWorld map, double x, double z, out double height, out Double3 normal, out byte surface) =>
            GroundAt(map, x, z, out height, out normal, out surface, out _);

        /// <summary>The same, and over a ground patch or a hole (SampleGround's Hole: their shapes are the ground there, S04's straw
        /// among them) the patch's object, whatever its first shape's material, if its top lies within 1.5 m of the plain ground
        /// around it (else a crown or a roof over the patch); patch −1 elsewhere.</summary>
        public static bool GroundAt(MapWorld map, double x, double z, out double height, out Double3 normal, out byte surface, out int patch)
        {
            var hits = new RayHit[1];
            map.Raycast(new[] { new Ray(new Double3(x, 300, z), new Double3(0, -1, 0)) }, 600, hits);
            height = hits[0].Point.Y;
            normal = hits[0].Normal;
            surface = hits[0].Surface;
            patch = -1;
            if (double.IsInfinity(hits[0].Distance)) return false;
            var g = new GroundSample[5];
            map.SampleGround(new[] { new XZ(x, z), new XZ(x + 30, z), new XZ(x - 30, z), new XZ(x, z + 30), new XZ(x, z - 30) }, g);
            if ((g[0].Flags & GroundFlags.Hole) != 0 && hits[0].Object != WorldIds.Ground)
            {
                double plain = double.PositiveInfinity;
                for (int i = 1; i < 5; i++) if ((g[i].Flags & GroundFlags.Hole) == 0) plain = Math.Min(plain, g[i].Height);
                if (!(height - plain < 1.5)) return false;
                patch = hits[0].Object;
                return true;
            }
            return surface != 0 && (hits[0].Object == WorldIds.Ground || hits[0].Material == WorldIds.NoMaterial);
        }

        /// <summary>Whether a column of the drone's size over (x, h, z) is clear of every object but the ground and soil, and of the
        /// patch it stands on but for its lumps (S04's straw stands a few cm proud of the point the ray met: within PatchLumps), and of
        /// any ground rising WallRise over it within the column (0.6 m in 0.5 m: steeper than 50°).</summary>
        public const double PatchLumps = 0.15, WallRise = 0.6;

        public static bool Clear(MapWorld map, double x, double h, double z, int patch = -1)
        {
            var found = new StaticContact[16];
            var column = new Capsule(new Double3(x, h + 0.55, z), new Double3(x, h + 2.5, z), 0.5);
            int n = Math.Min(map.StaticContacts(column, column, 0, found, QueryFilter.None), found.Length);
            for (int i = 0; i < n; i++)
            {
                if (found[i].Object != WorldIds.Ground && found[i].Surface == 0 && !(found[i].Object == patch && found[i].Distance > -PatchLumps)) return false;
                // Ground or soil standing WallRise over the spot within the drone's reach is a wall of earth beside it (a cut's
                // side: the range's E01 floor lies 1.35 m under the plane around it), not the slope it lands on.
                if (found[i].Surface != 0 && found[i].Point.Y > h + WallRise) return false;
            }
            return true;
        }

        /// <summary>The point at (x, z), classified; false where it is not a landing spot (under or inside an object).</summary>
        public static bool Classify(MapWorld map, double x, double z, out Point p)
        {
            p = default;
            if (!GroundAt(map, x, z, out double h, out Double3 n, out byte surface, out int patch) || !Clear(map, x, h, z, patch)) return false;
            double slope = Math.Atan2(Math.Sqrt(n.X * n.X + n.Z * n.Z), n.Y) * 57.29577951308232;
            int band = 0;
            while (band + 1 < Bands.Length && slope >= Bands[band + 1]) band++;
            string stems = null;
            if (map.MapId == "farmland")
            {
                // The stem field's strata under the drone's footprint: none, the short layers, a tall layer (map.json's Tall* groups, 10–13).
                var patches = new StemPatch[64];
                int k = Math.Min(map.StemsNear(new XZ(x, z), 0.4, 0, patches), patches.Length);
                stems = "bare";
                for (int i = 0; i < k; i++) stems = patches[i].Layer >= 10 ? "tall" : stems == "tall" ? "tall" : "short";
            }
            p = new Point { X = x, Z = z, Height = h, Normal = n, Slope = slope, Band = band, Stems = stems,
                Surface = patch >= 0 ? "patch:" + map.ObjectName(patch) : map.SurfaceName(surface) };
            return true;
        }

        /// <summary>
        /// The sweep's points: `count` drawn uniformly over the map's rectangle (so each class, band and stratum in proportion to
        /// its area), then, from the same stream, enough more of each class, band and stratum that has fewer than MinPerClass (at
        /// most `cap` candidates in all). candidates: the stream's length used.
        /// </summary>
        public static Point[] Draw(MapWorld map, int count, ulong seed, out long candidates, long cap = 4_000_000)
        {
            var (x0, z0, x1, z1) = Rect(map.MapId);
            var points = new List<Point>();
            var counts = new Dictionary<string, int>();
            ulong state = Mix(seed ^ 0x5357454550UL);
            candidates = 0;
            IEnumerable<string> Keys(Point p)
            {
                yield return "s:" + p.Surface;
                yield return "b:" + p.Band;
                if (p.Stems != null) yield return "g:" + p.Stems;
            }
            bool Short(Point p)
            {
                foreach (string k in Keys(p))
                    if (!counts.TryGetValue(k, out int c) || c < MinPerClass) return true;
                return false;
            }
            while (candidates < cap && !(points.Count >= count && !AnyShort(counts)))
            {
                state = Mix(state);
                double x = x0 + (x1 - x0) * Unit(state);
                state = Mix(state);
                double z = z0 + (z1 - z0) * Unit(state);
                candidates++;
                bool quota = points.Count >= count;
                // Past the proportional points only a short class, band or stratum is wanted: the ground's band and class from
                // SampleGround first (cheap), the whole classification only for a candidate that could fill one.
                if (quota && !ShortBand(map, x, z, counts)) continue;
                if (!Classify(map, x, z, out Point p)) continue;
                if (quota && !Short(p)) continue;
                p.Index = points.Count;
                p.Quota = quota;
                points.Add(p);
                foreach (string k in Keys(p)) counts[k] = counts.TryGetValue(k, out int c) ? c + 1 : 1;
            }
            return points.ToArray();
        }

        // Every slope band is wanted on a map with any slope (one of a terrain), whether or not the proportional points met it.
        static bool AnyShort(Dictionary<string, int> counts)
        {
            foreach (int c in counts.Values) if (c < MinPerClass) return true;
            if (counts.ContainsKey("b:1"))
                for (int b = 0; b < Bands.Length; b++)
                    if (!counts.TryGetValue("b:" + b, out int c) || c < MinPerClass) return true;
            return false;
        }

        static bool ShortBand(MapWorld map, double x, double z, Dictionary<string, int> counts)
        {
            var g = new GroundSample[1];
            map.SampleGround(new[] { new XZ(x, z) }, g);
            if ((g[0].Flags & GroundFlags.Hole) != 0) return true;
            Double3 n = g[0].Normal;
            double slope = Math.Atan2(Math.Sqrt(n.X * n.X + n.Z * n.Z), n.Y) * 57.29577951308232;
            int band = 0;
            while (band + 1 < Bands.Length && slope >= Bands[band + 1]) band++;
            bool Want(string k) => !counts.TryGetValue(k, out int c) || c < MinPerClass;
            if (Want("b:" + band) && (band == 0 || counts.ContainsKey("b:1"))) return true;
            if (Want("s:" + map.SurfaceName(g[0].Surface))) return true;
            foreach (var kv in counts) if (kv.Key.StartsWith("g:") && kv.Value < MinPerClass) return true;
            return false;
        }

        // ---- The flight's world, drone and air ----------------------------------------------------------------------------

        /// <summary>What every flight of a sweep shares (read-only once made): the map, its FlightWorld with its hills built, the
        /// drone, the tables, the air.</summary>
        public sealed class Context
        {
            public MapWorld Map;
            public FlightWorld World;
            public DroneSpec Spec;
            public MaterialRow[] Materials;
            public ulong MaterialsHash;
            public SurfaceTable Surfaces;
            public WindLevel Level;
            public WindSetup Wind;
            public Loadout Loadout = new Loadout { Payload = 2.0, Legs = true, CoilPaidOut = 0 };
            public double Stand;

            public Context(MapWorld map, DroneSpec spec, WindLevel level)
            {
                Map = map;
                World = new FlightWorld(map);
                _ = World.Hills; // built here, before any thread flies over it
                Spec = spec;
                Materials = ContactScenarios.RepositoryMaterials(out MaterialsHash);
                Surfaces = SoilScenarios.Table();
                Level = level;
                Wind = WindSetup.ForMap(map, level);
                foreach (ContactShape s in Contacts().Shapes)
                    if (s.Role == ContactRole.Support) Stand = Math.Max(Stand, s.Radius - Math.Min(s.A.Y, s.B.Y));
            }

            public ContactSpec Contacts() => Spec.BuildContacts(Loadout, Materials, MaterialsHash, Surfaces);

            /// <summary>The player's flight but the line (FarmlandLineCase's setup without its tether).</summary>
            public FlightSetup Setup(Double3 start, double headingDeg, ulong seed)
            {
                double yaw = headingDeg / 57.29577951308232;
                return new FlightSetup
                {
                    Seed = seed, World = World, Mass = Spec.BuildMass(Loadout), Propulsion = Spec.BuildPropulsion(), Aero = Spec.BuildAero(),
                    Wind = Wind, Rain = RainLevel.Sunny, CameraPosition = Spec.CameraPosition, Contacts = Contacts(),
                    Asymmetry = Spec.BuildAsymmetry(), Placement = Spec.BuildPlacement(), VortexRing = Spec.BuildVortexRing(),
                    GroundEffect = Spec.BuildGroundEffect(), RotorWake = Spec.BuildRotorWake(), Flapping = Spec.BuildFlapping(),
                    Interference = Spec.BuildInterference(), Modes = Spec.BuildModes(),
                    StartPosition = start, StartAttitude = new Quat(0, Math.Sin(yaw / 2), 0, Math.Cos(yaw / 2)),
                };
            }
        }

        // ---- A row ----------------------------------------------------------------------------------------------------------

        public sealed class Row
        {
            public Point Point;
            public Manoeuvre Manoeuvre;
            public double Heading;
            public ulong Seed;
            public bool Skipped;
            public string Note = "";
            /// <summary>Per check: failed, its first failing step (the flight's), its worst value.</summary>
            public readonly bool[] Failed = new bool[Checks];
            public readonly bool[] Judged = new bool[Checks];
            public readonly long[] First = new long[Checks];
            public readonly double[] Worst = new double[Checks];
            public double TouchSpeed, PeakAccel, EndTilt, Slide, RestTime = double.PositiveInfinity, LiftTime = double.PositiveInfinity;
            public int Crashes, BladeTouches;
            public string Detail = "";
            /// <summary>The flight's events by kind (a forensic reader's summary; not in Line, so not in the rows' hash).</summary>
            public string Events = "";

            public Row()
            {
                for (int i = 0; i < Checks; i++) First[i] = -1;
            }

            public bool Pass
            {
                get
                {
                    foreach (bool f in Failed) if (f) return false;
                    return true;
                }
            }

            public void Fail(Check c, long step, string what = null)
            {
                Judged[(int)c] = true;
                if (Failed[(int)c]) return;
                Failed[(int)c] = true;
                First[(int)c] = step;
                if (what != null) Detail += (Detail.Length > 0 ? "; " : "") + c + ": " + what;
            }

            public void Judge(Check c) => Judged[(int)c] = true;

            public void Worse(Check c, double v)
            {
                if (v > Worst[(int)c] || double.IsNaN(v)) Worst[(int)c] = v;
            }

            /// <summary>The row as one tab-separated line, every number with its digits fixed (the same bits print the same).</summary>
            /// <summary>alloc false leaves the allocation verdict out (the rows' hash: what a step allocates is the runtime's, a
            /// thread's first use of a lazily made cache, so it is the one column a thread count may move).</summary>
            public string Line(bool alloc = true)
            {
                CultureInfo ci = CultureInfo.InvariantCulture;
                var s = new System.Text.StringBuilder();
                s.Append(Point.Index).Append('\t').Append(Manoeuvre).Append('\t').Append(Point.X.ToString("F2", ci)).Append('\t')
                    .Append(Point.Z.ToString("F2", ci)).Append('\t').Append(Point.Surface).Append('\t').Append(Point.Stems ?? "-").Append('\t')
                    .Append(Point.Slope.ToString("F2", ci)).Append('\t').Append(Heading.ToString("F1", ci)).Append('\t').Append(Seed.ToString("X16", ci));
                if (Skipped)
                    return s.Append("\tSKIPPED\t").Append(Note).ToString();
                bool pass = true;
                for (int i = 0; i < Checks; i++) pass &= !Failed[i] || !alloc && i == (int)Check.Alloc;
                s.Append('\t').Append(pass ? "PASS" : "FAIL");
                for (int i = 0; i < Checks; i++)
                    if (!alloc && i == (int)Check.Alloc) s.Append("\t*");
                    else s.Append('\t').Append(!Judged[i] ? "-" : Failed[i] ? "F@" + First[i] : "ok").Append(':').Append(Worst[i].ToString("G6", ci));
                s.Append('\t').Append(TouchSpeed.ToString("F3", ci)).Append('\t').Append(PeakAccel.ToString("F0", ci)).Append('\t')
                    .Append(EndTilt.ToString("F1", ci)).Append('\t').Append(Slide.ToString("F2", ci)).Append('\t')
                    .Append(RestTime.ToString("F3", ci)).Append('\t').Append(LiftTime.ToString("F3", ci)).Append('\t').Append(Crashes)
                    .Append('\t').Append(BladeTouches).Append('\t').Append(Note).Append('\t').Append(alloc ? Detail : Strip(Detail));
                return s.ToString();
            }

            static string Strip(string detail)
            {
                var kept = new List<string>();
                foreach (string part in detail.Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
                    if (!part.StartsWith("Alloc:", StringComparison.Ordinal)) kept.Add(part);
                return string.Join("; ", kept);
            }

            public static string Header()
            {
                var s = new System.Text.StringBuilder("point\tmanoeuvre\tx\tz\tsurface\tstems\tslope\theading\tseed\tverdict");
                for (int i = 0; i < Checks; i++) s.Append('\t').Append((Check)i);
                return s.Append("\ttouch_mps\tpeak_mps2\tend_tilt\tslide_m\trest_s\tlift_s\tcrashes\tblade_touches\tnote\tdetail").ToString();
            }
        }

        // ---- The flights ----------------------------------------------------------------------------------------------------

        /// <summary>The heading (deg) and the flight's seed for a point's manoeuvre, drawn from the sweep's seed.</summary>
        public static void Draws(ulong sweepSeed, int point, Manoeuvre m, int attempt, out double heading, out ulong seed)
        {
            ulong h = Mix(Mix(sweepSeed ^ 0x4845414455UL) ^ ((ulong)point << 8) ^ (ulong)m ^ ((ulong)attempt << 40));
            heading = 360 * Unit(h);
            seed = Mix(h);
        }

        /// <summary>
        /// One flight at a point: Land (its rows Land and Liftoff), Cut, Drop or Slide. heading: NaN to draw it (Draws), else the
        /// nose's bearing in deg. prepare changes the core before its first step (a known-wrong); a flight so changed is not
        /// replayed (replay false). Rows come back in the manoeuvre's order. observe, if given, reads the core after each step
        /// (a trace; it must not change the core).
        /// </summary>
        public static Row[] Fly(Context c, in Point p, Manoeuvre m, ulong sweepSeed, double heading = double.NaN, Action<FlightCore> prepare = null, bool replay = true,
            Action<FlightCore> observe = null)
        {
            var row = new Row { Point = p, Manoeuvre = m };
            Row lift = m == Manoeuvre.Land ? new Row { Point = p, Manoeuvre = Manoeuvre.Liftoff } : null;
            Row[] rows = lift != null ? new[] { row, lift } : new[] { row };
            MapWorld map = c.Map;
            double stand = c.Stand;
            ulong seed = 0;
            Double3 dir = default, start = default;
            bool found = false;
            for (int attempt = 0; attempt < 8 && !found; attempt++)
            {
                double drawn;
                Draws(sweepSeed, p.Index, m, attempt, out drawn, out seed);
                if (!double.IsNaN(heading)) drawn = heading;
                row.Heading = drawn;
                double yaw = drawn / 57.29577951308232;
                dir = new Double3(Math.Sin(yaw), 0, Math.Cos(yaw));
                double sx = p.X, sz = p.Z;
                if (m == Manoeuvre.Slide)
                {
                    sx -= 6 * dir.X;
                    sz -= 6 * dir.Z;
                }
                found = GroundAt(map, sx, sz, out double sh, out _, out _, out int under) && Clear(map, sx, sh, sz, under);
                start = new Double3(sx, sh + stand + 1, sz);
                if (!double.IsNaN(heading)) break;
            }
            row.Seed = seed;
            if (lift != null)
            {
                lift.Heading = row.Heading;
                lift.Seed = seed;
            }
            if (!found)
            {
                foreach (Row r in rows)
                {
                    r.Skipped = true;
                    r.Note = "the slide's start lies under or in an object at every heading drawn";
                }
                return rows;
            }

            FlightSetup setup = c.Setup(start, row.Heading, seed);
            var core = new FlightCore(setup);
            prepare?.Invoke(core);
            MemoryStream log = null;
            FlightRecorder recorder = null;
            if (replay)
            {
                log = new MemoryStream();
                LogHeader h = LogHeader.For(setup, core);
                h.Build = "flightbench-sweep";
                h.Isa = "sweep";
                h.WorldId = map.MapId;
                h.SetDrone(c.Spec, c.Loadout);
                recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
            }

            var judge = new Judge(c, core, setup.StartPosition);
            var pilot = new SimPilot(core.Controller, start) { Heading = dir };
            var ground = new GroundSample[1];
            var at = new XZ[1];
            double GroundUnder(Double3 x)
            {
                if (GroundAt(map, x.X, x.Z, out double gh, out _, out _)) return gh;
                at[0] = new XZ(x.X, x.Z);
                map.SampleGround(at, ground);
                return ground[0].Height;
            }

            StickSample held = default;
            bool holding = false;
            long touch = -1, restAt = -1, liftStart = -1, liftAt = -1, quiet = 0;
            Double3 touchedAt = default, lastV = core.State.Velocity, restPos = default;
            Quat restAtt = default;
            double groundAtFoot = p.Height;
            const int hover = 3000;
            int steps = 0;
            bool liftJudged = false;
            SimPilot lifter = null;
            long end = hover + 20000;
            Exception thrown = null;
            try
            {
            for (int k = 0; k < end; k++)
            {
                ref readonly FlightState s = ref core.State;
                double t = (k - hover) * FlightCore.Dt;
                StickSample stick;
                bool lifting = liftStart >= 0;
                if (k < 16) stick = new StickSample { HostUs = s.Step * FlightCore.StepUs, Arm = k >= 8 };
                else if (lifting)
                {
                    stick = lifter.Sticks(core, s.Step * FlightCore.StepUs);
                }
                else if (k < hover) stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                else
                {
                    switch (m)
                    {
                        case Manoeuvre.Land:
                        case Manoeuvre.Cut:
                        {
                            double rate = m == Manoeuvre.Land ? 0.3 : 0.5;
                            if (!holding)
                            {
                                pilot.Target = new Double3(p.X, start.Y - rate * t, p.Z);
                                stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                                if (m == Manoeuvre.Cut && s.Position.Y - groundAtFoot - stand < 0.1 && s.Velocity.Y < 0)
                                {
                                    holding = true;
                                    held = stick;
                                    held.Roll = held.Pitch = held.Yaw = 0;
                                    stick = held;
                                }
                                if (m == Manoeuvre.Land && touch >= 0)
                                {
                                    holding = true;
                                    held = stick;
                                    held.Roll = held.Pitch = held.Yaw = 0;
                                    stick = held;
                                }
                            }
                            else stick = held;
                            if (touch >= 0)
                                stick.Throttle = m == Manoeuvre.Cut ? 0 : Math.Max(0, held.Throttle * (1 - (s.Step - touch) * FlightCore.Dt / 0.5));
                            break;
                        }
                        case Manoeuvre.Drop:
                            stick = new StickSample { Arm = true };
                            break;
                        default: // Slide
                        {
                            if (!holding)
                            {
                                pilot.HoldVelocity = true;
                                pilot.TargetVelocity = dir * 2;
                                double over = t < 1.5 ? 1 : Math.Max(0, 1 - 0.5 * (t - 1.5));
                                pilot.Target = new Double3(s.Position.X, GroundUnder(s.Position) + stand + over, s.Position.Z);
                                stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                                if (t > 1.5 && s.Position.Y - GroundUnder(s.Position) - stand < 0.1 && s.Velocity.Y < 0)
                                {
                                    holding = true;
                                    held = stick;
                                    held.Roll = held.Pitch = held.Yaw = 0;
                                    stick = held;
                                }
                            }
                            else stick = held;
                            if (touch >= 0) stick.Throttle = 0;
                            break;
                        }
                    }
                }
                stick.HostUs = s.Step * FlightCore.StepUs;
                Row r = lifting ? lift : row;
                judge.Step(stick, r, k >= 1000, recorder);
                observe?.Invoke(core);
                steps++;
                ref readonly FlightState now = ref core.State;
                if (touch < 0 && k >= hover && core.Trace.ContactsTouching > 0)
                {
                    touch = now.Step;
                    row.TouchSpeed = -now.Velocity.Y;
                    touchedAt = now.Position;
                }
                if (touch >= 0 && !lifting)
                {
                    double acc = (now.Velocity - lastV).Length() / FlightCore.Dt;
                    row.PeakAccel = Math.Max(row.PeakAccel, acc);
                    if (m == Manoeuvre.Land || m == Manoeuvre.Cut)
                    {
                        row.Judge(Check.Peak);
                        row.Worse(Check.Peak, acc);
                        if (!(acc < PeakBound)) row.Fail(Check.Peak, now.Step, $"{acc:F0} m/s² at {(now.Step - touch)} ms after the touch");
                    }
                    // At rest: over the hold the body goes nowhere, its mean speed and turn rate since the hold began under RestSpeed and
                    // RestRate (3.0b). The step's own speeds read an armed drone's idle vibration as motion: range #131 Drop, on its back
                    // with two props at idle, held its place to 1 mm and its attitude to 0.2° while its rate swung ±0.16 rad/s at ~15 Hz.
                    if (quiet == 0)
                    {
                        restPos = now.Position;
                        restAtt = now.Attitude;
                    }
                    double hold = RestHold * FlightCore.Dt, cosHalf = Math.Abs(restAtt.W * now.Attitude.W + restAtt.X * now.Attitude.X
                        + restAtt.Y * now.Attitude.Y + restAtt.Z * now.Attitude.Z);
                    bool still = (now.Position - restPos).Length() < RestSpeed * hold && 2 * Math.Acos(Math.Min(1, cosHalf)) < RestRate * hold;
                    quiet = still ? quiet + 1 : 0;
                    if (restAt < 0 && quiet >= RestHold) restAt = now.Step;
                    bool late = now.Step - touch >= (long)(RestSeconds / FlightCore.Dt);
                    if (restAt >= 0 || late)
                    {
                        row.Judge(Check.Rest);
                        if (restAt < 0)
                            row.Fail(Check.Rest, now.Step, $"still moving {RestSeconds:F0} s after the touch ({now.Velocity.Length():F3} m/s, {now.BodyRate.Length():F3} rad/s)");
                        row.RestTime = restAt >= 0 ? (restAt - RestHold - touch) * FlightCore.Dt : double.PositiveInfinity;
                        row.EndTilt = Tilt(now);
                        row.Slide = (now.Position - touchedAt).Length();
                        if (lift == null) break;
                        // The liftoff: a fresh pilot climbs 1.5 m over where it stands.
                        liftStart = now.Step;
                        lifter = new SimPilot(core.Controller, now.Position + new Double3(0, 1.5, 0)) { Heading = dir };
                        end = k + 1 + (long)((LiftSeconds + 1) / FlightCore.Dt);
                        liftJudged = row.EndTilt < 45;
                        if (!liftJudged) lift.Note = $"not judged: the landing ended {row.EndTilt:F0}° from upright";
                    }
                }
                if (lifting && liftAt < 0 && core.Trace.ContactsTouching == 0 && now.Position.Y > touchedAt.Y + 0.3)
                {
                    liftAt = now.Step;
                    lift.LiftTime = (liftAt - liftStart) * FlightCore.Dt;
                }
                lastV = now.Velocity;
            }
            }
            catch (Exception e)
            {
                thrown = e;
            }
            if (thrown != null)
            {
                // The core threw: the worst failure a step has. The row being flown takes it; nothing after it is judged.
                var frame = new System.Diagnostics.StackTrace(thrown, true).GetFrame(0);
                (liftStart >= 0 ? lift : row).Fail(Check.Throws, core.State.Step, $"{thrown.GetType().Name} in {frame?.GetMethod()?.DeclaringType?.Name}.{frame?.GetMethod()?.Name}: {thrown.Message}");
                core.Wind?.Dispose();
                recorder?.Close();
                return rows;
            }
            if (touch < 0)
            {
                row.Judge(Check.Rest);
                row.Fail(Check.Rest, core.State.Step, "never touched the ground");
            }
            if (lift != null)
            {
                if (liftStart < 0)
                {
                    lift.Skipped = true;
                    lift.Note = "no liftoff: the landing never ended";
                }
                else if (liftJudged)
                {
                    lift.Judge(Check.Lifts);
                    if (liftAt < 0) lift.Fail(Check.Lifts, core.State.Step, $"still on the ground {LiftSeconds:F0} s after the climb's command");
                }
            }
            row.Crashes = judge.CrashesBefore(liftStart);
            if (lift != null) lift.Crashes = judge.Crashes - row.Crashes;
            foreach (Row r in rows)
            {
                r.BladeTouches = judge.BladeTouches;
                r.Events = judge.Kinds();
            }
            core.Wind?.Dispose();

            if (recorder != null)
            {
                recorder.Close();
                ReplayResult result = FlightReplay.Run(new MemoryStream(log.ToArray()), id => id == map.MapId ? c.World : null, c.Spec.ContentHash, null,
                    c.Surfaces.Hash, materialsHash: c.MaterialsHash);
                foreach (Row r in rows)
                {
                    if (r.Skipped) continue;
                    r.Judge(Check.Replay);
                    if (result.Outcome != ReplayOutcome.Match || result.Steps != steps)
                        r.Fail(Check.Replay, result.Step, $"{result.Outcome} after {result.Steps} of {steps} steps: {result.Message}");
                }
            }
            return rows;
        }

        static double Tilt(in FlightState s)
        {
            double up = s.Attitude.Rotate(new Double3(0, 1, 0)).Y;
            return Math.Acos(Math.Max(-1, Math.Min(1, up))) * 57.29577951308232;
        }

        // ---- The pairs' points of application ------------------------------------------------------------------------------

        /// <summary>
        /// Where each touching pair's force acts (ContactModel.Forces' own points): a support's or skid's pair (its shape's own and
        /// its ground pair) at its contact point less its anchor's stretch when the legs give sideways (Final), a soil corner's foot
        /// likewise (SoilFinal), every other part's at its contact point (Apply). Read by reflection: the core keeps its pairs
        /// private and a test may not add a hook to it (3.0b). A disc's pairs carry no force here (PropStrikes' passes).
        /// </summary>
        public static class PairReach
        {
            const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            static readonly System.Reflection.FieldInfo PairsF = typeof(ContactModel).GetField("_pairs", Private),
                CountF = typeof(ContactModel).GetField("_count", Private), GroundF = typeof(ContactModel).GetField("_ground", Private),
                SoilF = typeof(ContactModel).GetField("_soilPoint", Private);
            static readonly Type PairT = PairsF.FieldType.GetElementType(), SoilT = SoilF.FieldType.GetElementType();
            static readonly System.Reflection.FieldInfo Touching = PairT.GetField("Touching"), Suppressed = PairT.GetField("Suppressed"),
                PointF = PairT.GetField("Point"), StretchF = PairT.GetField("Stretch"), SoilOn = SoilT.GetField("On"), SoilFoot = SoilT.GetField("Foot");

            /// <summary>The farthest point of application from com (world), m, and its shape (−1 for none).</summary>
            public static double Farthest(ContactModel model, in FlightState s, Double3 com, out int shape)
            {
                var pairs = (Array)PairsF.GetValue(model);
                var count = (int[])CountF.GetValue(model);
                var ground = (Array)GroundF.GetValue(model);
                var soil = (Array)SoilF.GetValue(model);
                bool series = model.Spec.LegSideStiffness > 0;
                double far = 0;
                shape = -1;
                for (int i = 0; i < model.ShapeCount; i++)
                {
                    ContactRole role = model.Shape(i).Role;
                    if (model.ShapeOff(i) || role == ContactRole.Disc) continue;
                    bool leg = series && (role == ContactRole.Support || role == ContactRole.Skid);
                    if (role == ContactRole.Support) Take(ground.GetValue(i), leg, i, com, ref far, ref shape);
                    for (int k = 0; k < count[i]; k++) Take(pairs.GetValue(i * ContactModel.PairsPerShape + k), leg, i, com, ref far, ref shape);
                }
                for (int c = 0; c < soil.Length; c++)
                {
                    object sp = soil.GetValue(c);
                    if (!(bool)SoilOn.GetValue(sp)) continue;
                    object foot = SoilFoot.GetValue(sp);
                    double d = ((Double3)PointF.GetValue(foot) - (Double3)StretchF.GetValue(foot) - com).Length();
                    if (d > far)
                    {
                        far = d;
                        shape = FirstOfCorner(model, c);
                    }
                }
                return far;
            }

            static readonly System.Reflection.FieldInfo ImpulseF = PairT.GetField("Impulse"), NormalF = PairT.GetField("N"),
                ObjectF = PairT.GetField("Object"), ShapeF = PairT.GetField("Shape");

            /// <summary>
            /// Each frame part's (ContactRole.Body) penalty overlap at its own force this step, m, into backed (by shape): the largest
            /// of its touching pairs' normal force over the part's stiffness along that pair's normal (ContactModel.Forces: 1/(u²/k_up
            /// + (1 − u²)/k), u the normal's body-up share). The force is the pair's impulse's growth over the step (Apply adds its
            /// normal force·dt; a pair made in the step before starts from 0), so a pair that carries nothing backs nothing. last and
            /// now hold the impulses by (shape, object, object's shape) across steps.
            /// </summary>
            public static void Backed(ContactModel model, in FlightState s, Dictionary<(int, int, int), double> last, Dictionary<(int, int, int), double> now,
                double[] backed)
            {
                var pairs = (Array)PairsF.GetValue(model);
                var count = (int[])CountF.GetValue(model);
                now.Clear();
                for (int i = 0; i < model.ShapeCount; i++)
                {
                    backed[i] = 0;
                    ContactShape sh = model.Shape(i);
                    if (model.ShapeOff(i) || sh.Role != ContactRole.Body) continue;
                    for (int k = 0; k < count[i]; k++)
                    {
                        object p = pairs.GetValue(i * ContactModel.PairsPerShape + k);
                        if (!(bool)Touching.GetValue(p) || (bool)Suppressed.GetValue(p)) continue;
                        var key = (i, (int)ObjectF.GetValue(p), (int)ShapeF.GetValue(p));
                        double impulse = (double)ImpulseF.GetValue(p);
                        now[key] = impulse;
                        double fn = (impulse - (last.TryGetValue(key, out double was) && was <= impulse ? was : 0)) / FlightCore.Dt;
                        Double3 nb = s.Attitude.InverseRotate((Double3)NormalF.GetValue(p));
                        double up = nb.Y * nb.Y, ks = 1 / (up / sh.StiffnessUp + (1 - up) / sh.Stiffness);
                        backed[i] = Math.Max(backed[i], fn / ks);
                    }
                }
            }

            static void Take(object p, bool leg, int i, Double3 com, ref double far, ref int shape)
            {
                if (!(bool)Touching.GetValue(p) || (bool)Suppressed.GetValue(p)) return;
                Double3 at = (Double3)PointF.GetValue(p);
                if (leg) at = at - (Double3)StretchF.GetValue(p);
                double d = (at - com).Length();
                if (d > far)
                {
                    far = d;
                    shape = i;
                }
            }

            static int FirstOfCorner(ContactModel model, int c)
            {
                for (int i = 0; i < model.ShapeCount; i++)
                    if (model.Shape(i).Corner == c && model.Shape(i).Role == ContactRole.Skid) return i;
                return 0;
            }
        }

        // ---- The per-step judge -------------------------------------------------------------------------------------------

        /// <summary>Steps a core and judges every step against the invariants it can see in a step (all but Rest, Lifts, Replay).</summary>
        sealed class Judge
        {
            readonly Context _c;
            readonly FlightCore _core;
            readonly Double3 _start;
            readonly StaticContact[] _found = new StaticContact[8];
            readonly GroundSample[] _g = new GroundSample[1];
            readonly XZ[] _at = new XZ[1];
            double _energy; // the contacts' net return since the start, J (ΔKE less the other forces' work)
            double[] _backed; // each frame part's penalty overlap at its force this step (PairReach.Backed)
            readonly CornerState[] _cornerBefore = new CornerState[4]; // each corner's state at the step's start
            readonly double[] _groove = new double[4], _grooveSink = new double[4]; // each corner's sink to judge, its groove's depth, m
            readonly Double3[] _grooveAt = new Double3[4]; // where each corner's skid lay when its soil last held it, world
            Dictionary<(int, int, int), double> _impulses = new Dictionary<(int, int, int), double>(), _impulsesNow = new Dictionary<(int, int, int), double>();
            public int Crashes, BladeTouches;
            readonly List<long> _crashSteps = new List<long>();
            readonly int[] _kinds = new int[64];
            readonly long[] _firstOf = new long[64];

            public string Kinds()
            {
                var s = new System.Text.StringBuilder();
                for (int i = 0; i < _kinds.Length; i++)
                    if (_kinds[i] > 0) s.Append(s.Length > 0 ? ", " : "").Append((EventKind)i).Append(' ').Append(_kinds[i]).Append(" (first @").Append(_firstOf[i]).Append(')');
                return s.ToString();
            }

            public Judge(Context c, FlightCore core, Double3 start)
            {
                _c = c;
                _core = core;
                _start = start;
            }

            public int CrashesBefore(long step)
            {
                if (step < 0) return Crashes;
                int n = 0;
                foreach (long s in _crashSteps) if (s < step) n++;
                return n;
            }

            static double Kinetic(in FlightState s, in MassProperties b)
            {
                Double3 v = ComVelocity(s, b);
                return 0.5 * b.Mass * Double3.Dot(v, v) + 0.5 * Double3.Dot(s.BodyRate, b.Inertia * s.BodyRate);
            }

            static Double3 ComVelocity(in FlightState s, in MassProperties b) => s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, b.CenterOfMass));

            public void Step(in StickSample stick, Row r, bool counted, FlightRecorder recorder)
            {
                FlightCore core = _core;
                FlightState s0 = core.State;
                MassProperties b0 = core.Body;
                for (int c = 0; c < 4; c++) _cornerBefore[c] = core.Contacts.Corner(c);
                long before = AllocProbe.Before();
                core.Advance(stick);
                long allocated = AllocProbe.After(before);
                recorder?.RecordStep(core.State, stick, core.Trace, core.Events);
                ref readonly FlightState s = ref core.State;
                ref readonly StepTrace tr = ref core.Trace;
                long step = s.Step;

                // Allocation (after the first second).
                if (counted)
                {
                    r.Judge(Check.Alloc);
                    r.Worse(Check.Alloc, allocated);
                    if (allocated != 0) r.Fail(Check.Alloc, step, $"{allocated} B in a step" + AllocProbe.Report());
                }

                // Finite and bounded.
                r.Judge(Check.Finite);
                double v = s.Velocity.Length(), w = s.BodyRate.Length(), away = (s.Position - _start).Length();
                bool finite = Finite(s.Position) && Finite(s.Velocity) && Finite(s.BodyRate) && double.IsFinite(s.Attitude.W) && double.IsFinite(s.Attitude.X)
                    && double.IsFinite(s.Attitude.Y) && double.IsFinite(s.Attitude.Z) && Finite(tr.NetForce) && Finite(tr.NetMoment) && Finite(tr.ContactForce)
                    && Finite(tr.ContactMoment);
                for (int i = 0; i < 4; i++) finite &= double.IsFinite(s.RotorSpeed[i]);
                r.Worse(Check.Finite, v);
                if (!finite || v > MaxSpeed || w > MaxRate || away > MaxRange)
                    r.Fail(Check.Finite, step, finite ? $"|v| {v:F1} m/s, |ω| {w:F1} rad/s, {away:F0} m from the start" : "a NaN or ∞ in the state or the trace");

                // The lever, pair by pair (3.0b): the spec's |M| ≤ Reach·Σ|F_i| holds whenever every pair's force acts within Reach of
                // the CoM (|r_i × F_i| ≤ |r_i|·|F_i|), so each touching pair's own point of application is judged: its contact point, a
                // leg point's less its anchor's stretch (ContactModel.Final, SoilFinal: the foot where the ground holds it). |M|/|ΣF|
                // was not that: two contacts pushing against each other make a couple with almost no net force (range #180 Slide,
                // 1.42 N·m on 2.36 N: its pairs' normals sum to 20.8 N, every point within 0.38 m). The core has no per-pair reading,
                // so PairReach reads its pairs by reflection, outside the step's allocation probe.
                double cf = tr.ContactForce.Length(), cm = tr.ContactMoment.Length();
                if (cf > 0 || cm > 0)
                {
                    r.Judge(Check.Lever);
                    double reach = PairReach.Farthest(core.Contacts, s, s.Position + s.Attitude.Rotate(core.Body.CenterOfMass), out int part);
                    r.Worse(Check.Lever, reach);
                    if (reach > Reach)
                        r.Fail(Check.Lever, step, $"{core.Contacts.Shape(part).Name}'s force {reach:F3} m from the CoM ({cm:F2} N·m on {cf:F2} N)");
                }

                // The energy: the body's kinetic energy since the start less the work of every force but the contacts' (rotors, air,
                // gravity), summed. RigidBody's kick makes the CoM's ΔKE exactly F·v̄·dt and the turn's ΔKE exactly
                // M·(ω0 + ½I⁻¹M·dt)·dt, so what is left is the contacts' work plus anything the step adds outside the integration but
                // the blades' passes. Two terms the step really has besides (3.0b, judged by hand on range #405 Slide):
                // - The blades' passes (PropStrikes.Apply, after the rigid body: stems, strikes, jams) kick the velocity and the rate
                //   with the rotors' own spin: the rotors' work, never the contacts'. The step is re-integrated from its start with the
                //   trace's net force and moment (RigidBody.Step, the core's own), and the passes' share is the kinetic energy past that,
                //   credited up to what the step's strikes took from the rotors (BladeStrikes' energy): no more than the rotors gave,
                //   and nothing in a step without a strike.
                // - Gravity's work is −m·g·v̄_y·dt, the same v̄ as the kick's; the potential m·g·Δy moves by m·g·v1_y·dt (the drift's
                //   new velocity), ½·m·g·dt·Δv_y apart, which sums to ½·m·g·dt times the vertical speed's change since the start
                //   (0.03 J at 1 m/s on 6 kg): the integrator's, not a contact's.
                // A step whose mass changes (a piece let go) is left out of the sum.
                MassProperties b1 = core.Body;
                if (b1.Mass == b0.Mass)
                {
                    FlightState integrated = s0;
                    RigidBody.Step(ref integrated, b0, tr.NetForce * b0.InverseMass, tr.NetMoment, FlightCore.Dt);
                    Double3 vbar = (ComVelocity(s0, b0) + ComVelocity(integrated, b0)) * 0.5;
                    Double3 wbar = s0.BodyRate + (b0.InverseInertia * tr.NetMoment) * (0.5 * FlightCore.Dt);
                    Double3 fOther = tr.NetForce - tr.ContactForce;
                    Double3 mOther = tr.NetMoment - tr.ContactMoment;
                    double work = (Double3.Dot(fOther, vbar) + Double3.Dot(mOther, wbar)) * FlightCore.Dt;
                    double taken = 0;
                    for (int i = 0; i < BladeStrikes.Slots; i++) taken += s.BladeStrikes[i].Energy;
                    double passes = Math.Min(Kinetic(s, b1) - Kinetic(integrated, b0), taken);
                    _energy += Kinetic(s, b1) - Kinetic(s0, b0) - work - passes;
                    r.Judge(Check.Energy);
                    r.Worse(Check.Energy, _energy);
                    if (_energy > EnergyTolerance) r.Fail(Check.Energy, step, $"{_energy:F3} J more than the contacts took");
                }

                // Events: twice in a step, refused, tunnels and world bugs, crashes, blades on the ground. Twice is the same kind, part,
                // index, object, shape and material: a blade through a crown's shell in one step cuts a twig (twig) and strikes the
                // step's leaves (foliage) on the same shell, two strikes (w9-village-physics 2.3 §4; 2.6's addendum, Village #283, #474).
                // Each corner's soil sink to judge its skid by (Ground and Tunnel): the soil's own while it holds the corner; after it
                // lets go, the last held sink while the half-skid's middle lies within its half length of where it was held.
                for (int c = 0; c < 4; c++)
                {
                    int skid = -1;
                    for (int i = 0; i < core.Contacts.ShapeCount && skid < 0; i++)
                        if (core.Contacts.Shape(i).Role == ContactRole.Skid && core.Contacts.Shape(i).Corner == c) skid = i;
                    if (skid < 0)
                    {
                        _groove[c] = 0;
                        continue;
                    }
                    ContactShape k = core.Contacts.Shape(skid);
                    Double3 middle = s.Position + s.Attitude.Rotate((k.A + k.B) * 0.5);
                    ContactModel.SkidSoil soil = core.Contacts.SoilOf(c);
                    if (soil.Engaged)
                    {
                        _grooveSink[c] = Math.Max(0, Math.Max(soil.Sink0, Math.Max(soil.Sink1, soil.Sink2)));
                        _grooveAt[c] = middle;
                        _groove[c] = _grooveSink[c];
                    }
                    else _groove[c] = (middle - _grooveAt[c]).Length() < 0.5 * (k.B - k.A).Length() ? _grooveSink[c] : 0;
                }

                // The frame parts' backed overlaps (Ground and Tunnel), every step, so each pair's impulse has its step before.
                if (_backed == null || _backed.Length != core.Contacts.ShapeCount) _backed = new double[core.Contacts.ShapeCount];
                PairReach.Backed(core.Contacts, s, _impulses, _impulsesNow, _backed);
                (_impulses, _impulsesNow) = (_impulsesNow, _impulses);

                StepEvents ev = core.Events;
                r.Judge(Check.EventsOnce);
                r.Judge(Check.EventsKept);
                r.Judge(Check.Tunnel);
                for (int i = 0; i < ev.Count; i++)
                {
                    ref readonly FlightEvent e = ref ev[i];
                    for (int j = 0; j < i; j++)
                    {
                        ref readonly FlightEvent o = ref ev[j];
                        if (o.Kind == e.Kind && o.Part == e.Part && o.Index == e.Index && o.Object == e.Object && o.Shape == e.Shape && o.Material == e.Material)
                        {
                            r.Fail(Check.EventsOnce, step, $"{e.Kind} twice on part {e.Part}, index {e.Index}, object {e.Object} shape {e.Shape} material {e.Material}");
                            break;
                        }
                    }
                    if ((int)e.Kind < _kinds.Length && _kinds[(int)e.Kind]++ == 0) _firstOf[(int)e.Kind] = step;
                    // A tunnel on the ground or a soil shape (a surface: soil gives) is judged by the soil's own depth law (3.0b): the
                    // core's rule takes a surface's row as rigid (ContactModel's surface rows), so a part dropped from a soil box at a
                    // depth its soil's sink and its own give hold (range #425 Slide: support_fl_end 15.0 mm in E01's box, its corner's
                    // soil sunk 12.5 mm, carried by the ground's pair beside it) is no passage. Past that allowance by GroundTolerance
                    // it is; on anything rigid every Tunnel and WorldBug is.
                    if (e.Kind == EventKind.Tunnel && e.Surface != 0 && e.Index < core.Contacts.ShapeCount)
                    {
                        double past = ThroughSoil(e.A, Give(core.Contacts, tr, e.Index, out double sink), sink);
                        r.Worse(Check.Tunnel, past);
                        if (past > GroundTolerance)
                            r.Fail(Check.Tunnel, step, $"Tunnel by {core.Contacts.Shape(e.Index).Name} {e.A * 1e3:F1} mm into the soil, {past * 1e3:F1} mm past its sink and its give");
                    }
                    else if (e.Kind == EventKind.Tunnel || e.Kind == EventKind.WorldBug)
                        r.Fail(Check.Tunnel, step, $"{e.Kind} by part {e.Part} at {e.A:F4}");
                    if (e.Kind == EventKind.EventsLost) r.Fail(Check.EventsKept, step, "an EventsLost event");
                    if (e.Kind == EventKind.Crash)
                    {
                        Crashes++;
                        _crashSteps.Add(step);
                    }
                    if (e.Kind == EventKind.PropStrike || e.Kind == EventKind.BladeStrike) BladeTouches++;
                }
                if (ev.Lost > 0) r.Fail(Check.EventsKept, step, $"{ev.Lost} events refused");

                // The ground: every support, skid and frame part's depth past the ground's or a soil shape's surface, near the ground.
                _at[0] = new XZ(s.Position.X, s.Position.Z);
                _c.Map.SampleGround(_at, _g);
                if (_g[0].Flags == GroundFlags.Hole || !(s.Position.Y - _g[0].Height > 1.5))
                {
                    r.Judge(Check.Ground);
                    ContactModel cmodel = core.Contacts;
                    double excess = 0, depthThen = 0, sinkThen = 0, giveThen = 0;
                    string stateThen = null;
                    string by = null;
                    for (int i = 0; i < cmodel.ShapeCount; i++)
                    {
                        ContactShape sh = cmodel.Shape(i);
                        if (cmodel.ShapeOff(i) || sh.Role != ContactRole.Support && sh.Role != ContactRole.Skid && sh.Role != ContactRole.Body) continue;
                        // A leg's support or skid may lie as deep as its corner's soil says its nodes sank this step (the soil model's own
                        // depth); every part GroundTolerance past that.
                        // A leg's support or skid may also lie as deep as its leg's own give this step: its compression along the
                        // tube and its lean across it on its lever (the trace's), which the rigid body's geometry shows as overlap
                        // while the modelled foot stays on the ground. Not a half whose leg is gone (Hanging, Stub, TubeGone, 3.0b):
                        // the trace's compression there is the half's own deepest overlap, which excused any depth by itself, and the
                        // half's give, its cantilever's turn, is already in its shapes' placement (ContactModel's hanging chain: its
                        // shapes stand out of what they touch by the contact spring's share), so it may lie only GroundTolerance in.
                        // A frame part may lie as deep as its own penalty spring's overlap at the force its pairs carry this step
                        // (PairReach.Backed, 3.0b): on the ground and soil a frame part's row is rigid, so that overlap is the whole of
                        // the contact's give (range #430 Drop: motor_fr 22.8 mm in E01's floor carrying 2.1 kN, 26 mm at 2.2 kN). A
                        // part deep with nothing pushing it out backs nothing.
                        double give = Give(cmodel, tr, i, out double sink);
                        var cap = new Capsule(s.Position + s.Attitude.Rotate(sh.A), s.Position + s.Attitude.Rotate(sh.B), sh.Radius);
                        int k = Math.Min(_c.Map.StaticContacts(cap, cap, 0, _found, QueryFilter.NoWires), _found.Length);
                        for (int j = 0; j < k; j++)
                            if (_found[j].Surface != 0 && -_found[j].Distance - sink - give > excess)
                            {
                                excess = -_found[j].Distance - sink - give;
                                depthThen = -_found[j].Distance;
                                sinkThen = sink;
                                giveThen = give;
                                stateThen = sh.Role != ContactRole.Body && sh.Corner >= 0 && sh.Corner < 4 ? cmodel.Corner(sh.Corner).ToString() : "frame";
                                by = sh.Name;
                            }
                    }
                    r.Worse(Check.Ground, excess);
                    if (excess > GroundTolerance)
                        r.Fail(Check.Ground, step, $"{by} {depthThen * 1e3:F1} mm past the ground (its soil's sink {sinkThen * 1e3:F1} mm, its {(stateThen == "frame" ? "spring's overlap at its force" : "leg's give")} {giveThen * 1e3:F1} mm, the corner {stateThen}) at {v:F2} m/s");
                }
            }

            /// <summary>How far a part dropped from the soil at depth (m) lay past what its soil's sink and its own give hold, m: a
            /// passage when more than GroundTolerance (the Tunnel check on a surface).</summary>
            public static double ThroughSoil(double depth, double give, double sink) => depth - sink - give;

            // A part's own give this step beyond its corner's soil sink (sink), m: Ground's allowance less GroundTolerance.
            double Give(ContactModel cmodel, in StepTrace tr, int i, out double sink)
            {
                ContactShape sh = cmodel.Shape(i);
                sink = 0;
                if (sh.Role == ContactRole.Body)
                {
                    // The part's, over its shapes: a capsule and the spheres at its ends are one part, and the force goes to whichever
                    // the model pairs (range #436 Drop: the payload capsule 20.4 mm in E01's floor, its aft sphere carrying 2.2 kN).
                    // Never past its own radius: a penalty overlap stands for the surface's give under the part, and a part whose core
                    // has passed the surface is through it at any force (a motor can 18.6 mm in radius took 26 mm at 2.2 kN).
                    double part = 0;
                    for (int j = 0; j < cmodel.ShapeCount; j++)
                        if (cmodel.Shape(j).Part == sh.Part && cmodel.Shape(j).Role == ContactRole.Body) part = Math.Max(part, _backed[j]);
                    return Math.Min(part, sh.Radius);
                }
                if (sh.Corner < 0 || sh.Corner >= 4) return 0;
                // Its soil's sink, or, where the soil has let it go, the groove it pressed while the corner's skid still lies over it:
                // the soil there is displaced, and the box's geometry does not know it (range #446 Slide, step 6581: skid_rr 22.9 mm
                // in E01's box with no force, its corner's soil, sunk 20.9 mm and 23.4 mm deepest, let go the step before).
                sink = _groove[sh.Corner];
                // The leg's state the step's forces were made in (its start's): in the step a leg breaks the trace still holds the
                // give it carried its load with (range #436 Cut, step 5330: 35.7 mm along, 52.3 mm across at 30 N, then broken).
                if ((_cornerBefore[sh.Corner] & (CornerState.Hanging | CornerState.Stub | CornerState.TubeGone)) != 0) return 0;
                return Math.Abs(tr.LegCompression[sh.Corner]) + Math.Abs(tr.LegLean[sh.Corner]);
            }

            static bool Finite(Double3 x) => double.IsFinite(x.X) && double.IsFinite(x.Y) && double.IsFinite(x.Z);
        }
    }
}
