using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind 1.10, the wind inside a cut (design 10, D-071): the cavity flow on synthetic straight cuts, the field given
    // its floor as WakeFieldTests gives it (Begin's ground: the floor's height, as the core's SoilUnder reads it). A steady
    // preset (σ 0, no meander, no shift, the strength fixed) makes the field's mean one vector, so the cut's flow is read
    // without eddies. Each scenario's known-wrong models are test-side: model 26's open profile from the floor (the field
    // without cuts over the floor's height), model 19's still hole (the floor's −∞), and one 2-D cavity on the wind's line.
    public sealed class CavityTests
    {
        const double DegToRad = Math.PI / 180;
        const ulong Seed = 7;

        // Severe's preset at a fixed strength (m/s at 35 m), σ 0, no meander and no shift: the mean is one vector.
        static WindPreset Steady(double speed)
        {
            WindPreset p = WindPreset.Of(WindLevel.Severe);
            p.Intensity = 0;
            p.MeanLo = p.MeanHi = speed;
            p.MeanderSigmaDeg = 0;
            p.ShiftRate = 1e-12;
            return p;
        }

        // The way the steady preset's wind blows (a unit vector), from the seed's drawn bearing.
        static Double3 Blowing(WindPreset p) => WindField.Toward(new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(Seed), p).FromDeg, 1);

        // A straight cut centred on the world's origin, its axis at `angleDeg` to the wind (90: across), y 0 at the lip,
        // closed at both ends (the end walls through its end points).
        static CutSpec Straight(Double3 wind, double angleDeg, double length, double width, double depth)
        {
            double a = angleDeg * DegToRad, c = Math.Cos(a), s = Math.Sin(a);
            var axis = new Double3(wind.X * c - wind.Z * s, 0, wind.X * s + wind.Z * c);
            return new CutSpec { CentreLine = new[] { axis * (-length / 2), axis * (length / 2) }, Width = width, Depth = depth };
        }

        static WindField Field(WindPreset p, params CutSpec[] cuts) =>
            new WindField(new WindSetup { Level = WindLevel.Severe, Cuts = cuts.Length == 0 ? null : cuts }, new FlightRandom(Seed), p);

        // The point at arc s from the cut's first end, n across its axis (along the frame's n̂) and y.
        static Double3 At(CutSpec cut, double s, double n, double y)
        {
            Double3 a = cut.CentreLine[0], b = cut.CentreLine[1], t = (b - a) * (1 / (b - a).Length());
            return new Double3(a.X + t.X * s - t.Z * n, y, a.Z + t.Z * s + t.X * n);
        }

        static double Horizontal(Double3 v) => Math.Sqrt(v.X * v.X + v.Z * v.Z);

        [Test]
        public void AcrossADeepCutOneVortexTurnsUnderTheLip()
        {
            // E01's section: 0.8 m wide at the lip, 1.5 m deep (design 10's first readings), 24 m long, the wind across at
            // Severe's centre.
            const double W = 0.8, D = 1.5, L = 24;
            WindPreset p = Steady(WindPreset.SevereCentre);
            Double3 wind = Blowing(p);
            CutSpec cut = Straight(wind, 90, L, W, D);
            WindField field = Field(p, cut), open = Field(p);
            field.Begin(0, At(cut, L / 2, 0, -1), -D);
            open.Begin(0, default, 0);
            CutFrame f = field.Cut;
            Assert.That(f.Active && !f.Covered && f.Depth == D && f.Width == W, "the frame: the cut whose opening holds the origin");
            double sign = Math.Sign(f.DriveAcross); // the upwind wall at n = −sign·W/2
            double U = Math.Sqrt(f.DriveAcross * f.DriveAcross + f.DriveAlong * f.DriveAlong);

            // Design 10's first readings: U_o at h_o 0.8 m, the cell's A, the middle of the opening at 1.0 and 0.5 m over
            // the floor against the plane at the same heights.
            double mid10 = Horizontal(field.At(At(cut, L / 2, 0, -D + 1.0))), mid05 = Horizontal(field.At(At(cut, L / 2, 0, -D + 0.5)));
            double plane10 = Horizontal(open.At(new Double3(0, 1.0, 0))), plane05 = Horizontal(open.At(new Double3(0, 0.5, 0)));
            TestContext.Out.WriteLine($"E01's section across at {p.MeanLo} m/s: U_o {U:F3} m/s at h_o {f.DriveHeight:F2} m (A {Cavity.CellStrength * U:F3} m/s); "
                + $"mid-opening at 1.0 / 0.5 m over the floor {mid10:F4} / {mid05:F4} m/s, the plane {plane10:F3} / {plane05:F3} m/s, "
                + $"{mid10 / plane10:F4} / {mid05 / plane05:F4} of it");
            Assert.That(Math.Round(U, 1), Is.EqualTo(2.9), "U_o 2.9 m/s");
            Assert.That(Math.Round(Cavity.CellStrength * U, 2), Is.EqualTo(0.96), "A 0.96 m/s");
            Assert.That(Math.Round(mid10, 2), Is.EqualTo(0.10));
            Assert.That(Math.Round(mid05, 2), Is.EqualTo(0.19));
            Assert.That(Math.Round(plane10, 2), Is.EqualTo(3.37));
            Assert.That(Math.Round(plane05, 2), Is.EqualTo(1.78));
            Assert.That(Math.Round(mid10 / plane10, 2), Is.EqualTo(0.03));
            Assert.That(Math.Round(mid05 / plane05, 2), Is.EqualTo(0.11));

            // The design's maxima at any bearing anywhere in the opening (reported): at 1.0 and 0.5 m over the floor, and
            // 0.1 m under the lip.
            double max10 = 0, max05 = 0, maxLip = 0;
            for (double angle = 0; angle <= 90; angle += 2)
            {
                CutSpec c = Straight(wind, angle, L, W, D);
                WindField g = Field(p, c);
                g.Begin(0, At(c, L / 2, 0, -1), -D);
                for (double s = 0; s <= L; s += 0.1)
                    for (double n = -W / 2; n <= W / 2 + 1e-9; n += W / 16)
                    {
                        max10 = Math.Max(max10, Horizontal(g.At(At(c, s, n, -D + 1.0))));
                        max05 = Math.Max(max05, Horizontal(g.At(At(c, s, n, -D + 0.5))));
                        maxLip = Math.Max(maxLip, Horizontal(g.At(At(c, s, n, -0.1))));
                    }
            }
            TestContext.Out.WriteLine($"  at any bearing, anywhere in the opening: at most {max10:F3} m/s at 1.0 m ({max10 / plane10:F3} of the plane), "
                + $"{max05:F3} m/s at 0.5 m ({max05 / plane05:F3}); 0.1 m under the lip up to {maxLip:F3} m/s");

            // One vortex: with the wind 0.1 m under the lip, against it from 0.5 m under the lip to the floor, up the upwind
            // wall and down the downwind wall; and the known-wrong, model 26's open profile from the floor.
            foreach (bool wrong in new[] { false, true })
            {
                WindField w = wrong ? Field(p) : field;
                w.Begin(0, At(cut, L / 2, 0, -1), -D);
                int with = 0, against = 0, top = 0, deep = 0;
                double worst = double.NegativeInfinity;
                for (double n = -0.35; n <= 0.351; n += 0.05)
                {
                    top++;
                    if (Double3.Dot(w.At(At(cut, L / 2, n, -0.1)), wind) > 0) with++;
                    for (double eta = 0.5; eta < D; eta += 0.1)
                    {
                        deep++;
                        double u = Double3.Dot(w.At(At(cut, L / 2, n, -eta)), wind);
                        if (u < 0) against++;
                        worst = Math.Max(worst, u);
                    }
                }
                double up = w.At(At(cut, L / 2, -sign * 0.36, -0.75)).Y, down = w.At(At(cut, L / 2, sign * 0.36, -0.75)).Y;
                double ratio = Horizontal(w.At(At(cut, L / 2, 0, -D + 0.5))) / plane05;
                TestContext.Out.WriteLine($"  {(wrong ? "known-wrong, the open profile from the floor (model 26)" : "the cavity")}: with the wind 0.1 m under the lip "
                    + $"{with} of {top}, against it from 0.5 m under the lip to the floor {against} of {deep} (fastest along the wind {worst:F3} m/s); "
                    + $"w by the upwind wall {up:+0.000;-0.000}, by the downwind wall {down:+0.000;-0.000} m/s; 0.5 m over the floor {ratio:F3} of the plane");
                if (!wrong)
                {
                    Assert.That(with, Is.EqualTo(top), "with the wind under the lip");
                    Assert.That(against, Is.EqualTo(deep), "against it below");
                    Assert.That(up, Is.GreaterThan(0), "up the upwind wall");
                    Assert.That(down, Is.LessThan(0), "down the downwind wall");
                    Assert.That(ratio, Is.LessThan(0.25));
                }
                else
                {
                    Assert.That(against, Is.EqualTo(0), "downwind at every depth");
                    Assert.That(ratio, Is.EqualTo(1).Within(1e-12), "the plane's at the same height over the floor");
                }
            }
        }

        [Test]
        public void ACutsRegimeIsItsLengthOverItsDepth()
        {
            // Straight cuts 1 m deep and long, the wind across them at Severe's centre, read 0.4 m over the floor along the
            // wind every 0.001 m from the upwind wall: one cell (reversed across the whole cut) up to L/D 5.5, a bubble
            // reattaching on the floor from 6 (Hotta et al. 1998's X_R at 6, 8 and 10, the step's beyond).
            const double D = 1, L = 400, eta = 0.6;
            WindPreset p = Steady(WindPreset.SevereCentre);
            Double3 wind = Blowing(p);
            var widths = new[] { 0.5, 1, 2, 4, 5, 5.5, 6, 8, 10, 20 };
            var expected = new Dictionary<double, double> { { 6, 5.2 }, { 8, 5.8 }, { 10, 6.8 }, { 20, 7.0 } };
            foreach (string model in new[] { "cavity", "open profile from the floor (model 26)", "still hole (model 19)" })
                foreach (double W in widths)
                {
                    CutSpec cut = Straight(wind, 90, L, W, D);
                    WindField field = model == "cavity" ? Field(p, cut) : Field(p);
                    field.Begin(0, At(cut, L / 2, 0, -0.5), model.StartsWith("still") ? double.NegativeInfinity : -D);
                    double sign = model == "cavity" ? Math.Sign(field.Cut.DriveAcross) : Math.Sign(Double3.Dot(wind, new Double3(-(cut.CentreLine[1] - cut.CentreLine[0]).Z, 0, (cut.CentreLine[1] - cut.CentreLine[0]).X)));
                    int reversed = 0, count = 0;
                    double turn = double.NaN;
                    bool back = false;
                    for (int i = 1; i < (int)Math.Round(W / 0.001); i++)
                    {
                        double xi = i * 0.001;
                        double u = Double3.Dot(field.At(At(cut, L / 2, sign * (xi - W / 2), -eta)), wind);
                        count++;
                        if (u < 0) { reversed++; back = true; turn = double.NaN; }
                        else if (back && u > 0 && double.IsNaN(turn)) turn = xi / D;
                    }
                    bool cell = reversed == count;
                    TestContext.Out.WriteLine($"{model}, L/D {W / D}: reversed at {reversed} of {count} points"
                        + (double.IsNaN(turn) ? "" : $", forward again from {turn:F3} D") + (expected.ContainsKey(W) ? $" (Hotta: {expected[W]})" : ""));
                    if (model == "cavity")
                    {
                        if (W <= 5.5) Assert.That(cell, "one cell, reversed across the whole cut, at L/D " + W);
                        else Assert.That(turn, Is.EqualTo(expected[W]).Within(0.1), "reattachment at L/D " + W);
                    }
                    else
                    {
                        Assert.That(reversed, Is.EqualTo(0), "no reversed floor flow: the known-wrong fails");
                        Assert.That(turn, Is.NaN);
                    }
                }
        }

        [Test]
        public void ObliqueWindDrivesTheCellAcrossAndTheFlowAlong()
        {
            // A straight 24 m cut of E01's section with closed ends, the wind at 90°, 60°, 30°, 10° and 2° to its axis; 0° for
            // the along flow's reference.
            const double W = 0.8, D = 1.5, L = 24;
            WindPreset p = Steady(WindPreset.SevereCentre);
            Double3 wind = Blowing(p);
            var points = new List<(double s, double n, double eta)>();
            foreach (double s in new[] { 1.0, 6, 12, 18, 23 })
                foreach (double n in new[] { -0.3, 0, 0.3 })
                    foreach (double eta in new[] { 0.05, 0.25, 0.5, 1.0, 1.4 })
                        points.Add((s, n, eta));
            Double3[] Read(double angle, out CutSpec cut, out WindField field)
            {
                cut = Straight(wind, angle, L, W, D);
                field = Field(p, cut);
                field.Begin(0, At(cut, L / 2, 0, -1), -D);
                var v = new Double3[points.Count];
                for (int i = 0; i < v.Length; i++) v[i] = field.At(At(cut, points[i].s, points[i].n, -points[i].eta));
                return v;
            }
            Double3[] across = Read(90, out CutSpec c90, out _), along = Read(0, out CutSpec c0, out _);
            Double3 t = (c90.CentreLine[1] - c90.CentreLine[0]) * (1 / L), nh = new Double3(-t.Z, 0, t.X);
            Double3 t0 = (c0.CentreLine[1] - c0.CentreLine[0]) * (1 / L);
            WindField plane = Field(p);
            plane.Begin(0, default, 0);
            double plane10 = Horizontal(plane.At(new Double3(0, 1.0, 0)));
            foreach (double angle in new[] { 90.0, 60, 30, 10, 2 })
            {
                Double3[] v = Read(angle, out CutSpec cut, out WindField field);
                double sin = Math.Sin(angle * DegToRad), cos = Math.Cos(angle * DegToRad), worst = 0;
                Double3 ta = (cut.CentreLine[1] - cut.CentreLine[0]) * (1 / L), na = new Double3(-ta.Z, 0, ta.X);
                for (int i = 0; i < v.Length; i++)
                {
                    double x = Double3.Dot(v[i], na), xe = sin * Double3.Dot(across[i], nh);
                    double a = Double3.Dot(v[i], ta), ae = cos * Double3.Dot(along[i], t0);
                    double y = v[i].Y, ye = sin * across[i].Y + cos * along[i].Y;
                    double scale = Math.Abs(xe) + Math.Abs(ae) + Math.Abs(sin * across[i].Y) + Math.Abs(cos * along[i].Y) + 1e-300;
                    worst = Math.Max(worst, Math.Max(Math.Abs(x - xe), Math.Max(Math.Abs(a - ae), Math.Abs(y - ye))) / scale);
                }
                // The along flow at 1.0 m over the floor down the middle, wall to wall.
                double most = 0, first = 0, last = 0;
                for (double s = 0; s <= L + 1e-9; s += 0.05)
                {
                    double u = Math.Abs(Double3.Dot(field.At(At(cut, s, 0, -(D - 1.0))), ta));
                    most = Math.Max(most, u);
                    if (s == 0) first = u;
                    last = u;
                }
                TestContext.Out.WriteLine($"{angle}° to the axis: the cross cell over sin, the along flow over cos, worst relative {worst:E1}; "
                    + $"the along flow at 1.0 m over the floor at most {most:F4} m/s ({most / plane10:F4} of the plane), {first:E1} and {last:E1} at the end walls");
                Assert.That(worst, Is.LessThan(1e-9), "the cross cell scales with sin, the along flow with cos");
                Assert.That(most / plane10, Is.LessThanOrEqualTo(0.1), "the slot's walls hold the along flow");
                Assert.That(first, Is.LessThan(1e-12), "0 at the upwind end wall");
                Assert.That(last, Is.LessThan(1e-12), "0 at the downwind end wall");
            }

            // The known-wrong: one 2-D cavity on the wind's line through the cut, L = W/sin θ, at 2°: past its reattachment
            // the floor's profile reads most of the plane at 1.0 m over the floor.
            {
                Read(2, out _, out WindField field);
                CutFrame f = field.Cut;
                double U = Math.Sqrt(f.DriveAcross * f.DriveAcross + f.DriveAlong * f.DriveAlong), line = W / Math.Sin(2 * DegToRad), most = 0;
                for (double xi = 0; xi <= line; xi += 0.01)
                {
                    Cavity.Cell(line, D, xi, D - 1.0, U, f.DriveHeight, p.Displacement, p.Roughness, out double u, out _, out _);
                    most = Math.Max(most, u);
                }
                TestContext.Out.WriteLine($"known-wrong, one cavity on the wind's line at 2° (L {line:F1} m): {most:F3} m/s at 1.0 m over the floor, {most / plane10:F3} of the plane");
                Assert.That(most / plane10, Is.GreaterThanOrEqualTo(0.4), "the known-wrong reads the floor's open profile");
            }
        }

        [Test]
        public void ACoveredCutIsStillAndTheOpenOneTakesTheirSharedPoints()
        {
            // A covered niche whose first end reaches across an open run (E01's niche_0): in the run's opening the run takes
            // the point; in the niche alone, still air under its roof and the lip as the ground above it.
            const double D = 1.5;
            WindPreset p = Steady(WindPreset.SevereCentre);
            Double3 wind = Blowing(p);
            CutSpec run = Straight(wind, 90, 24, 0.94, D);
            Double3 t = (run.CentreLine[1] - run.CentreLine[0]) * (1.0 / 24), n = new Double3(-t.Z, 0, t.X);
            // The niche: from the run's far wall outward, 1.1 m, its first end 0.47 m back across the run's opening.
            var niche = new CutSpec
            {
                CentreLine = new[] { n * 0.47, n * 1.57 }, Width = 0.8, Depth = 1.35, EndFirst = 0.47, Covered = true,
            };
            WindField field = Field(p, niche, run), lip = Field(p);
            field.Begin(0, n * 0.2 + new Double3(0, -0.5, 0), -D);
            Assert.That(field.Cut.Active && !field.Cut.Covered, "the shared point is the open run's");
            Assert.That(Horizontal(field.At(n * 0.2 + new Double3(0, -0.8, 0))), Is.GreaterThan(0.01), "the run's cell");
            field.Begin(0, n * 1.0 + new Double3(0, -0.5, 0), -1.35);
            lip.Begin(0, n * 1.0, 0);
            Assert.That(field.Cut.Active && field.Cut.Covered, "the niche alone");
            Assert.That(field.At(n * 1.0 + new Double3(0, -0.8, 0)), Is.EqualTo(default(Double3)), "still under the roof");
            for (double y = 0; y <= 3; y += 0.25)
                Assert.That(field.At(n * 1.0 + new Double3(0, y, 0)), Is.EqualTo(lip.At(n * 1.0 + new Double3(0, y, 0))), "the lip as the ground above it");
        }

        [Test]
        public void StillAndSteadyAirHaveNoCuts()
        {
            var cut = new CutSpec { CentreLine = new[] { new Double3(0, 0, 0), new Double3(10, 0, 0) }, Width = 1, Depth = 1 };
            foreach (WindLevel level in new[] { WindLevel.Still, WindLevel.Steady })
                Assert.Throws<ArgumentException>(() => new WindField(new WindSetup { Level = level, SteadySpeed = 5, Cuts = new[] { cut } }, new FlightRandom(1)));
            Assert.Throws<ArgumentException>(() => new WindField(new WindSetup { Level = WindLevel.Calm, Cuts = new CutSpec[0] }, new FlightRandom(1)));
            var bad = new CutSpec { CentreLine = new[] { new Double3(0, 0, 0), new Double3(0, 0, 0) }, Width = 1, Depth = 1 };
            Assert.Throws<ArgumentException>(() => new WindField(new WindSetup { Level = WindLevel.Calm, Cuts = new[] { bad } }, new FlightRandom(1)));
        }

        [Test]
        public void TheHeaderRecordsTheCuts()
        {
            Assert.That(FieldNames(typeof(CutSpec)), Is.EquivalentTo(LogHeader.CutFieldsLogged));
            FlightSetup setup = WindTargets.BenchFlight(WindLevel.Severe, 5);
            var cut = new CutSpec
            {
                CentreLine = new[] { new Double3(1, 0.1, 2), new Double3(4, 0.2, 6), new Double3(9, 0.15, 6.5) }, Width = 0.94, Depth = 1.5,
                EndFirst = 0.25, EndLast = 0.3, Covered = true,
            };
            setup.Wind.Cuts = new[] { cut };
            var core = new FlightCore(setup);
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            LogHeader back = LogHeader.Read(new MemoryStream(h.Encode()));
            Assert.That(back.Unknown, Is.Empty);
            Assert.That(back.Wind.Cuts, Has.Length.EqualTo(1));
            CutSpec c = back.Wind.Cuts[0];
            Assert.That(c.CentreLine, Is.EqualTo(cut.CentreLine));
            Assert.That(new[] { c.Width, c.Depth, c.EndFirst, c.EndLast }, Is.EqualTo(new[] { cut.Width, cut.Depth, cut.EndFirst, cut.EndLast }));
            Assert.That(c.Covered, Is.True);
            Assert.That(LogHeader.Read(new MemoryStream(LogHeaderWithout(h))).Wind.Cuts, Is.Null, "none: a count of 0");
        }

        static byte[] LogHeaderWithout(LogHeader h)
        {
            h.Wind.Cuts = null;
            return h.Encode();
        }

        static string[] FieldNames(Type t) =>
            Array.ConvertAll(t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance), f => f.Name);
    }
}
