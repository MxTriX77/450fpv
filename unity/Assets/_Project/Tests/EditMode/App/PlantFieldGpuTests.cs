using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// The plants' bend field on the GPU against the CPU path (w16-plants-in-the-air 1.3, design.md §7.1: "PlantAir.Sample is its
    /// reference"). Over the farmland in Windy, a hover 1 m over its fields' grass for 8 s (published poses, the wash grids ticked
    /// from them), the field drawn frame by frame on the GPU (PlantField.compute); then at two frames, on every level and band:
    /// - **the port:** each sampled texel against the CPU's own bend (PlantAir.Bend) from the mean the kernel interpolates there
    ///   (the four nodes of each of the two snapshots, PlantAir.Mean, weighted as the kernel weighs them) with the wash grid's
    ///   bend and air at its node (PlantWash.Bend): the arithmetic in float against double;
    /// - **the plant:** each texel that is a mean grid's node, at a snapshot's time, against PlantAir.Sample at that plant (the
    ///   drawn bend is the CPU reference's there), and every sampled texel against it (the mean's interpolation between nodes, a
    ///   second and 8 or 32 m apart, reported).
    /// Needs a GPU with compute shaders: run without -nographics.
    /// </summary>
    public sealed class PlantFieldGpuTests
    {
        const double Hover = 1.0, Mass = 4.735; // the stem scenarios' hover (PlantWashTests)
        static readonly Double3 Where = new Double3(503, 0, 524); // HillsPilot's first waypoint: the open fields south of the village

        [Test]
        public void TheDrawnBendIsTheCpuReference()
        {
            if (!SystemInfo.supportsComputeShaders || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("no GPU (-nographics): the port's check needs compute shaders");
            ComputeShader cs = Resources.Load<ComputeShader>(FarmlandPlants.Kernel);
            Assert.That(cs, Is.Not.Null, "Resources/" + FarmlandPlants.Kernel);

            string root = AppData.Root;
            MapWorld map = MapWorld.Load(root, FlightWorlds.FarmlandMap);
            var world = new FlightWorld(map);
            using var wind = new WindField(WindSetup.ForMap(map, WindLevel.Windy), new FlightRandom(0x16B3), world.Hills);
            SurfaceTable surfaces = SurfaceTable.Parse(File.ReadAllText(AppData.File(SurfaceTable.RelativePath)));
            TetherTable table = TetherTable.Parse(File.ReadAllText(AppData.File(TetherTable.RelativePath)));
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            double air = table.Number("fiber.air_density_kg_m3");
            PlantMechanics[] bands = PlantBands.Mechanics(k => surfaces.Cover[(int)k].Set(Moisture.Dry), air);
            var grass = bands.Take(PlantBands.Grass).ToArray();
            var motors = new Double3[4];
            PropulsionSpec propulsion = spec.BuildPropulsion();
            for (int i = 0; i < 4; i++) motors[i] = propulsion.Motors[i].Position;
            double radius = propulsion.Props.Diameter / 2;
            var washAir = new PlantAir(wind);
            var fine = new PlantWash(table, radius, motors, map, washAir, grass, surfaces, Moisture.Dry, PlantField.LevelSize[0], PlantField.LevelCell[0]);
            var coarse = new PlantWash(table, radius, motors, map, washAir, grass, surfaces, Moisture.Dry, PlantField.LevelSize[1], PlantField.LevelCell[1]);

            var point = new[] { new XZ(Where.X, Where.Z) };
            var under = new GroundSample[1];
            map.SampleGround(point, under);
            var body = new Double3(Where.X, under[0].Height + Hover, Where.Z);
            var camera = new Vector3((float)body.X, (float)body.Y, (float)body.Z);
            var poses = new PoseBuffer();
            double thrust = Mass * 9.80665 / 4;
            PoseFrame Pose(long step) => new PoseFrame
            {
                Step = step, Position = body, Attitude = new Quat(0, 0, 0, 1), Armed = true,
                RotorThrust = new Corners { RearRight = thrust, FrontRight = thrust, RearLeft = thrust, FrontLeft = thrust },
            };
            poses.Begin(0, Pose(0), default);

            using var field = new PlantField(bands, wind, map, poses, fine, coarse, cs, threads: false);
            var reference = new PlantAir(wind);
            var frameAir = new PlantAir(wind);
            var lines = new List<string>();
            double portWorst = 0, nodeWorst = 0, betweenWorst = 0;
            string worstAt = "none";
            List<double> between = new List<double>(), washed = new List<double>(), steady = new List<double>();
            int ported = 0, nodes = 0;
            long step = 0;
            foreach (double t in new[] { 8.0, 8.52 })
            {
                long until = (long)Math.Round(t / FlightCore.Dt);
                for (; step <= until; step++)
                {
                    if (step > 0) poses.Publish(Pose(step), default);
                    if (step % 40 == 0 && step < until) field.Draw(step * FlightCore.Dt, camera);
                }
                Assert.That(field.Draw(t, camera), "the field draws at " + t + " s");
                frameAir.Begin(t);
                reference.Begin(t);
                var rng = new System.Random(31 + (int)(t * 100));
                for (int level = 0; level < PlantField.Levels; level++)
                {
                    Vector4 box = field.Box(level);
                    Assert.That(box.w, Is.GreaterThan(0), $"level {level} holds the frame");
                    int size = (int)box.w;
                    int[] levelBands = PlantField.LevelBands[level];
                    for (int slice = 0; slice < levelBands.Length; slice++)
                    {
                        int band = levelBands[slice];
                        Color[] gpu = Read(field.Level(level), slice, size);
                        var texels = new List<(int, int)>();
                        for (int k = 0; k < 24; k++) texels.Add((rng.Next(size), rng.Next(size)));
                        // the texels that are the mean grid's nodes
                        int grid = PlantField.GridOf(level, band);
                        Vector4 node = field.MeanBox(grid, 0);
                        for (int a = 0; a < size; a++)
                        {
                            double x = box.x + (a + 0.5) * box.z;
                            double u = (x - node.x) / node.z;
                            if (Math.Abs(u - Math.Round(u)) > 1e-6 || level < 2) continue;
                            for (int b = 0; b < size; b += 7)
                            {
                                double z = box.y + (b + 0.5) * box.z, v = (z - node.y) / node.z;
                                if (Math.Abs(v - Math.Round(v)) < 1e-6) texels.Add((a, b));
                            }
                        }
                        foreach ((int a, int b) in texels)
                        {
                            double x = box.x + (a + 0.5) * box.z, z = box.y + (b + 0.5) * box.z;
                            Color g = gpu[a + size * b];
                            if (level < 2)
                            {
                                // A wash level's texel is its node, at the window's own coordinates; its last row and column hold none.
                                (long wi, long wj) = field.WashStart(level);
                                x = (wi + a + 0.5) * PlantField.LevelCell[level];
                                z = (wj + b + 0.5) * PlantField.LevelCell[level];
                                if (a == size - 1 || b == size - 1)
                                {
                                    Assert.That(g.b, Is.EqualTo(0f), $"level {level}: the window's edge holds no bend");
                                    continue;
                                }
                            }
                            // The port: the kernel's mean, from the CPU, and PlantAir.Bend.
                            if (!Port(field, level, band, bands[band], x, z, fine, coarse, map, wind, frameAir, out PlantBend cpu, out bool atNode, out double ground, out PlantMean used))
                            {
                                Assert.That(g.b, Is.EqualTo(0f), $"level {level} band {band} ({x:0.00}, {z:0.00}): no bend where the CPU has none");
                                continue;
                            }
                            Assert.That(g.b, Is.EqualTo(1f), $"level {level} band {band} ({x:0.00}, {z:0.00}) holds a bend");
                            double port = Math.Max(Math.Abs(g.r - cpu.X), Math.Abs(g.g - cpu.Z));
                            portWorst = Math.Max(portWorst, port);
                            ported++;
                            Assert.That(port, Is.LessThan(2e-3), $"the port at level {level} band {band} ({x:0.00}, {z:0.00}), t {t}: GPU ({g.r:F5}, {g.g:F5}) CPU ({cpu.X:F5}, {cpu.Z:F5})");
                            // The plant: PlantAir.Wind (the wind alone) and PlantAir.Sample (the wind and the wash) at its base.
                            var basePoint = new Double3(x, ground + PlantBands.Elevation(band), z);
                            reference.Wind(bands[band], basePoint, ground, out PlantBend windOnly);
                            reference.Sample(bands[band], band < PlantBands.Grass ? band : -1, fine, coarse, basePoint, ground, out PlantBend sample);
                            double toWind = Math.Max(Math.Abs(g.r - windOnly.X), Math.Abs(g.g - windOnly.Z));
                            double toSample = Math.Max(Math.Abs(g.r - sample.X), Math.Abs(g.g - sample.Z));
                            if (level >= 2 && atNode && Math.Abs(t / field.MeanTime(grid, 0) - 1) < 1e-12)
                            {
                                // At a node and a snapshot's time the drawn bend is the wind's at that plant.
                                nodeWorst = Math.Max(nodeWorst, toWind);
                                nodes++;
                                Assert.That(toWind, Is.LessThan(2e-3), $"a node's plant at level {level} band {band} ({x:0.00}, {z:0.00}): GPU ({g.r:F5}, {g.g:F5}) Wind ({windOnly.X:F5}, {windOnly.Z:F5})");
                            }
                            bool drawnHere = band >= PlantBands.Grass || !Inside(field.Box(level == 1 ? 0 : 1), x, z, level >= 1); // a finer wash level draws it there
                            if (!drawnHere) continue;
                            if (level < 2) washed.Add(toSample);
                            else
                            {
                                between.Add(toWind);
                                if (band < PlantBands.Grass) steady.Add(Math.Max(Math.Abs(sample.X - windOnly.X), Math.Abs(sample.Z - windOnly.Z)));
                                if (toWind > betweenWorst)
                                {
                                    betweenWorst = toWind;
                                    worstAt = $"level {level} band {band} ({x:0.00}, {z:0.00}), {Math.Sqrt((x - body.X) * (x - body.X) + (z - body.Z) * (z - body.Z)):0.0} m from the drone, " +
                                              $"t {t}: GPU ({g.r:F4}, {g.g:F4}), Wind ({windOnly.X:F4}, {windOnly.Z:F4})";
                                }
                            }
                        }
                    }
                }
                lines.Add($"t {t} s: the wash's levels at ({field.Box(0).x:0.00}, {field.Box(0).y:0.00}) and ({field.Box(1).x:0.00}, {field.Box(1).y:0.00}), the means' blend {field.MeanBlend(0):0.00}, the wind's mean {field.MeanMsPerSecond:0} ms a second of flight");
            }
            foreach (string l in lines) TestContext.WriteLine(l);
            TestContext.WriteLine($"the port: {ported} texels within {portWorst:E2} rad of PlantAir.Bend on the kernel's mean");
            TestContext.WriteLine($"the plants at the means' nodes at a snapshot's time: {nodes} within {nodeWorst:E2} rad of PlantAir.Wind");
            TestContext.WriteLine("the plants between nodes and snapshots, against PlantAir.Wind (the mean's interpolation): " + Stats(between) + "; the worst " + worstAt);
            TestContext.WriteLine("the plants on the wash's grids, against PlantAir.Sample: " + Stats(washed));
            TestContext.WriteLine("left out, the wash's steady air on the grass beyond its grids (PlantAir.Sample less PlantAir.Wind there): " + Stats(steady));
            Assert.That(nodes, Is.GreaterThan(20), "texels on the means' nodes were checked");
            Assert.That(ported, Is.GreaterThan(1000));
        }

        static string Stats(List<double> e)
        {
            if (e.Count == 0) return "none";
            e.Sort();
            return $"{e.Count} texels, median {e[e.Count / 2]:E2}, 95th percentile {e[(int)(e.Count * 0.95)]:E2}, worst {e[e.Count - 1]:E2} rad";
        }

        // Within a wash level's box (when the check applies).
        static bool Inside(Vector4 box, double x, double z, bool check) =>
            check && box.w > 0 && x >= box.x && z >= box.y && x <= box.x + box.z * box.w && z <= box.y + box.z * box.w;

        static Color[] Read(RenderTexture rt, int slice, int size)
        {
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(rt, 0, 0, size, 0, size, slice, 1, TextureFormat.RGBAFloat);
            request.WaitForCompletion();
            Assert.That(request.hasError, Is.False, "the level's read back");
            NativeArray<Color> data = request.GetData<Color>();
            return data.ToArray();
        }

        // The kernel's arithmetic on the CPU in double: the mean interpolated as PlantField.compute's MeanAt does, the wash grid's
        // node as its wash level reads it, then PlantAir.Bend. False where the kernel holds no bend.
        static bool Port(PlantField field, int level, int band, in PlantMechanics m, double x, double z, PlantWash fine, PlantWash coarse,
            IWorldQuery world, WindField wind, PlantAir frame, out PlantBend bend, out bool atNode, out double ground, out PlantMean used)
        {
            bend = default;
            used = default;
            atNode = false;
            ground = Ground(world, x, z);
            int grid = PlantField.GridOf(level, band);
            float blend = field.MeanBlend(grid);
            double elevation = PlantBands.Elevation(band);
            var sum = new Acc();
            double total = 0;
            for (int snap = 0; snap < 2; snap++)
            {
                double weight = snap == 0 ? 1 - blend : blend;
                Vector4 g = field.MeanBox(grid, snap);
                var air = new PlantAir(wind);
                air.Begin(field.MeanTime(grid, snap));
                int n = (int)g.w;
                double u = Math.Min(Math.Max((x - g.x) / g.z, 0), n - 1), v = Math.Min(Math.Max((z - g.y) / g.z, 0), n - 1);
                int i0 = Math.Min((int)Math.Floor(u), n - 2), j0 = Math.Min((int)Math.Floor(v), n - 2);
                double fu = u - i0, fv = v - j0;
                double ru = (x - g.x) / g.z, rv = (z - g.y) / g.z; // unclamped: a texel past the grid's last node takes the edge's
                atNode |= snap == 0 && ru >= -1e-9 && rv >= -1e-9 && ru <= n - 1 + 1e-9 && rv <= n - 1 + 1e-9 && (fu < 1e-9 || fu > 1 - 1e-9) && (fv < 1e-9 || fv > 1 - 1e-9);
                var s = new Acc();
                double valid = 0, best = -1;
                for (int c = 0; c < 4; c++)
                {
                    double w = ((c & 1) == 0 ? 1 - fu : fu) * ((c >> 1) == 0 ? 1 - fv : fv);
                    double nx = g.x + (i0 + (c & 1)) * g.z, nz = g.y + (j0 + (c >> 1)) * g.z;
                    double h = Ground(world, nx, nz);
                    if (!(h > double.NegativeInfinity)) continue;
                    air.Mean(m, new Double3(nx, h + elevation, nz), h, out PlantMean mean);
                    s.Add(mean, w);
                    valid += w;
                    if (w > best) { best = w; s.Strip = air.StripOf(mean); s.WakeInverse = mean.WakeInverse; }
                    s.Lambda = mean.Lambda;
                }
                if (!(valid > 0)) continue;
                sum.Add(s, weight / valid, weight);
                total += weight;
            }
            if (!(total > 0.5)) return false;
            PlantMean interpolated = sum.Mean(1 / total, frame);
            // The wake eddies where the grid reads the wake grid's cells: their share and scale at the point itself, the plant's own
            // (PlantAir.Mean's; they do not move with the time).
            if (field.WakeCells(grid) && !(interpolated.StripShare > 0))
            {
                frame.Mean(m, new Double3(x, ground + elevation, z), ground, out PlantMean here);
                interpolated.Wake = here.Wake;
                interpolated.WakeInverse = here.WakeInverse;
            }
            Double3 washAir = default;
            PlantBend made = default;
            if (level < 2)
            {
                PlantWash w = level == 0 ? fine : coarse;
                if (!w.Bend(band, x, z, 0, out PlantBend was, out washAir) || !w.Bend(band, x, z, 1, out PlantBend now, out _)) return false;
                double f = field.WashFraction(level);
                made = new PlantBend { X = was.X + (now.X - was.X) * f, Z = was.Z + (now.Z - was.Z) * f };
                interpolated.LoadY = ground + elevation + m.LoadPoint;
            }
            used = interpolated;
            frame.Bend(m, interpolated, x, z, washAir, made, out bend);
            return true;
        }

        static double Ground(IWorldQuery world, double x, double z)
        {
            var p = new[] { new XZ(x, z) };
            var g = new GroundSample[1];
            world.SampleGround(p, g);
            return g[0].Height;
        }

        // A weighted sum of means, as the kernel's MeanAt: the strip and the wake's scale the nearest node's.
        sealed class Acc
        {
            Double3 _air;
            double _loadY, _lx, _lz, _wx, _wz, _xx, _xz, _zz, _yy, _xy, _zy, _open, _strip, _wake;
            public int Strip = -1;
            public double Lambda, WakeInverse;

            public void Add(in PlantMean m, double w)
            {
                _air = _air + m.Air * w;
                _loadY += w * m.LoadY;
                _lx += w * m.Lean * m.WayX;
                _lz += w * m.Lean * m.WayZ;
                _wx += w * m.WayX;
                _wz += w * m.WayZ;
                _xx += w * m.XX; _xz += w * m.XZ; _zz += w * m.ZZ; _yy += w * m.YY; _xy += w * m.XY; _zy += w * m.ZY;
                _open += w * m.Open;
                _strip += w * m.StripShare;
                _wake += w * m.Wake;
            }

            // A snapshot's normalised sum, weighted into the frame's (k = weight / valid).
            public void Add(Acc s, double k, double weight)
            {
                _air = _air + s._air * k;
                _loadY += k * s._loadY; _lx += k * s._lx; _lz += k * s._lz; _wx += k * s._wx; _wz += k * s._wz;
                _xx += k * s._xx; _xz += k * s._xz; _zz += k * s._zz; _yy += k * s._yy; _xy += k * s._xy; _zy += k * s._zy;
                _open += k * s._open; _strip += k * s._strip; _wake += k * s._wake;
                if (weight >= 0.5)
                {
                    Strip = s.Strip;
                    WakeInverse = s.WakeInverse;
                }
                Lambda = s.Lambda;
            }

            public PlantMean Mean(double inv, PlantAir air)
            {
                double lx = _lx * inv, lz = _lz * inv, lean = Math.Sqrt(lx * lx + lz * lz), wx = _wx * inv, wz = _wz * inv;
                double way = Math.Sqrt(wx * wx + wz * wz);
                var m = new PlantMean
                {
                    Air = _air * inv, LoadY = _loadY * inv, Lean = lean,
                    WayX = lean > 0 ? lx / lean : way > 0 ? wx / way : 1, WayZ = lean > 0 ? lz / lean : way > 0 ? wz / way : 0,
                    XX = _xx * inv, XZ = _xz * inv, ZZ = _zz * inv, YY = _yy * inv, XY = _xy * inv, ZY = _zy * inv,
                    Open = _open * inv, StripShare = _strip * inv, Wake = _wake * inv, WakeInverse = WakeInverse, Lambda = Lambda,
                };
                air.SetStrip(ref m, Strip);
                return m;
            }
        }
    }
}
