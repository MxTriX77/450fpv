// MapWorld's conductors for physics (p7d-tether task 1.2, design 9 and 12; WLD-11): WiresNear, the placed ports, each
// conductor's ends and neighbours, and the envelope index. The query's rules are IWorldQuery's (Wires left to physics).
// - The records: a variant's wires records give, beyond its chain (MapWorldLoad.cs), mass_kg_per_m, axial_stiffness_n
//   and breaking_force_n, each above 0, and bending_stiffness_n_m2 where given, above 0 (p7d-tether 1.4 writes it; 0
//   until then). The edge radius is the edge_radius_m of the variant's own edge snag record along the very same points
//   (the pole-wires reading N-1: a wire's edge snag is written from its record's points), else half its diameter.
// - The ports: a variant's ports records (position, direction), placed at their row's pose as the shapes' axes are
//   (world = position + R·local, the direction R·local). A port of a houses-class row is a house anchor, any other a
//   pole's hook.
// - The ends, at load: each conductor's first point (A) and last point (B) hangs from the placed port whose seat,
//   position + radius · direction, lies nearest within PortTolerance (the first in canonical order, object then port,
//   on a tie), whatever object either belongs to. An end with none is named in WorldBugs and reads FittingKind.None.
//   Its neighbour is the first other conductor in canonical order with an end on the same port.
// - The envelope: the chain inflated by its radius and its blow-out at 45°, 2·d_max·sin 22.5° (IWorldQuery), d_max the
//   chain's farthest point from its chord, measured at load.
// - The index: the map's 2 m grid (MapWorld.cs) listing each conductor by its envelope's bounds. A query marks the
//   conductors listed in the cells its sphere's bounds meet in a bit set on the stack (one in many cells counts once)
//   and reads it in index order, which is canonical order: the conductors are the wire prims in prim order (object,
//   then shape). Each is tested by its envelope's bounds, then by the squared distance from the centre to its chain's
//   candidate segments (ShapeMath.WireRange, grown by the inflation) against (radius + inflation)². + − × ÷ only; the
//   result is a copy of the conductor's record, made at load, its chain a read-only view of the world's points.
// Load time allocates and throws FormatException naming the file; queries are allocation-free and reentrant.

using System;
using System.Collections.Generic;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld
    {
        /// <summary>A conductor's end hangs from a port whose seat lies this close, m (the pole-wires reading §1: 5 mm keeps it in the 7 mm groove).</summary>
        public const double PortTolerance = 0.005;

        /// <summary>sin 22.5°: a point d from the chord, the curve turned 45° about it, moves 2·d·sin 22.5°.</summary>
        private const double SinHalfBlowOut = 0.3826834323650898;

        /// <summary>The most conductors a map may hold: a query's bit set of them lives on the stack (8 KB).</summary>
        private const int MaxConductors = 1 << 16;

        /// <summary>A variant's wire records' fields beyond the chain, and its ports, in its own frame.</summary>
        private sealed class Lines
        {
            public double[] Diameter, Mass, Axial, Breaking, Bending, Edge;
            public Double3[] PortPosition, PortDirection;
        }

        // Set at load only.
        private Conductor[] _conductors;    // every placed wire in prim order: its record, chain and ends
        private int[] _conductorPrim;       // its prim
        private double[] _conductorReach;   // its radius plus its blow-out: how far its envelope reaches past its chain, m
        private Bounds[] _conductorBox;     // its envelope's bounds
        private int[] _conductorCellStart, _conductorCellItems; // per 2 m cell: the conductors whose envelope bounds meet it

        /// <summary>
        /// The world bugs found at load, each naming its wire: a conductor's end with no port within PortTolerance of its
        /// seat (p7d-tether design 12). Empty for a world without one.
        /// </summary>
        public string[] WorldBugs { get; }

        // ---- WiresNear ---------------------------------------------------------------------------------------------

        public int WiresNear(Double3 centre, double radius, Span<Conductor> results)
        {
            if (_conductors.Length == 0)
                return 0;
            Bounds reach = Bounds.Of(centre, centre, radius);
            Span<ulong> marks = stackalloc ulong[(_conductors.Length + 63) >> 6];
            marks.Clear();
            CellRange(reach, out int x0, out int x1, out int z0, out int z1);
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cz * _nx + cx;
                    for (int k = _conductorCellStart[cell]; k < _conductorCellStart[cell + 1]; k++)
                    {
                        int c = _conductorCellItems[k];
                        marks[c >> 6] |= 1UL << (c & 63);
                    }
                }
            }
            int count = 0;
            for (int w = 0; w < marks.Length; w++)
            {
                ulong bits = marks[w];
                while (bits != 0)
                {
                    ulong low = bits & (0UL - bits);
                    bits ^= low;
                    int c = (w << 6) | Lowest[(low * DeBruijn) >> 58];
                    if (!_conductorBox[c].Overlaps(reach) || !EnvelopeMeets(c, centre, radius, reach))
                        continue;
                    if (count < results.Length)
                        results[count] = _conductors[c];
                    count++;
                }
            }
            return count;
        }

        /// <summary>Whether conductor c's envelope meets the sphere: its chain within radius + its reach of the centre.</summary>
        private bool EnvelopeMeets(int c, Double3 centre, double radius, in Bounds reach)
        {
            ref readonly Prim p = ref _prims[_conductorPrim[c]];
            double inflate = _conductorReach[c], r = radius + inflate, rr = r * r;
            ShapeMath.WireRange(_wireProj, p.WireFirst, p.WireCount, _wirePoints[p.WireFirst], p.Geometry.AxisY, inflate, reach,
                out int lo, out int hi);
            for (int i = lo; i <= hi; i++)
            {
                if (SegmentDistanceSquared(centre, _wirePoints[i], _wirePoints[i + 1]) <= rr)
                    return true;
            }
            return false;
        }

        // ---- Load --------------------------------------------------------------------------------------------------

        /// <summary>A variant's wire records' fields beyond the chain, their edge radii and its ports (MapWorldLoad's ParseMeta).</summary>
        private static Lines ParseLines(string path, JsonNode root, JsonNode wires, Meta meta)
        {
            int n = wires.Items.Count;
            var lines = new Lines
            {
                Diameter = new double[n], Mass = new double[n], Axial = new double[n], Breaking = new double[n], Bending = new double[n],
                Edge = new double[n],
            };
            JsonNode snag = root.Member("snag");
            if (snag != null && snag.Kind != JsonKind.Array)
                throw new FormatException(path + ": snag is not an array");
            for (int w = 0; w < n; w++)
            {
                JsonNode wire = wires.Items[w];
                string where = path + " wire " + w;
                lines.Diameter[w] = Number(wire, "diameter_m", where);
                lines.Mass[w] = Positive(wire, "mass_kg_per_m", where);
                lines.Axial[w] = Positive(wire, "axial_stiffness_n", where);
                lines.Breaking[w] = Positive(wire, "breaking_force_n", where);
                if (wire.Member("bending_stiffness_n_m2") != null)
                    lines.Bending[w] = Positive(wire, "bending_stiffness_n_m2", where);
                lines.Edge[w] = meta.WireRadius[w];
                for (int s = 0; snag != null && s < snag.Items.Count; s++)
                {
                    JsonNode record = snag.Items[s];
                    if (Text(record, "kind", path + " snag " + s) != "edge" || !SamePoints(record, meta.WirePoints[w], path + " snag " + s))
                        continue;
                    if (record.Member("edge_radius_m") != null)
                        lines.Edge[w] = Positive(record, "edge_radius_m", path + " snag " + s);
                    break;
                }
            }
            JsonNode ports = root.Member("ports");
            if (ports != null && ports.Kind != JsonKind.Array)
                throw new FormatException(path + ": ports is not an array");
            int m = ports == null ? 0 : ports.Items.Count;
            lines.PortPosition = new Double3[m];
            lines.PortDirection = new Double3[m];
            for (int p = 0; p < m; p++)
            {
                lines.PortPosition[p] = Vector(ports.Items[p], "position", path + " port " + p);
                lines.PortDirection[p] = Vector(ports.Items[p], "direction", path + " port " + p);
            }
            return lines;
        }

        private static double Positive(JsonNode o, string key, string where)
        {
            double v = Number(o, key, where);
            if (!(v > 0))
                throw new FormatException(where + ": " + key + " must be above 0");
            return v;
        }

        /// <summary>Whether a snag record's points are exactly the chain's (the same numbers, in the same order).</summary>
        private static bool SamePoints(JsonNode record, Double3[] chain, string where)
        {
            JsonNode points = Need(record, "points", JsonKind.Array, where);
            if (points.Items.Count != chain.Length)
                return false;
            for (int k = 0; k < chain.Length; k++)
            {
                Double3 v = Vector(points.Items[k], where + " points");
                if (!(v.X == chain[k].X && v.Y == chain[k].Y && v.Z == chain[k].Z))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// The conductors: each placed wire's record and envelope, its ends paired with the placed ports and their
        /// neighbours, and the envelope index. After the prims and the grid's extent. Returns the world bugs (ends with no port).
        /// </summary>
        private string[] PlaceWires(Meta[] rowMeta, Double3[] rowPosition, Quat[] rowYaw, string[] rowClass, string file)
        {
            // The placed ports, object by object, each in its metadata's order.
            var portObject = new List<int>();
            var portIndex = new List<int>();
            var portPosition = new List<Double3>();
            var portDirection = new List<Double3>();
            var portKind = new List<FittingKind>();
            for (int i = 0; i < rowMeta.Length; i++)
            {
                Lines lines = rowMeta[i].Lines;
                for (int p = 0; p < lines.PortPosition.Length; p++)
                {
                    portObject.Add(i);
                    portIndex.Add(p);
                    portPosition.Add(rowPosition[i] + rowYaw[i].Rotate(lines.PortPosition[p]));
                    portDirection.Add(rowYaw[i].Rotate(lines.PortDirection[p]));
                    portKind.Add(rowClass[i] == "houses" ? FittingKind.HouseAnchor : FittingKind.PoleHook);
                }
            }

            var prims = new List<int>();
            for (int k = 0; k < _prims.Length; k++)
                if (_prims[k].Geometry.Kind == ShapeKind.Wire)
                    prims.Add(k);
            if (prims.Count > MaxConductors)
                throw new FormatException(file + $": {prims.Count} conductors, more than a map may hold ({MaxConductors})");
            int n = prims.Count;
            _conductorPrim = prims.ToArray();
            _conductorReach = new double[n];
            _conductorBox = new Bounds[n];
            var endPort = new int[2 * n]; // end 2c is conductor c's A, 2c + 1 its B: its port's index above, or −1
            var bugs = new List<string>();
            for (int c = 0; c < n; c++)
            {
                ref readonly Prim p = ref _prims[_conductorPrim[c]];
                Double3 first = _wirePoints[p.WireFirst];
                double far = 0;
                for (int k = p.WireFirst; k < p.WireFirst + p.WireCount; k++)
                    far = DetMath.Max(far, (_wirePoints[k] - first - p.Geometry.AxisY * _wireProj[k]).Length());
                double blowOut = 2 * far * SinHalfBlowOut;
                _conductorReach[c] = p.Geometry.Radius + blowOut;
                var grow = new Double3(blowOut, blowOut, blowOut);
                _conductorBox[c] = new Bounds(p.Box.Min - grow, p.Box.Max + grow);
                for (int e = 0; e < 2; e++)
                {
                    Double3 end = _wirePoints[e == 0 ? p.WireFirst : p.WireFirst + p.WireCount - 1];
                    int best = -1;
                    double bestSquared = PortTolerance * PortTolerance;
                    for (int q = 0; q < portPosition.Count; q++)
                    {
                        Double3 off = end - (portPosition[q] + portDirection[q] * p.Geometry.Radius);
                        double squared = Double3.Dot(off, off);
                        if (squared <= bestSquared && (best < 0 || squared < bestSquared))
                        {
                            best = q;
                            bestSquared = squared;
                        }
                    }
                    endPort[2 * c + e] = best;
                    if (best < 0)
                        bugs.Add(FormattableString.Invariant(
                            $"{file} instance {p.Object} ({_names[p.Object]}) wire {p.Shape - rowMeta[p.Object].Shapes.Length}: end {(e == 0 ? "A" : "B")} at ({end.X:0.000}, {end.Y:0.000}, {end.Z:0.000}) hangs from no port within {1000 * PortTolerance} mm of its seat"));
                }
            }

            // The ends on each port, in canonical order; each end's neighbour is the first of another conductor.
            var onPort = new List<int>[portPosition.Count];
            for (int k = 0; k < endPort.Length; k++)
                if (endPort[k] >= 0)
                    (onPort[endPort[k]] = onPort[endPort[k]] ?? new List<int>()).Add(k);
            WireEnd End(int k)
            {
                int q = endPort[k];
                var end = new WireEnd { Object = -1, Port = -1, Kind = FittingKind.None, NeighbourObject = -1, NeighbourShape = -1 };
                if (q < 0)
                    return end;
                end.Object = portObject[q];
                end.Port = portIndex[q];
                end.Kind = portKind[q];
                foreach (int other in onPort[q])
                {
                    if (other >> 1 == k >> 1)
                        continue;
                    ref readonly Prim o = ref _prims[_conductorPrim[other >> 1]];
                    end.NeighbourObject = o.Object;
                    end.NeighbourShape = o.Shape;
                    break;
                }
                return end;
            }

            _conductors = new Conductor[n];
            for (int c = 0; c < n; c++)
            {
                ref readonly Prim p = ref _prims[_conductorPrim[c]];
                Lines lines = rowMeta[p.Object].Lines;
                int w = p.Shape - rowMeta[p.Object].Shapes.Length;
                _conductors[c] = new Conductor
                {
                    Object = p.Object,
                    Shape = p.Shape,
                    Diameter = lines.Diameter[w],
                    MassPerMetre = lines.Mass[w],
                    Sag = p.Geometry.Sag,
                    AxialStiffness = lines.Axial[w],
                    BreakingForce = lines.Breaking[w],
                    BendingStiffness = lines.Bending[w],
                    EdgeRadius = lines.Edge[w],
                    Material = p.Geometry.Material,
                    Points = new ReadOnlyMemory<Double3>(_wirePoints, p.WireFirst, p.WireCount),
                    A = End(2 * c),
                    B = End(2 * c + 1),
                };
            }
            ListCells(n, c => (CellX(_conductorBox[c].Min.X), CellX(_conductorBox[c].Max.X), CellZ(_conductorBox[c].Min.Z), CellZ(_conductorBox[c].Max.Z)),
                out _conductorCellStart, out _conductorCellItems);
            return bugs.ToArray();
        }
    }
}
