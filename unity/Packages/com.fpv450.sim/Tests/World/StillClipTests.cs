using System;
using System.Collections.Generic;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// QA of the w9 step budget's first pass (0db885d2): a still capsule's query (the fiber's) tests an item, a tree node or a
    /// shape only where its bounds also come within the pad of the core's segment (MapWorld.Meets). Its claim: the same
    /// contacts, in the same order, with the same bits. Each query is checked against ShapeContact over every placed and
    /// runtime shape in canonical order (the same bounds check and sweep, one shape at a time: no grid, no tree, no clip), on
    /// still segments 0.2–30 m long, of the fiber's radius to a pole's, at margins 0–0.2 m, through the farmland's ruins and
    /// trees and over the test range; half of them at the edge where a segment's query first meets a shape, bisected along
    /// a random line, with the states on both sides checked.
    /// </summary>
    public sealed class StillClipTests
    {
        const int Queries = 600;

        [Test]
        public void AStillSegmentMeetsWhatEveryShapeAloneMeets()
        {
            int checkedQueries = 0, contacts = 0, edges = 0, clipped = 0;
            foreach (string id in new[] { "farmland", "test-range" })
            {
                MapWorld map = MapWorld.Load(RangeScenarios.Root, id);
                var aims = new List<Double3>();
                var shapes = new List<(int Obj, int Shape)>();
                var r = new Random(id.Length * 7919);
                for (int obj = 0; map.ObjectName(obj) != null; obj++)
                    for (int shape = 0; map.Geometry(obj, shape, out ShapeGeometry g); shape++)
                    {
                        if (g.Kind == ShapeKind.Wire) continue;
                        shapes.Add((obj, shape));
                        aims.Add(g.Center);
                    }
                var buf = new StaticContact[64];
                var brute = new List<StaticContact>();
                for (int q = 0; q < Queries; q++)
                {
                    double length = 0.2 * Math.Pow(150, r.NextDouble()), radius = r.Next(3) == 0 ? 0.05 * r.NextDouble() : 0.0005 + 0.0015 * r.NextDouble();
                    double margin = r.Next(4) == 0 ? 0 : 0.2 * r.NextDouble();
                    Double3 dir = Unit(r), aim = aims[r.Next(aims.Count)] + Unit(r) * r.NextDouble();
                    Double3 a = aim - dir * (length * r.NextDouble());
                    if (q % 2 == 1)
                    {
                        // At the edge: the segment's offset along a random line bisected to where its query first meets a shape.
                        Double3 line = Unit(r);
                        double lo = 0, hi = 2;
                        if (Count(map, a, dir, length, radius, margin, line, lo, buf) == 0 || Count(map, a, dir, length, radius, margin, line, hi, buf) > 0) continue;
                        for (int k = 0; k < 50; k++)
                        {
                            double mid = lo + (hi - lo) * 0.5;
                            if (!(mid > lo && mid < hi)) break;
                            if (Count(map, a, dir, length, radius, margin, line, mid, buf) > 0) lo = mid;
                            else hi = mid;
                        }
                        Compare(map, shapes, a - line * lo, dir, length, radius, margin, buf, brute, ref contacts);
                        Compare(map, shapes, a - line * hi, dir, length, radius, margin, buf, brute, ref contacts);
                        checkedQueries += 2;
                        edges++;
                        continue;
                    }
                    Compare(map, shapes, a, dir, length, radius, margin, buf, brute, ref contacts);
                    checkedQueries++;
                    if (length * length > (radius + margin + 1e-6) * (radius + margin + 1e-6)) clipped++;
                }
            }
            TestContext.Progress.WriteLine($"{checkedQueries} still queries ({edges} edges bisected, {clipped} of the random ones long enough to clip), " +
                $"{contacts} contacts, each the same bits as every shape's own ShapeContact");
            Assert.That(contacts, Is.GreaterThan(Queries), "the queries meet the world");
            Assert.That(edges, Is.GreaterThan(Queries / 4), "and its edges");
        }

        static int Count(MapWorld map, Double3 a, Double3 dir, double length, double radius, double margin, Double3 line, double s, StaticContact[] buf)
        {
            var c = new Capsule(a - line * s, a - line * s + dir * length, radius);
            return map.StaticContacts(c, c, margin, buf, QueryFilter.NoGround | QueryFilter.NoWires);
        }

        static void Compare(MapWorld map, List<(int Obj, int Shape)> shapes, Double3 a, Double3 dir, double length, double radius, double margin,
            StaticContact[] buf, List<StaticContact> brute, ref int contacts)
        {
            var c = new Capsule(a, a + dir * length, radius);
            int n = map.StaticContacts(c, c, margin, buf, QueryFilter.NoGround | QueryFilter.NoWires);
            brute.Clear();
            foreach ((int obj, int shape) in shapes)
                if (map.ShapeContact(obj, shape, c, c, margin, out StaticContact contact)) brute.Add(contact);
            Assert.That(n, Is.EqualTo(brute.Count), $"contacts of the still segment {a} + {dir} × {length} m, r {radius}, margin {margin}");
            for (int k = 0; k < n && k < buf.Length; k++)
                Assert.That(Same(buf[k], brute[k]), $"contact {k} (object {brute[k].Object} shape {brute[k].Shape}) of {a} + {dir} × {length} m: the same bits");
            contacts += n;
        }

        static bool Same(in StaticContact a, in StaticContact b) =>
            Bits(a.Point.X, b.Point.X) && Bits(a.Point.Y, b.Point.Y) && Bits(a.Point.Z, b.Point.Z) && Bits(a.Normal.X, b.Normal.X)
            && Bits(a.Normal.Y, b.Normal.Y) && Bits(a.Normal.Z, b.Normal.Z) && Bits(a.Distance, b.Distance) && a.Object == b.Object
            && a.Shape == b.Shape && a.Material == b.Material && a.Surface == b.Surface && Bits(a.WireParam, b.WireParam) && Bits(a.Time, b.Time);

        static bool Bits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        static Double3 Unit(Random r)
        {
            var v = new Double3(Gauss(r), Gauss(r), Gauss(r));
            return v * (1 / v.Length());
        }

        static double Gauss(Random r) => Math.Sqrt(-2 * Math.Log(1 - r.NextDouble())) * Math.Cos(2 * Math.PI * r.NextDouble());
    }
}
