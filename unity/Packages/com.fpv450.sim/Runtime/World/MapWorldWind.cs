// MapWorld's wind volumes (p7c-wind, the map's wind): each placed variant's `wind_volumes` records (K§10.2, the asset's
// physics metadata) placed at its row's pose, and each row's `group` (the map generator's: "posadka", "yard", ...), so the
// wind can build the wake grid's obstacles and read the tree belt from the world it flies in (MapWind). They are not
// queries: nothing here runs on the flight thread, and the content hash already covers every file they come from.
// - A volume upright in its asset (rotation the identity, or about the vertical only) keeps its shape and turns with the
//   row's yaw and its own (the asset's turn about +Y, 2·atan2(y, w) of its quaternion, D-019's sense).
// - A volume tilted off the vertical (a roof plane, a strut, a leaning car part) enters as the upright box that bounds
//   it in the asset's frame (worldbench's WakeRange does the same): the wake grid's obstacles are upright (K§10.3).
// - A part set into the ground keeps only what is above it (its base at 0); one with nothing above it is dropped.
// Load time only: it allocates and throws FormatException naming the file and the field.

using System;
using System.Collections.Generic;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld
    {
        /// <summary>A variant's wind volume in its own frame, as `.assetmeta.json` holds it.</summary>
        private struct WindRecord
        {
            public WindVolumeKind Kind;
            public Double3 Center, Size; // a cylinder's Size is (2r, height, 2r)
            public double Radius, Porosity;
            public Quat Rotation;
        }

        private WindVolume[] _windVolumes = Array.Empty<WindVolume>();
        private int[] _windObject = Array.Empty<int>();
        private string[] _groups;

        /// <summary>The placed objects' wind volumes, world metres (K§10.2), object by object in row order.</summary>
        public ReadOnlySpan<WindVolume> WindVolumes => _windVolumes;

        /// <summary>The placed object wind volume k belongs to (a row's index).</summary>
        public int WindVolumeObject(int k) => _windObject[k];

        /// <summary>A placed object's group in the placement file (the generator's: "posadka", "yard", ...), or null.</summary>
        public string ObjectGroup(int obj) => _groups[obj];

        /// <summary>The map's extent, world X and Z of its south-west corner, m, and its 2 m cells each way.</summary>
        public double ExtentX => _gridX;
        public double ExtentZ => _gridZ;
        public int CellsX => _nx;
        public int CellsZ => _nz;

        private static WindRecord[] ParseWind(JsonNode root, string path)
        {
            JsonNode list = root.Member("wind_volumes");
            if (list == null) return Array.Empty<WindRecord>();
            if (list.Kind != JsonKind.Array) throw new FormatException(path + ": wind_volumes is not an array");
            var records = new WindRecord[list.Items.Count];
            for (int v = 0; v < records.Length; v++)
            {
                JsonNode n = list.Items[v];
                string where = path + " wind_volumes " + v;
                var r = new WindRecord { Center = Vector(n, "center", where), Porosity = Number(n, "porosity", where) };
                JsonNode rotation = n.Member("rotation");
                r.Rotation = rotation == null ? Quat.Identity : Rotation(rotation, where);
                switch (Text(n, "shape", where))
                {
                    case "box":
                        r.Kind = WindVolumeKind.Box;
                        r.Size = Vector(n, "size", where);
                        break;
                    case "cylinder":
                        r.Kind = WindVolumeKind.Cylinder;
                        r.Radius = Number(n, "radius", where);
                        r.Size = new Double3(2 * r.Radius, Number(n, "height", where), 2 * r.Radius);
                        break;
                    default:
                        throw new FormatException(where + ": shape is box or cylinder");
                }
                if (!(r.Size.X > 0 && r.Size.Y > 0 && r.Size.Z > 0) || !(r.Porosity >= 0 && r.Porosity <= 1))
                    throw new FormatException(where + ": a size of 0 or less, or a porosity outside 0–1");
                records[v] = r;
            }
            return records;
        }

        /// <summary>Every row's wind volumes placed at its pose (position, yaw in radians), and the rows' groups.</summary>
        private void PlaceWind(Meta[] rowMeta, Double3[] rowPosition, double[] rowYaw, string[] groups)
        {
            _groups = groups;
            var volumes = new List<WindVolume>();
            var objects = new List<int>();
            for (int i = 0; i < rowMeta.Length; i++)
                foreach (WindRecord r in rowMeta[i].Wind)
                {
                    Quat q = r.Rotation;
                    Double3 centre = r.Center, size = r.Size;
                    WindVolumeKind kind = r.Kind;
                    double localYaw = 0;
                    if (DetMath.Abs(q.X) < 1e-9 && DetMath.Abs(q.Z) < 1e-9)
                        localYaw = 2 * DetMath.Atan2(q.Y, q.W); // about the vertical only
                    else
                    {
                        // Tilted: the upright box bounding the turned one, in the asset's frame.
                        Double3 lo = new Double3(double.MaxValue, double.MaxValue, double.MaxValue), hi = lo * -1;
                        for (int k = 0; k < 8; k++)
                        {
                            Double3 c = q.Rotate(new Double3((k & 1) == 0 ? -size.X / 2 : size.X / 2,
                                (k & 2) == 0 ? -size.Y / 2 : size.Y / 2, (k & 4) == 0 ? -size.Z / 2 : size.Z / 2));
                            lo = new Double3(DetMath.Min(lo.X, c.X), DetMath.Min(lo.Y, c.Y), DetMath.Min(lo.Z, c.Z));
                            hi = new Double3(DetMath.Max(hi.X, c.X), DetMath.Max(hi.Y, c.Y), DetMath.Max(hi.Z, c.Z));
                        }
                        centre = centre + (lo + hi) * 0.5;
                        size = hi - lo;
                        kind = WindVolumeKind.Box;
                    }
                    Double3 p = rowPosition[i];
                    WindVolume w = WindVolume.Placed(kind, centre, size.X, size.Y, size.Z, kind == WindVolumeKind.Cylinder ? r.Radius : 0,
                        r.Porosity, p.X, p.Z, rowYaw[i], 1);
                    w.Yaw = rowYaw[i] + localYaw;
                    if (w.BaseM < 0) w.BaseM = 0; // set into the ground
                    if (!(w.TopM > w.BaseM)) continue;
                    volumes.Add(w);
                    objects.Add(i);
                }
            _windVolumes = volumes.ToArray();
            _windObject = objects.ToArray();
        }
    }
}
