// MapWorld's stem field (w9-village-physics 2.1, design.md "## 2.1"; QueryVersion 9): the standing grass a map package
// draws, as the physics reads it, derived at load and never painted.
// - The counts: the package's grass/map.raw (map.json "grass": one resolution² plane of uint8 instance counts per layer,
//   row 0 the northmost, cell side (map_rect_m's width) / resolution; maps/farmland/README.md), its SHA-256 checked against
//   the manifest, which the content hash covers. The map only: the surround's grass is not exported, so outside the map's
//   rectangle there are no stems.
// - Per layer, from map.json: the prefab, the group, the height scale range and the mesh's top (mesh_y_m[1]); from
//   assets-src/vendor_plant_numbers.json (2.8, measured from the store's meshes): the prefab's stems per instance and its
//   count profile, the stems a horizontal plane at 5 %, 10 % … 95 % of the mesh's top crosses. That file's text joins the
//   content hash after the material names.
// - The kind: the group's state, GreenBlade for Green, GreenClump and TallGreen, DryCulm for Dry, DryClump and TallDry,
//   BurntCulm for Burnt and TallBurnt.
// - The height range: the drawn tops, mesh_y_m[1] × the height scale range.
// - How many reach a height h above the ground, per instance: R(f) at f = h / top, top the instance's drawn top. R is the
//   profile's upper envelope (at each plane the most any plane at or above it crosses, so it never rises with height; at
//   and below the densest plane it is the stems per instance), linear between planes, falling linearly from the 95 % plane
//   to 0 at the top. The top is the mesh's top × a height scale drawn uniformly over the layer's range (assumed: Unity
//   spreads it by a noise), so per layer Reach(h) is R's mean over 64 scales at the range's midpoints, tabled at load
//   every ReachStep up to the tallest top and read linearly between, 0 from the tallest top up. Per cell: stems per m² reaching h = the cell's count ×
//   Reach(h) / the cell's area.
// - StemsNear(centre, radius, height): every (cell, layer) with stems reaching `height` whose cell the circle overlaps with
//   positive area, in canonical order (row north to south, then column, then layer), with the circle's area in the cell
//   (exact: the circle's four signed quadrant boxes, one Atan2 pair each). Stems in the circle = Density × Area.
// + − × ÷, √ and DetMath.Atan2 only; allocation-free and reentrant; load time allocates and throws FormatException.

using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld
    {
        /// <summary>The stem field's height table's step, m.</summary>
        public const double ReachStep = 0.01;

        /// <summary>The scales averaged per table entry (the layer's height scale range at its 64 midpoints).</summary>
        private const int ReachScales = 64;

        /// <summary>The 2.8 numbers' file, from the repository root.</summary>
        public const string PlantNumbers = "assets-src/vendor_plant_numbers.json";

        /// <summary>A package's stem field (null on a world without grass).</summary>
        private sealed class StemField
        {
            public int N;                  // cells a side
            public double X0, Z1, Side, InverseArea;
            public byte[] Counts;          // layer · N² + row · N + column
            public StemKind[] Kind;        // per layer
            public double[] HeightMin, HeightMax;
            public double[][] Reach;       // per layer: stems per instance reaching k · ReachStep, the last entry 0
            public string NumbersText;     // vendor_plant_numbers.json, for the content hash
        }

        private readonly StemField _stems;

        /// <summary>The kind a grass group's stems are (design.md 2.1): its state, from the group's name.</summary>
        private static StemKind KindOf(string group, string where)
        {
            switch (group)
            {
                case "Green": case "GreenClump": case "TallGreen": return StemKind.GreenBlade;
                case "Dry": case "DryClump": case "TallDry": return StemKind.DryCulm;
                case "Burnt": case "TallBurnt": return StemKind.BurntCulm;
                default: throw new FormatException(where + ": group " + group + " has no stem kind");
            }
        }

        /// <summary>The stem field of a package's map.json "grass" (null when it has none), its counts read by `readFile`.</summary>
        private static StemField ReadStems(string manifest, Func<string, byte[]> readFile, Func<string> readNumbers)
        {
            const string file = "map.json";
            JsonNode root = DetJson.Parse(manifest);
            JsonNode grass = root.Member("grass");
            if (grass == null)
                return null;
            string where = file + " grass";
            JsonNode rect = Need(root, "map_rect_m", JsonKind.Array, file);
            double x0 = Number(rect.Items[0], where), z0 = Number(rect.Items[1], where), x1 = Number(rect.Items[2], where), z1 = Number(rect.Items[3], where);
            if (Text(grass, "tile", where) != "map" || x1 - x0 != z1 - z0)
                throw new FormatException(where + ": the grass is the square map tile's");
            int n = (int)Number(grass, "resolution", where);
            JsonNode layers = Need(grass, "layers", JsonKind.Array, where);
            int count = layers.Items.Count;
            byte[] counts = Terrain.Checked(Text(grass, "file", where), Need(root, "files", JsonKind.Object, file), readFile, where);
            if (n < 1 || counts.Length != count * n * n)
                throw new FormatException(where + $": {counts.Length} bytes, not {count} layers of {n}²");

            string numbersText = readNumbers();
            JsonNode numbers = Need(Need(DetJson.Parse(numbersText), "grass", JsonKind.Object, PlantNumbers), "prefabs", JsonKind.Object, PlantNumbers);
            double side = (x1 - x0) / n;
            var f = new StemField
            {
                N = n, X0 = x0, Z1 = z1, Side = side, InverseArea = 1 / (side * side), Counts = counts, NumbersText = numbersText,
                Kind = new StemKind[count], HeightMin = new double[count], HeightMax = new double[count], Reach = new double[count][],
            };
            for (int l = 0; l < count; l++)
            {
                JsonNode layer = layers.Items[l];
                string at = where + " layer " + l;
                if ((int)Number(layer, "layer", at) != l)
                    throw new FormatException(at + ": layers are in order");
                string prefab = Text(layer, "prefab", at);
                f.Kind[l] = KindOf(Text(layer, "group", at), at);
                JsonNode scale = Need(layer, "height_scale", JsonKind.Array, at), mesh = Need(layer, "mesh_y_m", JsonKind.Array, at);
                double s0 = Number(scale.Items[0], at), s1 = Number(scale.Items[1], at), top = Number(mesh.Items[1], at);
                if (!(top > 0 && s0 > 0 && s1 >= s0))
                    throw new FormatException(at + ": a positive mesh top and height scale range");
                f.HeightMin[l] = top * s0;
                f.HeightMax[l] = top * s1;

                JsonNode p = numbers.Member(prefab) ?? throw new FormatException(PlantNumbers + ": no grass prefab " + prefab);
                JsonNode profile = Need(p, "profile", JsonKind.Array, PlantNumbers + " " + prefab);
                int planes = profile.Items.Count;
                var fraction = new double[planes];
                var envelope = new double[planes];
                for (int i = 0; i < planes; i++)
                {
                    fraction[i] = Number(profile.Items[i], "at", PlantNumbers + " " + prefab);
                    envelope[i] = Number(profile.Items[i], "stems", PlantNumbers + " " + prefab);
                    if (i > 0 && !(fraction[i] > fraction[i - 1]) || !(fraction[i] > 0 && fraction[i] < 1))
                        throw new FormatException(PlantNumbers + " " + prefab + ": the profile's planes rise inside the mesh");
                }
                for (int i = planes - 2; i >= 0; i--)
                    envelope[i] = DetMath.Max(envelope[i], envelope[i + 1]);
                if (envelope[0] != Number(p, "stems", PlantNumbers + " " + prefab))
                    throw new FormatException(PlantNumbers + " " + prefab + ": stems is not the profile's most");

                // Reach(h) over 0, ReachStep, … up to the first step at or past the tallest top, whose entry is 0.
                int steps = (int)Math.Ceiling(f.HeightMax[l] / ReachStep) + 1;
                var reach = new double[steps];
                for (int k = 0; k < steps - 1; k++)
                {
                    double h = k * ReachStep, sum = 0;
                    for (int j = 0; j < ReachScales; j++)
                        sum += Profile(fraction, envelope, h / (top * (s0 + (s1 - s0) * ((j + 0.5) / ReachScales))));
                    reach[k] = sum / ReachScales;
                }
                f.Reach[l] = reach;
            }
            return f;
        }

        /// <summary>R(f): the stems per instance reaching the fraction f of its top (the envelope, linear between planes, 0 at 1).</summary>
        private static double Profile(double[] fraction, double[] envelope, double f)
        {
            int last = fraction.Length - 1;
            if (f <= fraction[0])
                return envelope[0];
            if (f >= 1)
                return 0;
            if (f >= fraction[last])
                return envelope[last] * ((1 - f) / (1 - fraction[last]));
            int i = 0;
            while (f > fraction[i + 1])
                i++;
            double u = (f - fraction[i]) / (fraction[i + 1] - fraction[i]);
            return envelope[i] + (envelope[i + 1] - envelope[i]) * u;
        }

        /// <inheritdoc />
        public int StemsNear(XZ centre, double radius, double height, Span<StemPatch> results)
        {
            StemField f = _stems;
            if (f == null || !(radius > 0))
                return 0;
            int n = f.N;
            // The cells the circle's box meets: columns west to east, rows north (row 0) to south.
            double c0 = Math.Floor((centre.X - radius - f.X0) / f.Side), c1 = Math.Floor((centre.X + radius - f.X0) / f.Side);
            double r0 = Math.Floor((f.Z1 - (centre.Z + radius)) / f.Side), r1 = Math.Floor((f.Z1 - (centre.Z - radius)) / f.Side);
            if (!(c1 >= 0 && c0 < n && r1 >= 0 && r0 < n))
                return 0;
            int col0 = (int)DetMath.Max(c0, 0), col1 = (int)DetMath.Min(c1, n - 1), row0 = (int)DetMath.Max(r0, 0), row1 = (int)DetMath.Min(r1, n - 1);
            double h = DetMath.Max(height, 0) / ReachStep;
            int count = 0, plane = n * n;
            for (int row = row0; row <= row1; row++)
            {
                double zTop = f.Z1 - row * f.Side, zBottom = f.Z1 - (row + 1) * f.Side;
                for (int col = col0; col <= col1; col++)
                {
                    int cell = row * n + col;
                    double xWest = f.X0 + col * f.Side, xEast = f.X0 + (col + 1) * f.Side;
                    double area = -1;
                    for (int l = 0; l < f.Kind.Length; l++)
                    {
                        byte instances = f.Counts[l * plane + cell];
                        if (instances == 0)
                            continue;
                        double[] reach = f.Reach[l];
                        if (!(height < f.HeightMax[l])) // the table's last step reads past the tallest top: none there
                            continue;
                        int k = Math.Min((int)h, reach.Length - 2); // h / step may round up onto the last entry
                        double per = reach[k] + (reach[k + 1] - reach[k]) * (h - k);
                        if (!(per > 0))
                            continue;
                        if (area < 0)
                            area = CircleInRectangle(centre, radius, xWest, zBottom, xEast, zTop);
                        if (!(area > 0))
                            break;
                        if (count < results.Length)
                            results[count] = new StemPatch
                            {
                                Cell = cell,
                                Layer = (byte)l,
                                Kind = f.Kind[l],
                                Area = area,
                                Density = instances * per * f.InverseArea,
                                HeightMin = f.HeightMin[l],
                                HeightMax = f.HeightMax[l],
                            };
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>The area of the circle at `centre` of `radius` inside [x0, x1] × [z0, z1], m²: its four signed quadrant boxes.</summary>
        internal static double CircleInRectangle(XZ centre, double radius, double x0, double z0, double x1, double z1)
        {
            double a = x0 - centre.X, b = x1 - centre.X, c = z0 - centre.Z, d = z1 - centre.Z;
            return Quadrant(b, d, radius) - Quadrant(a, d, radius) - Quadrant(b, c, radius) + Quadrant(a, c, radius);
        }

        /// <summary>The signed area of the circle (at the origin, of radius r) inside the box between the origin and (x, z).</summary>
        private static double Quadrant(double x, double z, double r)
        {
            double ax = DetMath.Min(DetMath.Abs(x), r), az = DetMath.Min(DetMath.Abs(z), r), area;
            double rr = r * r;
            if (ax * ax + az * az <= rr)
                area = ax * az;
            else
            {
                // The corner lies outside: the box to where z's line meets the circle, then the circle's own strip.
                double xc = DetMath.Sqrt(DetMath.Max(rr - az * az, 0)), zc = DetMath.Sqrt(DetMath.Max(rr - ax * ax, 0));
                area = 0.5 * (xc * az + ax * zc) + 0.5 * rr * (DetMath.Atan2(ax, zc) - DetMath.Atan2(xc, az));
            }
            return (x < 0) != (z < 0) ? -area : area;
        }
    }
}
