using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The farmland's stem field (w9-village-physics 2.1, design.md "## 2.1"; MapWorldStems.cs): StemsNear against the
    // package's grass counts and the plants' numbers read here, the circle's area against closed forms, and the query's rules
    // (order, overflow, the map's edge, the height, 0 B, threads).
    public sealed class StemFieldTests
    {
        private const double Side = 1000.0 / 1024; // the grass grid's cell, m
        private static byte[] _counts;
        private static List<object> _layers;
        private static Dictionary<string, object> _prefabs;

        private static MapWorld World => FarmlandTests.World;

        private static byte Count(int layer, int row, int column)
        {
            _counts ??= File.ReadAllBytes(Path.Combine(MapWorldTests.Root, "maps/farmland/grass/map.raw"));
            return _counts[layer * 1024 * 1024 + row * 1024 + column];
        }

        private static Dictionary<string, object> Obj(object n) => (Dictionary<string, object>)n;

        private static List<object> Layers =>
            _layers ??= (List<object>)Obj(Obj(TestJson.Parse(File.ReadAllText(Path.Combine(MapWorldTests.Root, "maps/farmland/map.json"))))["grass"])["layers"];

        private static Dictionary<string, object> Layer(int layer) => Obj(Layers[layer]);

        private static double StemsPerInstance(int layer)
        {
            _prefabs ??= Obj(Obj(Obj(TestJson.Parse(File.ReadAllText(Path.Combine(MapWorldTests.Root, MapWorld.PlantNumbers))))["grass"])["prefabs"]);
            return (double)Obj(_prefabs[(string)Layer(layer)["prefab"]])["stems"];
        }

        private static XZ Centre(int row, int column) => new XZ((column + 0.5) * Side, 1000 - (row + 0.5) * Side);

        private static StemPatch[] Near(XZ centre, double radius, double height, out int n, int buffer = 64)
        {
            var found = new StemPatch[buffer];
            n = World.StemsNear(centre, radius, height, found);
            return found;
        }

        [Test]
        public void AtTheGround_ACellHoldsItsCountTimesStemsPerInstanceOverItsArea()
        {
            // The audit's densest tall cell (768.07, 917.48): row 84, column 786; a disc inside it.
            StemPatch[] found = Near(Centre(84, 786), 0.127, 0, out int n);
            int k = 0;
            for (int l = 0; l < 14; l++)
            {
                byte c = Count(l, 84, 786);
                if (c == 0)
                    continue;
                Assert.That(found[k].Layer, Is.EqualTo(l));
                Assert.That(found[k].Cell, Is.EqualTo(84 * 1024 + 786));
                Assert.That(found[k].Density, Is.EqualTo(c * StemsPerInstance(l) / (Side * Side)).Within(1e-9).Percent, "layer " + l);
                Assert.That(found[k].Area, Is.EqualTo(Math.PI * 0.127 * 0.127).Within(1e-12));
                k++;
            }
            Assert.That(n, Is.EqualTo(k));
            Assert.That(n, Is.EqualTo(11), "its 11 drawn layers");
        }

        [Test]
        public void TheKindIsTheGroupsState_AndTheHeightsTheDrawnTops()
        {
            StemPatch[] found = Near(Centre(84, 786), 0.127, 0, out int n);
            for (int k = 0; k < n; k++)
            {
                Dictionary<string, object> layer = Layer(found[k].Layer);
                string group = (string)layer["group"];
                StemKind want = group.Contains("Green") ? StemKind.GreenBlade : group.Contains("Dry") ? StemKind.DryCulm : StemKind.BurntCulm;
                Assert.That(found[k].Kind, Is.EqualTo(want), group);
                // TestJson reads by double.Parse, which may be an ulp off DetJson's: within 1e-12 m.
                double top = (double)((List<object>)layer["mesh_y_m"])[1];
                var scale = (List<object>)layer["height_scale"];
                Assert.That(found[k].HeightMin, Is.EqualTo(top * (double)scale[0]).Within(1e-12));
                Assert.That(found[k].HeightMax, Is.EqualTo(top * (double)scale[1]).Within(1e-12));
            }
        }

        [Test]
        public void WithHeight_TheStemsNeverRise_AndNoneReachPastTheTallestTop()
        {
            XZ c = Centre(84, 786);
            var last = new double[14];
            for (int l = 0; l < 14; l++)
                last[l] = double.PositiveInfinity;
            for (double h = 0; h <= 2; h += 0.005)
            {
                var now = new double[14];
                StemPatch[] found = Near(c, 0.127, h, out int n);
                for (int k = 0; k < n; k++)
                {
                    now[found[k].Layer] = found[k].Density;
                    Assert.That(h, Is.LessThan(found[k].HeightMax), "a stem reaching past its layer's tallest top");
                }
                for (int l = 0; l < 14; l++)
                {
                    Assert.That(now[l], Is.LessThanOrEqualTo(last[l]), $"layer {l} rose at {h} m");
                    last[l] = now[l];
                }
            }
            Near(c, 0.127, -1, out int below);
            Near(c, 0.127, 0, out int ground);
            Assert.That(below, Is.EqualTo(ground), "a height below the ground counts every stem");
        }

        [Test]
        public void ABareCell_AndOffTheMap_HoldNoStems()
        {
            Near(new XZ(229.98046875, 257.32421875), 0.127, 0, out int bare); // a tilled cell with no grass about it
            Assert.That(bare, Is.EqualTo(0));
            Near(new XZ(1000.2, 908.69), 0.15, 0, out int off); // past the east edge: the surround's grass is not exported
            Assert.That(off, Is.EqualTo(0));
            Near(new XZ(-50, 500), 30, 0, out int far);
            Assert.That(far, Is.EqualTo(0));
            Near(Centre(84, 786), 0, 0, out int point);
            Assert.That(point, Is.EqualTo(0), "a circle of no radius has no area");
        }

        [Test]
        public void TheCirclesArea_IsSplitExactlyAmongTheCells()
        {
            // A circle on a cell corner: four quarters; one across the east edge: only the map's part.
            double r = 0.3;
            XZ corner = new XZ(786 * Side, 1000 - 84 * Side);
            var cells = new System.Collections.Generic.Dictionary<int, double>();
            StemPatch[] found = Near(corner, r, 0, out int n);
            for (int k = 0; k < n; k++)
                cells[found[k].Cell] = found[k].Area;
            foreach (double a in cells.Values)
                Assert.That(a, Is.EqualTo(Math.PI * r * r / 4).Within(1e-12));

            XZ edge = new XZ(1000 - 0.1, 908.69140625); // column 1023 holds grass there; its circle reaches 0.2 m past the edge
            found = Near(edge, r, 0, out n);
            double inside = 0;
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int k = 0; k < n; k++)
                if (seen.Add(found[k].Cell))
                    inside += found[k].Area;
            // The segment of the circle beyond x = 0.1 from its centre is r²·acos(d/r) − d·√(r² − d²).
            double d = 0.1, cut = r * r * Math.Acos(d / r) - d * Math.Sqrt(r * r - d * d);
            Assert.That(seen.Count, Is.GreaterThan(0));
            Assert.That(inside, Is.LessThanOrEqualTo(Math.PI * r * r - cut + 1e-12), "no area past the map's edge");
        }

        [Test]
        public void Results_ComeInCanonicalOrder_AndOverflowReportsTheTrueCount()
        {
            StemPatch[] all = Near(new XZ(768.07, 917.48), 1.5, 0, out int n, 256);
            Assert.That(n, Is.GreaterThan(16));
            for (int k = 1; k < n; k++)
                Assert.That(all[k].Cell > all[k - 1].Cell || all[k].Cell == all[k - 1].Cell && all[k].Layer > all[k - 1].Layer, "canonical order at " + k);
            StemPatch[] few = Near(new XZ(768.07, 917.48), 1.5, 0, out int m, 16);
            Assert.That(m, Is.EqualTo(n));
            for (int k = 0; k < 16; k++)
                Assert.That((few[k].Cell, few[k].Layer, few[k].Density, few[k].Area), Is.EqualTo((all[k].Cell, all[k].Layer, all[k].Density, all[k].Area)));
        }

        [Test]
        public void Threads_GetTheSameBits()
        {
            var s = new Random(21);
            var centres = new XZ[2000];
            var radii = new double[2000];
            var heights = new double[2000];
            for (int i = 0; i < centres.Length; i++)
            {
                centres[i] = new XZ(740 + 60 * s.NextDouble(), 890 + 60 * s.NextDouble());
                radii[i] = 0.01 + 1.5 * s.NextDouble();
                heights[i] = 2 * s.NextDouble();
            }
            long Run()
            {
                var buffer = new StemPatch[128];
                long hash = 17;
                for (int i = 0; i < centres.Length; i++)
                {
                    int n = World.StemsNear(centres[i], radii[i], heights[i], buffer);
                    hash = hash * 31 + n;
                    for (int k = 0; k < Math.Min(n, buffer.Length); k++)
                        hash = (hash * 31 + BitConverter.DoubleToInt64Bits(buffer[k].Density)) * 31 + BitConverter.DoubleToInt64Bits(buffer[k].Area);
                }
                return hash;
            }
            long one = Run();
            var four = new long[4];
            Parallel.For(0, 4, t => four[t] = Run());
            Assert.That(four, Is.All.EqualTo(one));
        }

        [Test]
        public void StemsNear_AllocatesNothing()
        {
            var found = new StemPatch[64];
            void Step()
            {
                for (int i = 0; i < 4; i++)
                    World.StemsNear(new XZ(766 + 0.3 * i, 916 + 0.2 * i), 0.127, 0.25 * i, found);
            }
            Step();
            long before = AllocProbe.Before();
            for (int k = 0; k < 1000; k++)
                Step();
            long allocated = AllocProbe.After(before);
            if (allocated != 0)
                Assert.Fail($"{allocated} B over 1000 steps of four discs" + AllocProbe.Report());
        }

        [Test]
        public void TheFloorAndTheTestRange_GrowNoStems()
        {
            var found = new StemPatch[4];
            Assert.That(new FloorWorld(0, "meadow_sod").StemsNear(new XZ(0, 0), 5, 0, found), Is.EqualTo(0));
            Assert.That(MapWorldTests.World.StemsNear(new XZ(0, 0), 50, 0, found), Is.EqualTo(0));
        }
    }
}
