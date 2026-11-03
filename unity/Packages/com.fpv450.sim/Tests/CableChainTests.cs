using System;
using System.Diagnostics;
using System.IO;
using Fpv450.Sim.Tether;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // fiber-tether (p7d-tether 1.5): the cable chain against published cable statics and dynamics. "A hanging span is a
    // catenary" (the elastic catenary, Irvine 1981 §2), Irvine's λ² and a taut span's first in-plane and out-of-plane
    // modes (Irvine and Caughey 1974; Irvine 1981 §3), "The capstan holds and slips" (Euler–Eytelwein), "Energy is never
    // gained" over 10 000 random chains, stability at the design's ω·dt (36 for a 0.1 m fiber segment, 9.6 for a 0.5 m
    // A-50 one), and the known-wrong models that must fail: an explicit step blows up, a rigid rod has no sag, a taut
    // string and an inextensible cable miss the in-plane mode. Then insertion and merging, 0 B and the pinned bits.
    public sealed class CableChainTests
    {
        const double Dt = 1e-3, G = 9.80665;
        static readonly Double3 Down = new Double3(0, -G, 0);

        // A-50 (L03_medium's wire record, unity/Assets/_Project/Art/Models/lines/L03/L03_medium.assetmeta.json): 0.135 kg/m,
        // EA 3 118 500 N; its span 40 m with 0.8 m of sag.
        const double A50Mass = 0.135, A50Stiffness = 3118500;

        static TetherTable _table;
        static TetherTable Table() => _table ??= TetherTable.Parse(File.ReadAllText(TestFiles.Find(TetherTable.RelativePath)));
        static double FiberMass => Table().Number("fiber.mass_per_m_kg");
        static double FiberStiffness => Table().Number("fiber.axial_stiffness_n");

        static Sym3 Iso(double s) => new Sym3(s, s, s, 0, 0, 0);

        static void Anchor(CableChain c, int i, double stiffness)
        {
            c[i].AnchorStiffness = stiffness;
            c[i].AnchorTarget = c[i].Position;
        }

        static double Asinh(double z) => z < 0 ? -Math.Log(-z + Math.Sqrt(z * z + 1)) : Math.Log(z + Math.Sqrt(z * z + 1));

        // ---- the elastic catenary (Irvine 1981, eq. 2.1–2.6): supports level, span l apart, unstretched length L,
        // weight w per unstretched metre. With V(p) = w·(p − L/2) the tension's vertical part along p,
        // x(p) = H·p/EA + (H/w)·[asinh(V(p)/H) − asinh(V(0)/H)],
        // y(p) = (V(0)·p + w·p²/2)/EA + (H/w)·[√(1 + (V(p)/H)²) − √(1 + (V(0)/H)²)],
        // and H from x(L) = l. ----

        static double CatenaryH(double w, double ea, double length, double span)
        {
            double lo = 1e-9, hi = 1e9;
            for (int i = 0; i < 300; i++)
            {
                double h = Math.Sqrt(lo * hi);
                double x = h * length / ea + 2 * h / w * Asinh(w * length / (2 * h));
                if (x > span) hi = h;
                else lo = h;
            }
            return Math.Sqrt(lo * hi);
        }

        static Double3 CatenaryAt(double p, double h, double w, double ea, double length)
        {
            double v0 = -w * length / 2, v = v0 + w * p;
            double x = h * p / ea + h / w * (Asinh(v / h) - Asinh(v0 / h));
            double y = (v0 * p + w * p * p / 2) / ea + h / w * (Math.Sqrt(1 + v * v / (h * h)) - Math.Sqrt(1 + v0 * v0 / (h * h)));
            return new Double3(x, y, 0);
        }

        // Nodes at arc lengths i·h along the parabola y = −depth·(1 − (2x/span − 1)²) whose length is `length`.
        static Double3[] OnParabola(double span, double length, double h, int n)
        {
            const int fine = 200000;
            double depth = 0, lo = 0, hi = length;
            double[] arc = new double[fine + 1];
            for (int it = 0; it < 80; it++)
            {
                depth = 0.5 * (lo + hi);
                if (ParabolaArc(span, depth, fine, arc) > length) hi = depth;
                else lo = depth;
            }
            ParabolaArc(span, depth, fine, arc);
            var x = new Double3[n];
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                double s = Math.Min(i * h, arc[fine]);
                while (k < fine - 1 && arc[k + 1] < s) k++;
                double f = (s - arc[k]) / (arc[k + 1] - arc[k]);
                double u = (k + f) / fine * span;
                x[i] = new Double3(u, -depth * (1 - Math.Pow(2 * u / span - 1, 2)), 0);
            }
            return x;
        }

        static double ParabolaArc(double span, double depth, int fine, double[] arc)
        {
            arc[0] = 0;
            double px = 0, py = 0;
            for (int k = 1; k <= fine; k++)
            {
                double u = span * k / fine, y = -depth * (1 - Math.Pow(2 * u / span - 1, 2));
                arc[k] = arc[k - 1] + Math.Sqrt((u - px) * (u - px) + (y - py) * (y - py));
                px = u;
                py = y;
            }
            return arc[fine];
        }

        // fiber-tether: "A hanging span is a catenary". 40 m of the fiber (its mass and EA from tether.json) between two
        // fixed points 30 m apart, released from rest on a parabola of the same length, settles (a test-only viscous
        // drag of 2 s⁻¹ per kilogram speeds the settling and leaves the statics alone) onto the elastic catenary.
        [Test]
        public void AHangingSpanIsACatenary()
        {
            double mu = FiberMass, ea = FiberStiffness, w = mu * G;
            const double length = 40, span = 30, h = 0.1;
            const int n = 401;
            var s = new double[n];
            for (int i = 0; i < n; i++) s[i] = i * h;
            var chain = new CableChain(n, mu, ea) { Gravity = Down };
            chain.Reset(OnParabola(span, length, h, n), s, n);
            Anchor(chain, 0, 1e6);
            Anchor(chain, n - 1, 1e6);
            for (int i = 0; i < n; i++) chain[i].Drag = Iso(2 * chain[i].Mass);
            double before = chain.Energy(out _);
            for (int step = 0; step < 15000; step++) chain.Step(Dt);
            double after = chain.Energy(out _);

            double hc = CatenaryH(w, ea, length, span), worst = 0, fastest = 0;
            for (int i = 0; i < n; i++)
            {
                Double3 d = chain[i].Position - CatenaryAt(s[i], hc, w, ea, length);
                worst = Math.Max(worst, d.Length());
                fastest = Math.Max(fastest, chain[i].Velocity.Length());
            }
            int mid = n / 2;
            Double3 seg = chain[mid].Position - chain[mid - 1].Position;
            double hChain = chain.Tension(mid - 1) * seg.X / seg.Length();
            double sag = -CatenaryAt(length / 2, hc, w, ea, length).Y;
            TestContext.WriteLine($"elastic catenary: H {hc:E6} N, sag {sag:F6} m; the chain: H {hChain:E6} N " +
                $"({(hChain / hc - 1) * 100:F4} %), worst node {worst * 1000:F4} mm from it at its own material point, " +
                $"fastest node {fastest:E2} m/s after 15 s; energy {before:E4} → {after:E4} J, clips {chain.Clips}, corrections {chain.Corrections}, safe steps {chain.SafeSteps}, wedges {chain.Wedges}");
            Assert.That(worst, Is.LessThanOrEqualTo(1e-3), "every node within 1 mm of the elastic catenary");
            Assert.That(Math.Abs(hChain / hc - 1), Is.LessThanOrEqualTo(0.01), "the horizontal tension within 1 %");
            Assert.That(chain.Wedges, Is.EqualTo(0));

            // The known-wrong rigid rod: straight between the supports, it has no sag, so its middle lies the sag from
            // the catenary's and it fails the scenario.
            Assert.That(sag, Is.GreaterThan(1000 * 1e-3), "a rigid rod is the sag off at mid-span");
        }

        // ---- a taut span (Irvine 1981 §3.3): λ² = (wL/H)²·L/(H·L_e/EA), L_e = ∫(ds/dx)³dx; the out-of-plane modes
        // ω̄ = nπ, the in-plane antisymmetric ω̄ = 2nπ, the first in-plane symmetric from tan(ω̄/2) = ω̄/2 − (4/λ²)(ω̄/2)³,
        // with ω̄ = ωL/√(H/m). ----

        sealed class Span
        {
            public CableChain Chain;
            public Double3[] Rest;
            public double H, LambdaSquared, Wave, Length;
        }

        // A span of `length` between level anchors, segments of `segment`, built on the parabola of `sag` with its rest
        // lengths shortened by the stretch H/EA it will carry, settled under a test drag and then left without it.
        static Span TautSpan(double mu, double ea, double length, double sag, double segment)
        {
            int n = (int)Math.Round(length / segment) + 1;
            double w = mu * G, hTarget = w * length * length / (8 * sag);
            var x = new Double3[n];
            var s = new double[n];
            for (int i = 0; i < n; i++)
            {
                double u = (double)i / (n - 1);
                x[i] = new Double3(u * length, -4 * sag * u * (1 - u), 0);
                if (i > 0) s[i] = s[i - 1] + (x[i] - x[i - 1]).Length() / (1 + hTarget / ea);
            }
            var chain = new CableChain(n, mu, ea) { Gravity = Down };
            chain.Reset(x, s, n);
            Anchor(chain, 0, 100 * ea / segment);
            Anchor(chain, n - 1, 100 * ea / segment);
            double omega = Math.PI / length * Math.Sqrt(hTarget / mu);
            for (int i = 0; i < n; i++) chain[i].Drag = Iso(2 * omega * chain[i].Mass);
            for (int step = 0; step < 8000; step++) chain.Step(Dt);
            for (int i = 0; i < n; i++)
            {
                chain[i].Drag = default;
                chain[i].Velocity = default;
            }
            for (int step = 0; step < 2000; step++) chain.Step(Dt);

            var span = new Span { Chain = chain, Rest = new Double3[n], Length = length };
            for (int i = 0; i < n; i++) span.Rest[i] = chain[i].Position;
            int mid = n / 2;
            Double3 seg = chain[mid].Position - chain[mid - 1].Position;
            span.H = chain.Tension(mid - 1) * seg.X / seg.Length();
            double le = 0;
            for (int j = 0; j + 1 < n; j++)
            {
                Double3 d = chain[j + 1].Position - chain[j].Position;
                double l = d.Length();
                le += l * l * l / (d.X * d.X);
            }
            span.LambdaSquared = Math.Pow(w * length / span.H, 2) * length / (span.H * le / ea);
            span.Wave = Math.Sqrt(span.H / mu);
            return span;
        }

        // The first root of tan x = x − (4/λ²)·x³ on (π/2, 3π/2): half the first symmetric in-plane ω̄.
        static double SymmetricHalf(double lambda2)
        {
            double lo = Math.PI / 2 + 1e-12, hi = 1.5 * Math.PI - 1e-12;
            for (int i = 0; i < 200; i++)
            {
                double m = 0.5 * (lo + hi);
                if (Math.Tan(m) - m + 4 / lambda2 * m * m * m < 0) lo = m;
                else hi = m;
            }
            return 0.5 * (lo + hi);
        }

        // The span set off in shape(u) along `along` (amplitude a), then its modal coordinate's rate measured from the
        // upward zero crossings over `seconds`, rad/s.
        static double Ring(Span span, Func<double, double> shape, Double3 along, double a, double seconds)
        {
            CableChain c = span.Chain;
            int n = c.Count;
            for (int i = 0; i < n; i++)
            {
                double u = (c[i].Position.X - span.Rest[0].X) / span.Length;
                c[i].Position = span.Rest[i] + along * (a * shape(u));
                c[i].Velocity = default;
            }
            double first = -1, last = -1, previous = 0;
            int crossings = 0, steps = (int)(seconds / Dt);
            for (int step = 0; step <= steps; step++)
            {
                double q = 0;
                for (int i = 0; i < n; i++)
                {
                    double u = (span.Rest[i].X - span.Rest[0].X) / span.Length;
                    q += c[i].Mass * Double3.Dot(c[i].Position - span.Rest[i], along) * shape(u);
                }
                if (step > 0 && previous < 0 && q >= 0)
                {
                    double t = (step - 1 + previous / (previous - q)) * Dt;
                    if (first < 0) first = t;
                    last = t;
                    crossings++;
                }
                previous = q;
                c.Step(Dt);
            }
            for (int i = 0; i < n; i++)
            {
                c[i].Position = span.Rest[i];
                c[i].Velocity = default;
            }
            return 2 * Math.PI * (crossings - 1) / (last - first);
        }

        [TestCase("fiber", 0.42, 0.5, TestName = "ATautSpanHasIrvinesModes(fiber, 1 % sag, λ² below 4π²)")]
        [TestCase("A-50", 0.8, 0.5, TestName = "ATautSpanHasIrvinesModes(A-50, L03_medium's 2 % sag, λ² above 4π²)")]
        public void ATautSpanHasIrvinesModes(string line, double sag, double segment)
        {
            bool fiber = line == "fiber";
            double mu = fiber ? FiberMass : A50Mass, ea = fiber ? FiberStiffness : A50Stiffness;
            const double length = 40;
            Span span = TautSpan(mu, ea, length, sag, segment);
            double l2 = span.LambdaSquared, scale = span.Wave / length;
            double half = SymmetricHalf(l2);
            double outTheory = Math.PI * scale, antiTheory = 2 * Math.PI * scale, symTheory = 2 * half * scale;
            double periods = 12;
            double outMeasured = Ring(span, u => Math.Sin(Math.PI * u), new Double3(0, 0, 1), 0.005 * sag, periods * 2 * Math.PI / outTheory);
            double antiMeasured = Ring(span, u => Math.Sin(2 * Math.PI * u), new Double3(0, 1, 0), 0.005 * sag, periods * 2 * Math.PI / antiTheory);
            double symMeasured = Ring(span, u => 1 - Math.Tan(half) * Math.Sin(2 * half * u) - Math.Cos(2 * half * u),
                new Double3(0, 1, 0), 0.005 * sag, periods * 2 * Math.PI / symTheory);
            double stringWrong = Math.PI * scale, inextensibleWrong = 2 * SymmetricHalf(1e12) * scale;
            TestContext.WriteLine($"{line}: H {span.H:E5} N, λ² {l2:F3} (4π² = {4 * Math.PI * Math.PI:F3}), √(H/m) {span.Wave:F3} m/s");
            TestContext.WriteLine($"  out of plane: theory {outTheory:F5} rad/s (ω̄ = π), chain {outMeasured:F5} ({(outMeasured / outTheory - 1) * 100:F4} %)");
            TestContext.WriteLine($"  in-plane antisymmetric: theory {antiTheory:F5} (2π), chain {antiMeasured:F5} ({(antiMeasured / antiTheory - 1) * 100:F4} %)");
            TestContext.WriteLine($"  in-plane symmetric: theory {symTheory:F5} (ω̄ = {2 * half / Math.PI:F4}π), chain {symMeasured:F5} " +
                $"({(symMeasured / symTheory - 1) * 100:F4} %); known-wrong taut string {stringWrong:F5} ({(symMeasured / stringWrong - 1) * 100:F2} %), " +
                $"inextensible {inextensibleWrong:F5} ({(symMeasured / inextensibleWrong - 1) * 100:F2} %)");
            TestContext.WriteLine($"  clips {span.Chain.Clips}, corrections {span.Chain.Corrections}, safe steps {span.Chain.SafeSteps}, wedges {span.Chain.Wedges}");
            Assert.That(Math.Abs(outMeasured / outTheory - 1), Is.LessThanOrEqualTo(0.01));
            Assert.That(Math.Abs(antiMeasured / antiTheory - 1), Is.LessThanOrEqualTo(0.01));
            Assert.That(Math.Abs(symMeasured / symTheory - 1), Is.LessThanOrEqualTo(0.01));
            Assert.That(Math.Abs(symMeasured / stringWrong - 1), Is.GreaterThan(0.01), "a taut string (no sag) fails");
            Assert.That(Math.Abs(symMeasured / inextensibleWrong - 1), Is.GreaterThan(0.01), "an inextensible cable fails");
            Assert.That(fiber ? l2 < 4 * Math.PI * Math.PI : l2 > 4 * Math.PI * Math.PI, Is.True, "the span's side of the crossover");
            Assert.That(span.Chain.Wedges, Is.EqualTo(0));
        }

        // ---- the capstan (Euler–Eytelwein): T_hi/T_lo ≤ e^(μs·θ) holds, sliding at e^(μk·θ) ----

        // fiber-tether: "The capstan holds and slips". The fiber over one capstan node on a fixed rod with μ 0.30/0.25:
        // the held end pulled by a dead 0.1 N, the loaded end by a soft spring (10 N/m) whose end walks away at 2 cm/s,
        // so the load rises 0.2 N/s: it holds until the load passes 0.1·e^(0.30·θ) N, then slides at e^(0.25·θ).
        [TestCase(90.0)]
        [TestCase(180.0)]
        public void TheCapstanHoldsAndSlips(double degrees)
        {
            double theta = degrees * Math.PI / 180;
            Double3 dIn = new Double3(1, 0, 0), dOut = new Double3(Math.Cos(theta), -Math.Sin(theta), 0);
            Double3 normal = dIn - dOut;
            normal = normal * (1 / normal.Length());
            var chain = new CableChain(3, FiberMass, FiberStiffness);
            chain.Reset(new[] { dIn * -1, default, dOut }, new[] { 0.0, 1.0, 2.0 }, 3);
            chain[0].Force = dIn * -0.1;
            chain[1].Contact0 = CableContact.On(default, normal, default, 1e3, 0, 1e3, 0.30, 0.25, true);
            chain[2].AnchorStiffness = 10;
            Double3 load0 = dOut * 1.01;
            chain[2].AnchorTarget = load0;

            double rs = Math.Exp(0.30 * theta), rk = Math.Exp(0.25 * theta);
            double heldMax = 0, onsetLoad = -1, slipWorst = 0;
            int slipSteps = 0;
            long slides = 0;
            for (int step = 1; step <= 1500; step++)
            {
                chain[2].AnchorTarget = load0 + dOut * (0.02 * step * Dt);
                chain[2].AnchorVelocity = dOut * 0.02;
                chain.Step(Dt);
                double ta = chain.Tension(0), tb = chain.Tension(1), ratio = tb / ta;
                if (chain.Slides == slides)
                {
                    if (onsetLoad < 0) heldMax = Math.Max(heldMax, ratio);
                }
                else
                {
                    if (onsetLoad < 0) onsetLoad = chain.AnchorForce(2).Length();
                    slipWorst = Math.Max(slipWorst, Math.Abs(ratio / rk - 1));
                    slipSteps++;
                    slides = chain.Slides;
                }
            }
            TestContext.WriteLine($"{degrees}°: e^(μs·θ) {rs:F5}, held up to a ratio {heldMax:F5} ({(heldMax / rs - 1) * 100:F3} %); " +
                $"slip at a load {onsetLoad:F5} N against {0.1 * rs:F5} ({(onsetLoad / (0.1 * rs) - 1) * 100:F3} %); " +
                $"{slipSteps} sliding steps at e^(μk·θ) {rk:F5} within {slipWorst * 100:F4} %; clips {chain.Clips}, corrections {chain.Corrections}, safe steps {chain.SafeSteps}, wedges {chain.Wedges}");
            Assert.That(onsetLoad, Is.GreaterThan(0), "it slips");
            Assert.That(Math.Abs(onsetLoad / (0.1 * rs) - 1), Is.LessThanOrEqualTo(0.01));
            Assert.That(Math.Abs(heldMax / rs - 1), Is.LessThanOrEqualTo(0.01));
            Assert.That(slipSteps, Is.GreaterThan(100));
            Assert.That(slipWorst, Is.LessThanOrEqualTo(0.01));
            Assert.That(chain.Wedges, Is.EqualTo(0));
        }

        // A wrap of 180° over seven capstan nodes on a rod of 2 cm radius (15°, 5 × 30°, 15°), loaded as above: the run
        // of nodes is one wrap, so it holds until the load passes 0.1·e^(0.30·π) and slides at e^(0.25·π), as one node does.
        [Test]
        public void TheCapstanComposesOverAWrap()
        {
            const double radius = 0.02;
            var x = new Double3[9];
            var s = new double[9];
            x[0] = new Double3(-1, radius, 0);
            for (int k = 0; k < 7; k++)
            {
                double phi = Math.PI / 2 - k * Math.PI / 6;
                x[k + 1] = new Double3(radius * Math.Cos(phi), radius * Math.Sin(phi), 0);
            }
            x[8] = new Double3(-1, -radius, 0);
            for (int i = 1; i < 9; i++) s[i] = s[i - 1] + (x[i] - x[i - 1]).Length();
            var chain = new CableChain(9, FiberMass, FiberStiffness);
            chain.Reset(x, s, 9);
            chain[0].Force = new Double3(-0.1, 0, 0);
            for (int k = 1; k <= 7; k++)
                chain[k].Contact0 = CableContact.On(x[k], x[k] * (1 / radius), default, 1e4, 0, 1e4, 0.30, 0.25, true);
            chain[8].AnchorStiffness = 10;
            Double3 away = new Double3(-1, 0, 0), load0 = x[8] + away * 0.01;
            double rs = Math.Exp(0.30 * Math.PI), rk = Math.Exp(0.25 * Math.PI);
            double heldMax = 0, onsetLoad = -1, slipWorst = 0;
            int slipSteps = 0;
            long slides = 0;
            for (int step = 1; step <= 2000; step++)
            {
                chain[8].AnchorTarget = load0 + away * (0.02 * step * Dt);
                chain[8].AnchorVelocity = away * 0.02;
                chain.Step(Dt);
                double ratio = chain.Tension(7) / chain.Tension(0);
                if (chain.Slides == slides)
                {
                    if (onsetLoad < 0) heldMax = Math.Max(heldMax, ratio);
                }
                else
                {
                    if (onsetLoad < 0) onsetLoad = chain.AnchorForce(8).Length();
                    slipWorst = Math.Max(slipWorst, Math.Abs(ratio / rk - 1));
                    slipSteps++;
                    slides = chain.Slides;
                }
            }
            TestContext.WriteLine($"seven nodes, 180°: held up to {heldMax:F5} against e^(μs·π) {rs:F5} ({(heldMax / rs - 1) * 100:F3} %); " +
                $"slip at a load {onsetLoad:F5} N against {0.1 * rs:F5} ({(onsetLoad / (0.1 * rs) - 1) * 100:F3} %); {slipSteps} sliding steps " +
                $"at e^(μk·π) {rk:F5} within {slipWorst * 100:F4} %; clips {chain.Clips}, corrections {chain.Corrections}, safe steps {chain.SafeSteps}, wedges {chain.Wedges}");
            Assert.That(Math.Abs(onsetLoad / (0.1 * rs) - 1), Is.LessThanOrEqualTo(0.01));
            Assert.That(Math.Abs(heldMax / rs - 1), Is.LessThanOrEqualTo(0.01));
            Assert.That(slipSteps, Is.GreaterThan(100));
            Assert.That(slipWorst, Is.LessThanOrEqualTo(0.01));
            Assert.That(chain.Wedges, Is.EqualTo(0));
        }

        // ---- energy ----

        sealed class Xorshift
        {
            ulong _s;
            public Xorshift(ulong seed) { _s = seed; }
            public double Next()
            {
                _s ^= _s << 13;
                _s ^= _s >> 7;
                _s ^= _s << 17;
                return (_s >> 11) * (1.0 / 9007199254740992.0);
            }
            public double In(double a, double b) => a + (b - a) * Next();
            public Double3 Unit()
            {
                double z = In(-1, 1), t = In(0, 2 * Math.PI), r = Math.Sqrt(1 - z * z);
                return new Double3(r * Math.Cos(t), r * Math.Sin(t), z);
            }
        }

        static CableContact RandomContact(Xorshift r, Double3 at, double mass)
        {
            Double3 normal = r.Unit();
            double k = Math.Pow(10, r.In(1, 5)), mu = r.In(0, 0.6);
            return CableContact.On(at - normal * r.In(-0.003, 0.003), normal, default, k,
                r.Next() < 0.5 ? 0 : r.In(0, 2) * Math.Sqrt(k * mass), r.Next() < 0.3 ? 0 : Math.Pow(10, r.In(1, 4)),
                mu, mu * r.In(0.5, 1), r.Next() < 0.5);
        }

        // fiber-tether: "Energy is never gained". 10 000 random chains (2 to 13 nodes, the fiber's or A-50's line with its
        // EA scaled 0.1–10×, segments stretched or slack, random velocities, fixed anchors, members on and off, two on one
        // node, capstan or not, insertion and merging on some), each stepped 40 times with nothing doing work on it: no
        // step's energy rises more than 1e-12 of its magnitude, no value is non-finite, no wedge.
        [Test]
        public void EnergyIsNeverGained()
        {
            var r = new Xorshift(0x9E3779B97F4A7C15UL);
            const int chains = 10000, steps = 40;
            double worst = double.NegativeInfinity, clipped = 0;
            long clips = 0, wedges = 0, slides = 0, splits = 0, merges = 0, wedge2 = 0, contactSteps = 0, corrections = 0, safe = 0;
            for (int c = 0; c < chains; c++)
            {
                bool fiber = r.Next() < 0.5;
                double mu = fiber ? FiberMass : A50Mass, ea = (fiber ? FiberStiffness : A50Stiffness) * Math.Pow(10, r.In(-1, 1));
                int n = 2 + (int)(r.Next() * 12);
                var x = new Double3[n];
                var s = new double[n];
                double h = r.In(0.05, 1);
                x[0] = new Double3(r.In(-5, 5), r.In(0, 10), r.In(-5, 5));
                for (int i = 1; i < n; i++)
                {
                    s[i] = s[i - 1] + h * r.In(0.5, 1.5);
                    double rest = s[i] - s[i - 1];
                    x[i] = x[i - 1] + r.Unit() * (rest * (r.Next() < 0.5 ? r.In(1, 1 + 2e-3) : r.In(0.6, 1)));
                }
                var chain = new CableChain(32, mu, ea) { Gravity = Down };
                chain.Reset(x, s, n);
                double speed = fiber ? 3 : 1;
                for (int i = 0; i < n; i++)
                {
                    chain[i].Velocity = r.Unit() * r.In(0, speed);
                    if (r.Next() < 0.2)
                    {
                        chain[i].AnchorStiffness = Math.Pow(10, r.In(1, 6));
                        chain[i].AnchorDamping = r.Next() < 0.5 ? 0 : r.In(0, 1) * Math.Sqrt(chain[i].AnchorStiffness * chain[i].Mass);
                        chain[i].AnchorTarget = chain[i].Position + r.Unit() * r.In(0, 0.01);
                    }
                    double roll = r.Next();
                    if (roll < 0.4) chain[i].Contact0 = RandomContact(r, chain[i].Position, chain[i].Mass);
                    if (roll < 0.1)
                    {
                        chain[i].Contact1 = RandomContact(r, chain[i].Position, chain[i].Mass);
                        wedge2++;
                    }
                }
                if (r.Next() < 0.3) chain.SetSpacing(h * r.In(0.5, 1.5), r.In(0, 2), r.In(1, 2), s[n - 1]);

                double e = chain.Energy(out double mag);
                for (int step = 0; step < steps; step++)
                {
                    chain.Step(Dt);
                    double next = chain.Energy(out double nextMag);
                    double rise = (next - e) / Math.Max(mag, nextMag);
                    worst = Math.Max(worst, rise);
                    Assert.That(double.IsNaN(next) || double.IsInfinity(next), Is.False, $"chain {c} step {step}: a non-finite energy");
                    Assert.That(rise, Is.LessThanOrEqualTo(CableChain.EnergyTolerance), $"chain {c} step {step}: the energy rose {rise:E3} of its magnitude " +
                        $"({e:E9} → {next:E9} J; the step's start {chain.EnergyAtStart:E9}, budget {chain.Budget:E9}, end {chain.EnergyAtEnd:E9}, " +
                        $"deficit {chain.Deficit:E3}, {chain.LastGuard}; slides {chain.Slides}, splits {chain.Splits}, merges {chain.Merges}, corrections {chain.Corrections})");
                    e = next;
                    mag = nextMag;
                    for (int i = 0; i < chain.Count; i++)
                        if (chain[i].Contact0.Engaged || chain[i].Contact1.Engaged) contactSteps++;
                }
                clips += chain.Clips;
                clipped += chain.ClippedEnergy;
                wedges += chain.Wedges;
                corrections += chain.Corrections;
                safe += chain.SafeSteps;
                slides += chain.Slides;
                splits += chain.Splits;
                merges += chain.Merges;
            }
            TestContext.WriteLine($"{chains} chains × {steps} steps: the worst rise {worst:E3} of the magnitude; {clips} clipped steps " +
                $"({100.0 * clips / (chains * steps):F3} %) taking back {clipped:E3} J in all; {corrections} corrections, {safe} safe steps " +
                $"({100.0 * safe / (chains * steps):F3} %); {wedges} wedges; {wedge2} nodes on two members, " +
                $"{contactSteps} node-steps on a member, {slides} slides, {splits} splits, {merges} merges");
            Assert.That(wedges, Is.EqualTo(0));
            Assert.That(slides, Is.GreaterThan(0));
            Assert.That(splits + merges, Is.GreaterThan(0));
        }

        // fiber-tether: "... and a forced breach is logged as TetherWedge". The check's verdicts on forced rises, and a
        // non-finite value in a chain: the step is undone, the line stopped, the wedge recorded with its node.
        [Test]
        public void AForcedBreachIsAWedge()
        {
            Assert.That(CableChain.Judge(1e-13, 0, 1, 1e-12), Is.EqualTo(CableGuard.Kept));
            Assert.That(CableChain.Judge(1e-6, 2e-6, 1, 1e-12), Is.EqualTo(CableGuard.Clipped));
            Assert.That(CableChain.Judge(1e-6, 0, 1, 1e-12), Is.EqualTo(CableGuard.Wedge), "a rise the linearisation cannot make");
            Assert.That(CableChain.Judge(1e-6, 2e-6, 1e-7, 1e-12), Is.EqualTo(CableGuard.Wedge), "more than the motion holds");
            Assert.That(CableChain.Judge(double.NaN, 1, 1, 1e-12), Is.EqualTo(CableGuard.Wedge));

            var chain = new CableChain(4, FiberMass, FiberStiffness) { Gravity = Down };
            chain.Reset(new[] { new Double3(0, 1, 0), new Double3(0.1, 1, 0), new Double3(0.2, 1, 0) }, new[] { 0.0, 0.1, 0.2 }, 3);
            Anchor(chain, 0, 1e4);
            for (int step = 0; step < 10; step++) chain.Step(Dt);
            Double3 at = chain[1].Position;
            chain[1].Force = new Double3(double.NaN, 0, 0);
            chain.Step(Dt);
            Assert.That(chain.Wedges, Is.EqualTo(1));
            Assert.That(chain.LastGuard, Is.EqualTo(CableGuard.Wedge));
            Assert.That(chain.LastWedge.NonFinite, Is.True);
            Assert.That(chain.LastWedge.Node, Is.GreaterThanOrEqualTo(0));
            Assert.That(chain.LastWedge.Step, Is.EqualTo(11));
            Assert.That(chain[1].Position.X, Is.EqualTo(at.X), "the step is undone");
            Assert.That(chain[1].Velocity.Length(), Is.EqualTo(0.0), "the line is stopped");
            chain[1].Force = default;
            chain.Step(Dt);
            Assert.That(chain.LastGuard, Is.Not.EqualTo(CableGuard.Wedge), "the line goes on");
        }

        // ---- stability at the design's ω·dt, and the known-wrong explicit step ----

        // A taut line: the fiber at half its proof tension in 0.1 m segments (ω·dt ≈ 36 axially), or A-50's span in
        // 0.5 m ones (≈ 9.6). Every node kicked at random: 5000 steps never gain energy and stay finite; the same chain
        // stepped explicitly (semi-implicit Euler, the drone's integrator) blows up.
        [TestCase("fiber")]
        [TestCase("A-50")]
        public void StableAtTheDesignsOmegaDt(string line)
        {
            bool fiber = line == "fiber";
            CableChain chain;
            double segment;
            if (fiber)
            {
                segment = 0.1;
                const int n = 101;
                double ea = FiberStiffness, tension = 0.5 * Table().Number("fiber.proof_force_n");
                var x = new Double3[n];
                var s = new double[n];
                for (int i = 0; i < n; i++)
                {
                    x[i] = new Double3(i * segment, 5, 0);
                    s[i] = i * segment / (1 + tension / ea);
                }
                chain = new CableChain(n, FiberMass, ea) { Gravity = Down };
                chain.Reset(x, s, n);
                Anchor(chain, 0, 100 * ea / segment);
                Anchor(chain, n - 1, 100 * ea / segment);
            }
            else
            {
                segment = 0.5;
                chain = TautSpan(A50Mass, A50Stiffness, 40, 0.8, segment).Chain;
            }
            double nodeMass = chain[1].Mass, k = chain.AxialStiffness / (chain[2].Material - chain[1].Material);
            double omegaDt = Math.Sqrt(k / nodeMass) * Dt;

            var r = new Xorshift(42);
            int count = chain.Count;
            var x0 = new Double3[count];
            var v0 = new Double3[count];
            var rest = new double[count];
            var mass = new double[count];
            for (int i = 1; i + 1 < count; i++) chain[i].Velocity = r.Unit() * (fiber ? 1 : 0.5);
            for (int i = 0; i < count; i++)
            {
                x0[i] = chain[i].Position;
                v0[i] = chain[i].Velocity;
                rest[i] = chain[i].Material;
                mass[i] = chain[i].Mass;
            }
            double e = chain.Energy(out double mag), first = e, worst = double.NegativeInfinity;
            for (int step = 0; step < 5000; step++)
            {
                chain.Step(Dt);
                double next = chain.Energy(out double nextMag);
                worst = Math.Max(worst, (next - e) / Math.Max(mag, nextMag));
                e = next;
                mag = nextMag;
            }

            // The known-wrong explicit step: v += dt·f/m, x += dt·v, the same springs, anchors and gravity.
            var xe = (Double3[])x0.Clone();
            var ve = (Double3[])v0.Clone();
            double kAnchor = chain[0].AnchorStiffness, eExplicit0 = ExplicitEnergy(xe, ve, rest, mass, chain.AxialStiffness, kAnchor, x0), eExplicit = eExplicit0;
            int blew = -1;
            for (int step = 0; step < 1000 && blew < 0; step++)
            {
                ExplicitStep(xe, ve, rest, mass, chain.AxialStiffness, kAnchor, x0);
                eExplicit = ExplicitEnergy(xe, ve, rest, mass, chain.AxialStiffness, kAnchor, x0);
                if (double.IsNaN(eExplicit) || double.IsInfinity(eExplicit) || eExplicit > 1e6 * Math.Abs(eExplicit0)) blew = step + 1;
            }
            TestContext.WriteLine($"{line}: {segment} m segments, ω·dt {omegaDt:F2} axially; implicit: energy {first:E4} → {e:E4} J over 5 s, " +
                $"worst rise {worst:E3} of the magnitude, clips {chain.Clips}, corrections {chain.Corrections}, safe steps {chain.SafeSteps}, wedges {chain.Wedges}; explicit: past 1e6× its energy " +
                $"or non-finite at step {blew}");
            Assert.That(omegaDt, Is.EqualTo(fiber ? 36 : 9.6).Within(fiber ? 1.0 : 0.3));
            Assert.That(worst, Is.LessThanOrEqualTo(CableChain.EnergyTolerance));
            Assert.That(double.IsNaN(e) || double.IsInfinity(e), Is.False);
            Assert.That(e, Is.LessThan(first));
            Assert.That(chain.Wedges, Is.EqualTo(0));
            Assert.That(blew, Is.GreaterThan(0).And.LessThanOrEqualTo(100), "the explicit step blows up");
        }

        static void ExplicitStep(Double3[] x, Double3[] v, double[] s, double[] m, double ea, double kAnchor, Double3[] anchors)
        {
            int n = x.Length;
            var f = new Double3[n];
            for (int i = 0; i < n; i++) f[i] = Down * m[i];
            f[0] = f[0] - (x[0] - anchors[0]) * kAnchor;
            f[n - 1] = f[n - 1] - (x[n - 1] - anchors[n - 1]) * kAnchor;
            for (int j = 0; j + 1 < n; j++)
            {
                Double3 d = x[j + 1] - x[j];
                double l = d.Length(), rest = s[j + 1] - s[j];
                if (l > rest)
                {
                    Double3 pull = d * (ea / rest * (l - rest) / l);
                    f[j] = f[j] + pull;
                    f[j + 1] = f[j + 1] - pull;
                }
            }
            for (int i = 0; i < n; i++)
            {
                v[i] = v[i] + f[i] * (Dt / m[i]);
                x[i] = x[i] + v[i] * Dt;
            }
        }

        static double ExplicitEnergy(Double3[] x, Double3[] v, double[] s, double[] m, double ea, double kAnchor, Double3[] anchors)
        {
            int n = x.Length;
            double e = 0;
            for (int i = 0; i < n; i++) e += 0.5 * m[i] * Double3.Dot(v[i], v[i]) - m[i] * Double3.Dot(Down, x[i]);
            Double3 a = x[0] - anchors[0], b = x[n - 1] - anchors[n - 1];
            e += 0.5 * kAnchor * (Double3.Dot(a, a) + Double3.Dot(b, b));
            for (int j = 0; j + 1 < n; j++)
            {
                double l = (x[j + 1] - x[j]).Length(), rest = s[j + 1] - s[j];
                if (l > rest) e += 0.5 * ea / rest * (l - rest) * (l - rest);
            }
            return e;
        }

        // ---- insertion and merging ----

        // A free fiber line of 20 nodes 0.5 m apart (a gentle zigzag at its rest lengths), drifting and turning with no
        // gravity, its spacing set to 0.1 m within 1 m of its last node growing by 1.5× a node beyond: it splits near that
        // end and merges far from it; its mass stays μ·L, its momentum stays put (no external force), its energy never
        // rises, and every segment ends within its target.
        [Test]
        public void InsertionAndMergingKeepMassAndMomentum()
        {
            double mu = FiberMass;
            const int n = 20;
            var x = new Double3[n];
            var s = new double[n];
            var r = new Xorshift(7);
            for (int i = 0; i < n; i++)
            {
                x[i] = new Double3(0.5 * i, i % 2 == 0 ? 0 : 0.01, 0);
                if (i > 0) s[i] = s[i - 1] + (x[i] - x[i - 1]).Length();
            }
            var chain = new CableChain(64, mu, FiberStiffness);
            chain.Reset(x, s, n);
            for (int i = 0; i < n; i++) chain[i].Velocity = new Double3(1, 2, 0) + new Double3(0, 0, 0.2 * (i - 9.5) / 9.5) + r.Unit() * 0.01;
            Double3 p0 = Momentum(chain), previous = p0;
            double length = s[n - 1], e = chain.Energy(out double mag), worst = double.NegativeInfinity, drift = 0;
            chain.SetSpacing(0.1, 1, 1.5, length);
            for (int step = 0; step < 500; step++)
            {
                chain.Step(Dt);
                double next = chain.Energy(out double nextMag);
                worst = Math.Max(worst, (next - e) / Math.Max(mag, nextMag));
                e = next;
                mag = nextMag;
                Double3 p = Momentum(chain);
                drift = Math.Max(drift, (p - previous).Length() / p0.Length());
                previous = p;
            }
            double total = 0, worstFit = 0;
            for (int i = 0; i < chain.Count; i++) total += chain[i].Mass;
            for (int j = 0; j + 1 < chain.Count; j++)
            {
                double a = chain[j].Material, b = chain[j + 1].Material, away = length - 0.5 * (a + b);
                double target = away <= 1 ? 0.1 : 0.1 + 0.5 * (away - 1);
                worstFit = Math.Max(worstFit, (b - a) / target);
            }
            TestContext.WriteLine($"{chain.Splits} splits, {chain.Merges} merges: {n} → {chain.Count} nodes; mass {total:E9} of μ·L {mu * length:E9}; " +
                $"momentum's worst change in a step {drift:E2} of it, {(Momentum(chain) - p0).Length() / p0.Length():E2} over 500; worst energy rise {worst:E3}; longest segment {worstFit:F3} of its target; clips {chain.Clips}");
            Assert.That(chain.Splits, Is.GreaterThan(0));
            Assert.That(chain.Merges, Is.GreaterThan(0));
            Assert.That(total, Is.EqualTo(mu * length).Within(1e-12).Percent);
            Assert.That(drift, Is.LessThanOrEqualTo(1e-13), "momentum kept to rounding each step");
            Assert.That((Momentum(chain) - p0).Length() / p0.Length(), Is.LessThanOrEqualTo(1e-10));
            Assert.That(worst, Is.LessThanOrEqualTo(CableChain.EnergyTolerance));
            Assert.That(worstFit, Is.LessThanOrEqualTo(1.0));
            Assert.That(chain[0].Material, Is.EqualTo(0.0));
            Assert.That(chain[chain.Count - 1].Material, Is.EqualTo(length));
        }

        static Double3 Momentum(CableChain c)
        {
            Double3 p = default;
            for (int i = 0; i < c.Count; i++) p = p + c[i].Velocity * c[i].Mass;
            return p;
        }

        // ---- 0 B, the bits, the cost ----

        // A busy line: anchored at one end, swung by the other, over a capstan rod and a floor, splitting and merging.
        static CableChain Busy()
        {
            const int n = 24;
            var x = new Double3[n];
            var s = new double[n];
            for (int i = 0; i < n; i++)
            {
                s[i] = 0.25 * i;
                x[i] = new Double3(0.25 * i, 1 + 0.05 * Math.Sin(i), 0.01 * i);
            }
            var chain = new CableChain(64, FiberMass, FiberStiffness) { Gravity = Down };
            chain.Reset(x, s, n);
            Anchor(chain, 0, 1e4);
            chain[n - 1].AnchorStiffness = 1e3;
            chain[n - 1].AnchorDamping = 0.01;
            chain[8].Contact0 = CableContact.On(chain[8].Position + new Double3(0, -0.001, 0), new Double3(0, 1, 0), default, 1e3, 0.01, 1e3, 0.3, 0.25, true);
            chain[16].Contact0 = CableContact.On(new Double3(0, 0.8, 0), new Double3(0, 1, 0), default, 1e3, 0.01, 1e2, 0.5, 0.4, false);
            chain[16].Contact1 = CableContact.On(chain[16].Position + new Double3(0.02, 0, 0), new Double3(-1, 0, 0), default, 1e3, 0.01, 1e2, 0.5, 0.4, true);
            chain.SetSpacing(0.1, 0.5, 1.5, s[n - 1]);
            return chain;
        }

        static void Swing(CableChain chain, int step)
        {
            DetMath.SinCos(step * 0.004, out double sin, out double cos);
            int last = chain.Count - 1;
            chain[last].AnchorTarget = new Double3(5.75 + 0.5 * cos, 1.5 + 0.5 * sin, 0.3);
            chain[last].AnchorVelocity = new Double3(-0.002 * sin / Dt, 0.002 * cos / Dt, 0);
            for (int i = 0; i < chain.Count; i++) chain[i].Drag = Iso(0.001 * (1 + 0.1 * cos));
        }

        [Test]
        public void AStepAllocatesNothing()
        {
            CableChain chain = Busy();
            for (int step = 0; step < 500; step++)
            {
                Swing(chain, step);
                chain.Step(Dt);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int step = 500; step < 3000; step++)
            {
                Swing(chain, step);
                chain.Step(Dt);
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine($"2500 steps of {chain.Count} nodes: {bytes} B; slides {chain.Slides}, splits {chain.Splits}, merges {chain.Merges}");
            Assert.That(bytes, Is.EqualTo(0));
            Assert.That(chain.Splits + chain.Merges, Is.GreaterThan(0));
        }

        // Determinism: the busy line's bits after 3000 steps, twice, and pinned (recorded on .NET 10; Unity's Mono must
        // give the same: a difference there is a determinism bug, never a reason to re-pin).
        [Test]
        public void TheSameStepsGiveTheSameBits()
        {
            ulong a = BusyHash(), b = BusyHash();
            TestContext.WriteLine($"the busy line after 3000 steps: 0x{a:X16}");
            Assert.That(b, Is.EqualTo(a));
            Assert.That(a, Is.EqualTo(PinnedBusy), "recorded on .NET 10");
        }

        const ulong PinnedBusy = 0x5BCF01C10EFF9831UL;

        static ulong BusyHash()
        {
            CableChain chain = Busy();
            for (int step = 0; step < 3000; step++)
            {
                Swing(chain, step);
                chain.Step(Dt);
            }
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < chain.Count; i++)
            {
                h = Fold(h, chain[i].Position.X);
                h = Fold(h, chain[i].Position.Y);
                h = Fold(h, chain[i].Position.Z);
                h = Fold(h, chain[i].Velocity.X);
                h = Fold(h, chain[i].Velocity.Y);
                h = Fold(h, chain[i].Velocity.Z);
                h = Fold(h, chain[i].Material);
            }
            h = Fold(h, chain.Count);
            h = Fold(h, chain.Slides);
            return h;
        }

        static ulong Fold(ulong h, double v)
        {
            long bits = BitConverter.DoubleToInt64Bits(v);
            for (int k = 0; k < 8; k++)
            {
                h ^= (byte)(bits >> (8 * k));
                h *= 1099511628211UL;
            }
            return h;
        }

        // The solve's cost a node (design 13: about 200 flops a node): a 32-node fiber span and a 120-node conductor,
        // printed for the as-built note; a figure counts only on AC (the laptop's iGPU fallback on battery).
        [Test]
        public void TheCostOfANode()
        {
            // Each size twice: the first runs while the JIT still compiles its tiers, the second is the figure.
            for (int run = 0; run < 4; run++)
            {
                int n = run % 2 == 0 ? 32 : 120;
                var x = new Double3[n];
                var s = new double[n];
                for (int i = 0; i < n; i++)
                {
                    s[i] = 0.5 * i;
                    x[i] = new Double3(0.5 * i, 10 - 0.001 * i * (n - 1 - i), 0);
                }
                var chain = new CableChain(n, n == 32 ? FiberMass : A50Mass, n == 32 ? FiberStiffness : A50Stiffness) { Gravity = Down };
                chain.Reset(x, s, n);
                Anchor(chain, 0, 1e6);
                Anchor(chain, n - 1, 1e6);
                for (int step = 0; step < 5000; step++) chain.Step(Dt);
                long corrections = chain.Corrections, safe = chain.SafeSteps, clips = chain.Clips;
                var watch = Stopwatch.StartNew();
                const int steps = 10000;
                for (int step = 0; step < steps; step++) chain.Step(Dt);
                watch.Stop();
                double perStep = watch.Elapsed.TotalMilliseconds * 1000 / steps;
#if DEBUG
                const string build = "Debug";
#else
                const string build = "Release";
#endif
                TestContext.WriteLine($"{n} nodes: {perStep:F2} µs a step, {perStep / n * 1000:F0} ns a node ({build}{(run < 2 ? ", the JIT still compiling" : "")}); over the timed steps " +
                    $"{chain.Corrections - corrections} corrections, {chain.SafeSteps - safe} safe steps, {chain.Clips - clips} clips");
            }
        }
    }
}
