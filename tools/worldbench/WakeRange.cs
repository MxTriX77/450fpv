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
    // The wake grid over the test range's placements (p6-test-range's maps/test-range/placements.json and the placed
    // assets' .assetmeta.json wind volumes), read from a checkout of that branch: a real layout as a second fixture
    // (p7c-wind 1.3). Numbers only: the data lives on another branch, so nothing here is golden.
    // A wind volume turned off the vertical (a roof plane, a leaning post) enters as the upright box that bounds it in
    // the asset's frame; one turned about the vertical keeps its yaw. The conversion uses System.Math: it is the tool's
    // input, not the sim core.
    internal static class WakeRange
    {
        public static int Run(string root)
        {
            string placements = Path.Combine(root, "maps", "test-range", "placements.json");
            string models = Path.Combine(root, "unity", "Assets", "_Project", "Art", "Models");
            if (!File.Exists(placements) || !Directory.Exists(models))
            {
                Console.WriteLine($"worldbench --wakes-range: no test range under {root} (maps/test-range/placements.json and unity/Assets/_Project/Art/Models)");
                return 2;
            }
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(placements));
            var metas = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
            var volumes = new List<WindVolume>();
            int placed = 0, tilted = 0;
            foreach (JsonElement inst in doc.RootElement.GetProperty("instances").EnumerateArray())
            {
                string variant = inst.GetProperty("variant").GetString(), cls = inst.GetProperty("class").GetString(), asset = inst.GetProperty("asset").GetString();
                if (!metas.TryGetValue(variant, out JsonDocument meta))
                {
                    string path = Path.Combine(models, cls, asset, variant + ".assetmeta.json");
                    meta = File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path)) : null;
                    metas[variant] = meta;
                }
                if (meta == null || !meta.RootElement.TryGetProperty("wind_volumes", out JsonElement wvs)) continue;
                JsonElement pos = inst.GetProperty("position_m");
                double px = pos[0].GetDouble(), pz = pos[2].GetDouble(), yaw = inst.GetProperty("yaw_deg").GetDouble() * Math.PI / 180;
                foreach (JsonElement wv in wvs.EnumerateArray())
                {
                    placed++;
                    JsonElement c = wv.GetProperty("center"), r = wv.GetProperty("rotation");
                    double qx = r[0].GetDouble(), qy = r[1].GetDouble(), qz = r[2].GetDouble(), qw = r[3].GetDouble();
                    double porosity = wv.GetProperty("porosity").GetDouble();
                    var centre = new Double3(c[0].GetDouble(), c[1].GetDouble(), c[2].GetDouble());
                    bool cylinder = wv.GetProperty("shape").GetString() == "cylinder";
                    double sx, sy, sz, radius = 0;
                    if (cylinder)
                    {
                        radius = wv.GetProperty("radius").GetDouble();
                        sx = sz = 2 * radius;
                        sy = wv.GetProperty("height").GetDouble();
                    }
                    else
                    {
                        JsonElement size = wv.GetProperty("size");
                        sx = size[0].GetDouble(); sy = size[1].GetDouble(); sz = size[2].GetDouble();
                    }
                    double localYaw = 0;
                    if (Math.Abs(qx) < 1e-9 && Math.Abs(qz) < 1e-9)
                        localYaw = 2 * Math.Atan2(qy, qw); // about the vertical only: Unity's yaw, D-019's sense
                    else
                    {
                        // Tilted: the upright box bounding the turned one, in the asset's frame.
                        tilted++;
                        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                        for (int k = 0; k < 8; k++)
                        {
                            var v = new Double3((k & 1) == 0 ? -sx / 2 : sx / 2, (k & 2) == 0 ? -sy / 2 : sy / 2, (k & 4) == 0 ? -sz / 2 : sz / 2);
                            Double3 w = Rotate(qx, qy, qz, qw, v);
                            minX = Math.Min(minX, w.X); maxX = Math.Max(maxX, w.X);
                            minY = Math.Min(minY, w.Y); maxY = Math.Max(maxY, w.Y);
                            minZ = Math.Min(minZ, w.Z); maxZ = Math.Max(maxZ, w.Z);
                        }
                        centre = centre + new Double3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
                        sx = maxX - minX; sy = maxY - minY; sz = maxZ - minZ;
                        cylinder = false;
                    }
                    WindVolume vol = WindVolume.Placed(cylinder ? WindVolumeKind.Cylinder : WindVolumeKind.Box, centre, sx, sy, sz, radius,
                        porosity, px, pz, yaw, 1);
                    vol.Yaw = yaw + localYaw;
                    if (vol.BaseM < 0) vol.BaseM = 0; // parts set into the ground
                    if (!(vol.TopM > vol.BaseM)) continue;
                    volumes.Add(vol);
                }
            }
            foreach (JsonDocument m in metas.Values) m?.Dispose();

            long t0 = Stopwatch.GetTimestamp();
            var obstacles = new WakeObstacles(-256, -256, 256, 256, volumes.ToArray());
            double obstacleMs = (Stopwatch.GetTimestamp() - t0) * 1e3 / Stopwatch.Frequency;
            int cells = 0;
            for (int k = 0; k < 256 * 256; k++) cells += obstacles.IsObstacle(k) ? 1 : 0;
            Console.WriteLine(FormattableString.Invariant(
                $"  test range: {placed} wind volumes placed ({tilted} tilted, bounded upright), {volumes.Count} obstacles; 512 m grid: {cells} obstacle cells in {obstacles.Groups} groups, tallest {obstacles.MaxTop:F2} m; built in {obstacleMs:F1} ms"));

            foreach (double deg in new[] { 0.0, 90 })
            {
                t0 = Stopwatch.GetTimestamp();
                WakeGrid grid = WakeGrid.Build(obstacles, deg);
                double ms = (Stopwatch.GetTimestamp() - t0) * 1e3 / Stopwatch.Frequency;
                Console.WriteLine(FormattableString.Invariant($"  wind from {deg} deg: built in {ms:F1} ms, {grid.WakeCells} cells in a wake, hash {grid.Hash():x16}"));
            }

            // Behind the posadka (z −6.4 .. 6.4) with wind from the north, at three columns.
            WakeGrid north = WakeGrid.Build(obstacles, 0);
            foreach (double x in new[] { -101.0, 1, 101 })
            {
                int i = (int)((x + 256) / 2), last = -1;
                Console.WriteLine(FormattableString.Invariant($"  behind the posadka at x = {x} m (wind from the north):  z m    x/H     H m   beta    phi | u(0.5H) T(0.5H) u(1H) T(1H) T(2H)"));
                foreach (double target in new[] { 0.5, 2, 5, 10, 15, 20, 30 })
                {
                    int best = -1;
                    double err = double.MaxValue;
                    for (int j = 128; j >= 0; j--)
                    {
                        north.Describe(i, j, out double cx, out double h, out _, out _, out _, out _, out _);
                        if (h > 0 && Math.Abs(cx - target) < err) { err = Math.Abs(cx - target); best = j; }
                    }
                    if (best < 0 || best == last) continue;
                    last = best;
                    north.Describe(i, best, out double X, out double H, out _, out double beta, out double phi, out _, out _);
                    north.SampleCell(i, best, 0.5 * H, out WakeSample a);
                    north.SampleCell(i, best, 1.0 * H, out WakeSample b);
                    north.SampleCell(i, best, 2.0 * H, out WakeSample c);
                    Console.WriteLine(FormattableString.Invariant(
                        $"    {-256 + 2 * best + 1,7:F1} {X,6:F2} {H,6:F2} {beta,6:F3} {phi,6:F3} | {a.Speed,6:F3} {a.Turbulence,6:F3}  {b.Speed,5:F3} {b.Turbulence,5:F3} {c.Turbulence,5:F3}"));
                }
            }
            return 0;
        }

        private static Double3 Rotate(double x, double y, double z, double w, Double3 v)
        {
            // v' = q v q*, Hamilton product (DetMath's convention; Unity's quaternion components one to one).
            double tx = 2 * (y * v.Z - z * v.Y), ty = 2 * (z * v.X - x * v.Z), tz = 2 * (x * v.Y - y * v.X);
            return new Double3(v.X + w * tx + (y * tz - z * ty), v.Y + w * ty + (z * tx - x * tz), v.Z + w * tz + (x * ty - y * tx));
        }
    }
}
